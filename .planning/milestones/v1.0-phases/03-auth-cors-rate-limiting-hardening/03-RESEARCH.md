# Phase 3: Auth, CORS & Rate Limiting Hardening - Research

**Researched:** 2026-08-31
**Domain:** ASP.NET Core 8 authentication (JWT), CORS middleware, rate limiting middleware, structured security logging
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**AUTH-01/AUTH-02 — JWT lifetime & TokenValidationParameters**
- **D-01:** JWT lifetime reduced from 7 days (`AuthService.cs:71`, `DateTime.UtcNow.AddDays(7)`) to **24 hours**. No refresh-token rotation is in scope this milestone (SEC-01 is deferred to v2) — this is a straight lifetime cut, not a UX redesign.
- **D-02:** Frontend expiry UX stays as-is: `AuthInterceptor` already catches HTTP 401 and calls `auth.logout()` (redirect to `/login`). No new "session expired" message — this remains a backend/auth-config phase, not a frontend UX phase.
- **D-03:** `TokenValidationParameters.ClockSkew` stays at the library default (5 minutes) — absorbs reasonable clock drift on a single-server deployment; tightening it was considered and rejected as unnecessary risk for no real security gain here.
- **D-04:** Explicitly pin `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]` and `RequireExpirationTime = true` on `TokenValidationParameters` (`Program.cs`) as defense-in-depth against algorithm-confusion attacks — satisfies AUTH-02's "explicitly reviewed and configured" wording beyond the Issuer/Audience/Lifetime validation that already exists.

**NET-01/NET-02 — CORS lockdown**
- **D-05:** CORS origin loads from the existing (currently unused) `App:FrontendUrl` config key (`appsettings.json`) instead of the hardcoded `"http://localhost:4200"` literal in `Program.cs`. Single-origin only — no comma-separated multi-origin support, since there's no current need for more than one deployed frontend.
- **D-06:** CORS allowed headers restricted to exactly `Authorization` + `Content-Type` — confirmed by scanning `game-planner-ui/src/app` (only `AuthInterceptor` sets a custom header, `Authorization`; POST/PUT bodies use the browser's default `Content-Type`). No other custom headers exist anywhere in the frontend.
- **D-07:** CORS allowed methods restricted to exactly `GET, POST, PUT, DELETE` — confirmed by scanning frontend services; no PATCH or other verbs are used.
- **D-08:** Verification for ROADMAP.md success criterion 4 ("real Angular SPA can still call every endpoint with no CORS errors") is a **full manual browser walkthrough**, not a scripted/curl-based preflight check — matches the criterion's explicit wording.

**NET-03 — Rate limiting on data-mutation endpoints**
- **D-09:** Scope is exactly **Games + Journal + Session controllers** (`GamesController`, `GameJournalController`, `SessionController`) POST/PUT/DELETE endpoints — matches NET-03's literal wording and ROADMAP.md success criterion 5. `ProfileController`, `NowPlayingController`, `TrackedGamesController`, `SteamController` (import), `SyncController`/`WorkerController` (admin-ish run triggers) are explicitly **not** in scope for this phase.
- **D-10:** Limit: **100 requests / hour per user** — generous enough for normal backlog/journal/session editing bursts while still bounding abuse; matches the illustrative number in `CONCERNS.md`'s fix-approach note (not previously a locked decision, now is).
- **D-11:** Partition key: the new limiter partitions by **JWT user ID** (the same `NameIdentifier` claim controllers already extract) — required by ROADMAP.md success criterion 5 ("one user's rejections don't affect another user's requests"). This was the only viable mechanism given that requirement, not a genuine multi-option choice.
- **D-12:** One **shared rate-limiter policy** across all in-scope endpoints (not a separate named policy per controller) — mirrors the existing single `"auth"` policy pattern in `Program.cs`; no evidence yet that games/journal/session need different limits from each other.

**OBS-01 — Security event logging**
- **D-13:** Mechanism is **structured `ILogger` output only** — no new logging infrastructure (Seq/ELK is explicitly out of scope per `PROJECT.md`). "Central log stream" means the project's existing single configured `Logging` provider (console/file via `appsettings.json` `Logging` section), following the same `ILogger<T>` + named-parameter convention used everywhere else in the codebase.
- **D-14:** Failed logins: `AuthService.LoginAsync` currently has **zero logging** — no `ILogger` is even injected. Add `logger.LogWarning("Failed login attempt for {Email}: {Reason}", ...)` on both the "user not found/inactive" and "wrong password" branches. Never log the password. `AuthService` needs `ILogger<AuthService>` added to its constructor.
- **D-15:** Rate-limit rejections (both the existing `"auth"` limiter and the new games/journal/session mutation limiter) get logged via the standard ASP.NET Core `RateLimiterOptions.OnRejected` callback — one shared handler logging `{UserId or IP}`, `{Endpoint}`, `{PolicyName}` before the 429 response is sent.
- **D-16:** CORS rejections are the one item flagged as **needing research during planning**, not fully locked here: ASP.NET Core's CORS middleware has no built-in rejection hook analogous to `OnRejected` (a blocked preflight just omits CORS headers; the browser enforces the block client-side, so the server doesn't see it as an "error"). The user's direction is to attempt logging at the `Program.cs` CORS-policy level via a custom check before the CORS middleware — this research resolves the concrete mechanism (see `## Architecture Patterns` below). **Reversibility:** reversible — this is a logging-only addition with no functional/security behavior change if it turns out infeasible and gets scoped down to policy-level logging only.

### Claude's Discretion
- Exact wording/structured-parameter names of the new log statements (D-14, D-15) — follow this project's existing `ILogger<T>` + named-parameter conventions (e.g., patterns already used in `IgdbService`, `SteamService`).
- Concrete implementation mechanism for CORS rejection logging (D-16) — resolved by this research (see below); planner should treat the recommended approach as the primary plan, with the documented alternative as fallback only if the primary proves impractical during implementation.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. One cross-scope item (session-planner cross-user leak, `SessionManager.cs:15`) was explicitly **folded in** rather than deferred, per user decision — see Folded Todo below.

### Folded Todo (in scope, self-contained)
**Session planner leaks cross-user game data** (`.planning/todos/pending/2026-08-29-session-planner-leaks-cross-user-game-data.md`, `SessionManager.cs:15`, major severity) — folded into Phase 3 scope at the user's explicit request, despite not being a native fit for the phase's JWT/CORS/rate-limiting/logging domain (it's a missing `.Where(g => g.UserId == userId)` filter — an access-control/data-isolation bug). Treat as an added, self-contained fix, independent of the AUTH/NET/OBS decisions above.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AUTH-01 | JWT lifetime reduced from 7 days to a deliberate, bounded value | D-01: change `AddDays(7)` → `AddHours(24)` in `AuthService.GenerateToken`. Verified via decoded-token `exp` claim inspection (see Verification section of the plan). |
| AUTH-02 | `TokenValidationParameters` explicitly reviewed and configured (issuer, audience, lifetime validation) | Already validates Issuer/Audience/Lifetime/SigningKey (confirmed in current `Program.cs`). D-04 adds `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]` and `RequireExpirationTime = true` — both properties verified to exist on `TokenValidationParameters` via Microsoft Learn API reference (see Code Examples). |
| NET-01 | CORS allowed origins load from environment-based configuration (no hardcoded `localhost`) | D-05: read `App:FrontendUrl` (already present, currently unused, already `"http://localhost:4200"` in `appsettings.json`) instead of the hardcoded string literal in `Program.cs`. Confirmed no override in `appsettings.Development.json` or user-secrets — value is unchanged, this is a config-source swap only. |
| NET-02 | CORS restricted to explicit headers/methods (no `AllowAnyHeader`/`AllowAnyMethod`) | D-06/D-07: `.WithHeaders("Authorization", "Content-Type")` + `.WithMethods("GET","POST","PUT","DELETE")`, confirmed against actual frontend `AuthInterceptor` and service code (no other custom headers, no PATCH/other verbs found). |
| NET-03 | Rate limiting extended to data-mutation endpoints (game/journal/session POST/PUT/DELETE), partitioned by JWT user ID | D-09 through D-12: new named rate-limiter policy using `options.AddPolicy<string>(...)` (NOT `AddFixedWindowLimiter`, which does not partition — see Pitfall 1) with `ClaimTypes.NameIdentifier` as partition key. Critical middleware-order finding below (Pitfall 2) is required for this to actually work. |
| OBS-01 | Security-relevant events logged centrally (failed logins, CORS rejections, rate-limit rejections) | D-14/D-15/D-16: `ILogger<AuthService>` for failed logins, `RateLimiterOptions.OnRejected` for rate-limit rejections (both verified via official Microsoft docs), and a custom pre-`UseCors()` middleware for CORS rejection logging (concrete mechanism resolved below, D-16's open question). |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

From `./CLAUDE.md` (root) and `./.claude/CLAUDE.md` (milestone-specific):

- **Stored procedures ONLY for Insert/Update/Delete; EF LINQ for reads.** Not applicable to this phase's AUTH/NET/OBS work (no new persisted entities). The folded `SessionManager.cs` fix is a read-only LINQ query (`db.Games.Where(...)`) — adding a `.Where(g => g.UserId == userId)` clause stays within the EF LINQ read pattern, no stored procedure needed.
- **Services are ONLY for external APIs. Logic belongs in Managers.** This phase touches `AuthService` (external-API-adjacent: auth token issuance, permitted as a Service per existing precedent — it already lives there) and `Program.cs` (composition root, not a Manager/Service). No new business logic is being added to a Service that should live in a Manager.
- **No unnecessary abstractions, design patterns, or features beyond what's requested.** The CORS-rejection-logging mechanism recommended below (a small custom middleware) is the minimal implementation that satisfies D-16 — an `ICorsService` decorator (also documented, as a fallback) is more "correct" but adds DI-registration complexity not justified unless the simple middleware proves insufficient.
- **Priority: Clarity > Simplicity > Performance > Completeness.** Favors the simple pre-`UseCors()` middleware (Pitfall/Pattern below) over the `ICorsService` decorator for D-16 unless the simple approach is later found to under- or over-report.
- **Be concise; work step-by-step; generate only required files/classes.** No new controllers, DTOs, or abstractions should be introduced for this phase beyond what's listed in the Architecture Patterns section.

None of this phase's locked decisions (D-01 through D-16) conflict with these constraints.

## Summary

This phase touches exactly one composition root (`Program.cs`), one service (`AuthService.cs`), three controllers (`GamesController`, `GameJournalController`, `SessionController`), and one manager (`SessionManager.cs`, the folded fix). All of the ASP.NET Core primitives needed (JWT `TokenValidationParameters`, CORS middleware, `System.Threading.RateLimiting`) are already part of the `Microsoft.NET.Sdk.Web` shared framework and already in use in this codebase — **no new NuGet packages are required for this phase.**

The single most important finding from grounding this research in the actual current code: **the existing middleware pipeline order (`UseCors` → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization`) will silently break D-11's per-user rate-limit partitioning requirement.** `UseRateLimiter()` currently runs *before* `UseAuthentication()`, so `HttpContext.User` is an unauthenticated principal at the moment the rate limiter's partition-key function would run — every request would fall into the same "anonymous" partition regardless of which user is authenticated, defeating the entire purpose of D-11. The Microsoft-official `WebRateLimitAuth` sample (cited below) places `UseAuthentication()` before `UseRateLimiter()`, before `UseAuthorization()`. This phase's plan must reorder the pipeline, not just add a new policy.

The second key finding is that the existing `"auth"` policy (`AddFixedWindowLimiter("auth", ...)`) uses a mechanism (`AddFixedWindowLimiter(policyName, options)`) that has **no partitioning at all** — it is one shared global bucket for every caller under that policy name. D-12's instruction to "mirror the existing `'auth'` policy pattern" refers to the **usage pattern** (`[EnableRateLimiting("name")]` attributes on controller actions), not the **registration mechanism** — the new mutation policy must use `options.AddPolicy<string>(policyName, partitioner)` instead, since only `AddPolicy` supports a per-request partition-key function.

For D-16 (CORS rejection logging), ASP.NET Core genuinely has no `OnRejected`-equivalent hook for CORS. The user's directed approach — a custom check before `app.UseCors()` — is both the simplest and the most aligned-with-project-conventions solution: a small middleware that reads the incoming `Origin` header, compares it against the configured `App:FrontendUrl` value, and logs a warning when they don't match (only when an `Origin` header is actually present, to avoid false positives on non-browser API calls). A more precise alternative (decorating `ICorsService.EvaluatePolicy`, whose `CorsResult.IsOriginAllowed` property is confirmed to exist via the official API reference) is documented as a fallback if the simple approach proves insufficient during implementation, but the simple middleware is recommended as primary per this project's "no unnecessary abstractions" constraint.

**Primary recommendation:** Fix the middleware order first (`UseAuthentication`/`UseAuthorization` before `UseRateLimiter`), then layer in the JWT hardening, CORS config-source swap, the new `AddPolicy`-based mutation rate limiter, and the three logging additions (failed login, rate-limit `OnRejected`, CORS pre-check middleware) — all using built-in ASP.NET Core 8 primitives, no new packages.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| JWT token issuance & lifetime (AUTH-01) | API/Backend | — | `AuthService.GenerateToken` is a backend concern; frontend only stores/sends the opaque token, no frontend change needed (D-02) |
| JWT validation (AUTH-02) | API/Backend | — | `TokenValidationParameters` is JwtBearer middleware config in `Program.cs`, evaluated per-request server-side |
| CORS policy definition & enforcement (NET-01/NET-02) | API/Backend | Browser/Client | Policy is defined and evaluated server-side; the *browser* is the actual enforcer of the block (blocks JS from reading the response), which is why server-side "rejection" logging (D-16) requires a workaround — the server doesn't natively see a CORS failure as an error |
| Rate limiting (NET-03) | API/Backend | — | `System.Threading.RateLimiting` middleware, partition key derived from server-side `HttpContext.User` claims |
| Security event logging (OBS-01) | API/Backend | — | `ILogger<T>` output to the existing configured `Logging` provider; no new infrastructure tier introduced |
| Cross-user data isolation fix (folded todo) | API/Backend (Managers) | Database/Storage | `SessionManager.cs` LINQ query missing a `UserId` filter — a Managers-tier read-query fix, not a database schema change |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.0 (already referenced) | JWT bearer authentication scheme, `TokenValidationParameters` | Already in `GamePlanner.API.csproj`; official ASP.NET Core JWT auth package `[VERIFIED: GamePlanner.API.csproj]` |
| `Microsoft.IdentityModel.Tokens` | transitive (via JwtBearer) | `TokenValidationParameters`, `SecurityAlgorithms`, `SymmetricSecurityKey` | Already in use in `Program.cs`/`AuthService.cs`; `ValidAlgorithms` property confirmed via Microsoft Learn API reference `[VERIFIED: learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.validalgorithms]` |
| `Microsoft.AspNetCore.Cors` (part of `Microsoft.NET.Sdk.Web` shared framework) | 8.0 (in-box) | CORS middleware (`AddCors`, `UseCors`, `ICorsService`) | Already used in `Program.cs` with no separate `PackageReference` — confirmed part of the ASP.NET Core shared framework, not a standalone NuGet package `[VERIFIED: GamePlanner.API.csproj — no separate Cors package listed]` |
| `System.Threading.RateLimiting` + `Microsoft.AspNetCore.RateLimiting` (in-box) | 8.0 (in-box) | Rate limiting primitives (`PartitionedRateLimiter`, `RateLimitPartition`, `AddFixedWindowLimiter`, `AddPolicy`) | Already used in `Program.cs` with no separate `PackageReference` — part of .NET 8 runtime + ASP.NET Core shared framework `[VERIFIED: GamePlanner.API.csproj — no separate RateLimiting package listed]` |

### Supporting
No new supporting libraries required. `Microsoft.Extensions.Logging.Abstractions` (`ILogger<T>`) is already implicit via `Microsoft.NET.Sdk.Web` and already used project-wide (`IgdbService`, `SteamService`, Workers).

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Built-in `System.Threading.RateLimiting` `AddPolicy` partitioning | Third-party libraries (e.g., `AspNetCoreRateLimit`) | Not needed — built-in .NET 8 rate limiting already supports per-claim partitioning natively; adding a third-party package would violate "no unnecessary abstractions" and introduce an unneeded dependency |
| Simple pre-`UseCors()` middleware for D-16 | `ICorsService` decorator wrapping `EvaluatePolicy` | Decorator is more precise (matches exact CORS evaluation logic, handles per-endpoint `[EnableCors]` policies if ever introduced) but requires manual DI-registration-order tricks (no `Scrutor` package in this project) — documented as fallback only |
| `ClaimTypes.NameIdentifier` for rate-limit partition key | `sub` claim / custom claim | Project already extracts `ClaimTypes.NameIdentifier` in every controller's `GetUserId()` helper (`GamesController`, `GameJournalController`) — reusing the same claim keeps partition key and business-logic user-scoping consistent |

**Installation:** None — no new packages required. All primitives are already referenced or in-box.

**Version verification:** `Microsoft.AspNetCore.Authentication.JwtBearer` confirmed at 8.0.0 in `GamePlanner.API.csproj` (read directly, not a registry lookup — this is an internal dependency check, not an external package installation).

## Package Legitimacy Audit

**Not applicable this phase.** No new external packages are introduced — every API used (`TokenValidationParameters`, `CorsOptions`/`ICorsService`, `RateLimiterOptions`/`PartitionedRateLimiter`) is already referenced via existing `PackageReference` entries or is part of the in-box `Microsoft.NET.Sdk.Web` shared framework, confirmed by reading `GamePlanner.API.csproj` directly. The Package Legitimacy Gate protocol is skipped per its own "whenever this phase installs external packages" trigger condition, which does not apply here.

## Architecture Patterns

### System Architecture Diagram

```
Angular SPA (localhost:4200, post-Phase-1 upgrade)
   │  HTTP request, Authorization: Bearer <jwt>, Content-Type: application/json
   ▼
┌─────────────────────────────────────────────────────────────────────┐
│ ASP.NET Core 8 Middleware Pipeline (Program.cs)                     │
│                                                                       │
│  1. UseHttpsRedirection()                                            │
│  2. [NEW] CorsRejectionLoggingMiddleware  ── inspects Origin header  │
│     (custom, before UseCors — logs mismatch, does not block)         │
│  3. UseCors("AllowAngular")  ── WithOrigins(App:FrontendUrl),        │
│     WithHeaders(Authorization, Content-Type), WithMethods(GET,POST,  │
│     PUT,DELETE) — omits CORS headers if Origin doesn't match         │
│         │                                                             │
│         │ (preflight OPTIONS short-circuits here with 204,          │
│         │  no CORS headers if origin mismatched → browser blocks)   │
│         ▼                                                             │
│  4. [REORDERED] UseAuthentication()  ── validates JWT, populates    │
│     HttpContext.User from Bearer token (Issuer/Audience/Lifetime/   │
│     Algorithm all checked here)                                      │
│         │                                                             │
│         ▼                                                             │
│  5. [REORDERED] UseRateLimiter()  ── partition key now CAN read      │
│     HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)       │
│     since UseAuthentication already ran (D-11 depends on this order)│
│     "auth" policy (unpartitioned, login/register only)               │
│     "mutations" policy (NEW, partitioned by user id, 100/hr)         │
│     OnRejected → logs {UserId|IP}, {Endpoint}, {PolicyName} (D-15)   │
│         │                                                             │
│         ▼                                                             │
│  6. UseAuthorization()  ── [Authorize] attribute checks              │
│         │                                                             │
│         ▼                                                             │
│  7. MapControllers()                                                 │
│     ├─ AuthController.Login → AuthService.LoginAsync                 │
│     │    └─ [NEW] logger.LogWarning on failed login (D-14)           │
│     ├─ GamesController (Create/Update/Delete) [EnableRateLimiting]   │
│     ├─ GameJournalController (Create/Update/Delete) [EnableRateLimiting]│
│     └─ SessionController (Recommend/Plan) [EnableRateLimiting]       │
│          └─ SessionManager.GetRecommendationAsync                    │
│               └─ [FIX] add .Where(g => g.UserId == userId) (folded todo)│
└─────────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure

No new files/folders needed — every change lands in an existing file:

```
GamePlanner.API/
├── Program.cs                    # reordered pipeline, CORS config-source swap,
│                                  #   ValidAlgorithms/RequireExpirationTime, new
│                                  #   "mutations" rate-limit policy, OnRejected,
│                                  #   new CorsRejectionLoggingMiddleware registration
├── Middleware/                   # (NEW folder, one small file)
│   └── CorsRejectionLoggingMiddleware.cs
├── Services/
│   └── AuthService.cs            # add ILogger<AuthService>, 24h lifetime,
│                                  #   LogWarning on both failed-login branches
├── Controllers/
│   ├── GamesController.cs        # add [EnableRateLimiting("mutations")] to
│                                  #   Create/CreateFromIgdb/Update/Delete
│   ├── GameJournalController.cs  # add [EnableRateLimiting("mutations")] to
│                                  #   CreateEntry/UpdateEntry/DeleteEntry
│   └── SessionController.cs      # add [EnableRateLimiting("mutations")] to
│                                  #   Recommend/Plan; add GetUserId() + pass to manager
└── Managers/
    └── SessionManager.cs         # add .Where(g => g.UserId == userId) filter
```

### Pattern 1: CORS rejection logging (D-16 — primary recommendation)

**What:** A tiny custom middleware registered *before* `app.UseCors(...)` that inspects the incoming `Origin` header and logs a warning if it's present but doesn't match the configured frontend origin. It never blocks the request — `UseCors()` still runs afterward and is solely responsible for the actual CORS header decision. This is purely observational.

**When to use:** Single-origin CORS setups (matches D-05's single-origin decision) where the "is this origin allowed" check is a simple string comparison, not a complex per-endpoint policy evaluation.

**Why this is the correct place in the pipeline:** ASP.NET Core CORS middleware has no `OnRejected`-style hook. For an actual (non-preflight) cross-origin request with a disallowed origin, the middleware does not fail or short-circuit the request — it simply omits the `Access-Control-Allow-Origin` response header, and the *browser* (client-side) blocks the frontend JS from reading the response. For a preflight `OPTIONS` request, the middleware terminates the pipeline early with `204 No Content` and, if the origin isn't allowed, without CORS headers — again, no server-side "error" occurs `[VERIFIED: learn.microsoft.com/aspnet/core/security/cors]`. Placing a lightweight check immediately *before* `UseCors()` is the only way to observe "this request's Origin doesn't match our policy" as a distinct loggable server-side event, matching D-16's explicit direction.

**Example:**
```csharp
// Source: pattern synthesized from official ASP.NET Core CORS docs
// (learn.microsoft.com/aspnet/core/security/cors) — no direct Microsoft sample
// exists for this exact logging use case; this is standard ASP.NET Core custom
// middleware shape [CITED: learn.microsoft.com/aspnet/core/security/cors]
namespace GamePlanner.API.Middleware;

public class CorsRejectionLoggingMiddleware(RequestDelegate next, ILogger<CorsRejectionLoggingMiddleware> logger, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        var allowedOrigin = config["App:FrontendUrl"];

        if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, allowedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "CORS rejection: Origin {Origin} does not match configured {AllowedOrigin} for {Method} {Path}",
                origin, allowedOrigin, context.Request.Method, context.Request.Path);
        }

        await next(context);
    }
}

// Program.cs — registered BEFORE app.UseCors(...)
app.UseHttpsRedirection();
app.UseMiddleware<GamePlanner.API.Middleware.CorsRejectionLoggingMiddleware>();
app.UseCors("AllowAngular");
```

**Caveat (must be handled, not just noted):** Only log when the `Origin` header is actually present. Same-origin requests, server-to-server calls, curl/Postman/Swagger UI requests typically don't send an `Origin` header at all — logging every request without one would produce false-positive noise, not a genuine CORS rejection signal.

### Pattern 1 (fallback): `ICorsService` decorator (if the simple middleware proves insufficient)

**What:** Wrap the DI-registered `ICorsService` so every `EvaluatePolicy` call is inspected for `CorsResult.IsOriginAllowed == false`, which is confirmed to be the exact signal ASP.NET Core itself uses internally to decide whether to write CORS headers `[VERIFIED: learn.microsoft.com/dotnet/api/microsoft.aspnetcore.cors.infrastructure.corsresult — "IsOriginAllowed: When false, no CORS headers should be sent"]`.

**When to use:** Only if the primary pattern's simple string-comparison approach diverges from actual CORS evaluation (e.g., if a future phase introduces per-endpoint `[EnableCors]` policies with different allowed origins) — not needed for this phase's single-policy, single-origin setup.

```csharp
// Source: pattern synthesized from CorsResult API reference + standard DI
// last-registration-wins override behavior [ASSUMED — exact registration-order
// interaction with AddCors()'s internal TryAddSingleton not verified against
// ASP.NET Core CORS source in this session; ground with a quick build+manual
// test before relying on it, or use Pattern 1 primary]
builder.Services.AddCors(options => { /* ... */ });

builder.Services.AddSingleton<ICorsService>(sp =>
{
    var inner = new CorsService(
        sp.GetRequiredService<IOptions<CorsOptions>>(),
        sp.GetRequiredService<ILoggerFactory>());
    return new LoggingCorsService(inner, sp.GetRequiredService<ILogger<Program>>());
});

public class LoggingCorsService(ICorsService inner, ILogger logger) : ICorsService
{
    public CorsResult EvaluatePolicy(HttpContext context, CorsPolicy policy)
    {
        var result = inner.EvaluatePolicy(context, policy);
        if (!result.IsOriginAllowed && context.Request.Headers.ContainsKey("Origin"))
            logger.LogWarning("CORS rejection: Origin {Origin} rejected for {Path}",
                context.Request.Headers.Origin.ToString(), context.Request.Path);
        return result;
    }

    public void ApplyResult(CorsResult result, HttpResponse response) => inner.ApplyResult(result, response);
}
```

### Pattern 2: Per-user partitioned rate limiting (NET-03 / D-09 through D-12)

**What:** A named policy registered with `AddPolicy<string>`, not `AddFixedWindowLimiter`, so the partition key can be derived per-request from `HttpContext.User`.

**When to use:** Whenever a rate limit must be fair per-authenticated-user rather than a single shared bucket for the whole endpoint (which is what `AddFixedWindowLimiter(policyName, options)` gives you today for the `"auth"` policy).

**Example:**
```csharp
// Source: learn.microsoft.com/aspnet/core/performance/rate-limit (aspnetcore-8.0)
// [VERIFIED: learn.microsoft.com/aspnet/core/performance/rate-limit]
using System.Security.Claims;
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o => { /* unchanged, existing */ });

    options.AddPolicy<string>("mutations", httpContext =>
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromHours(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, ct) =>
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
                      ?? "unknown";
        logger.LogWarning(
            "Rate limit rejected: {UserOrIp} on {Path} (policy unknown from context; log endpoint/path instead)",
            userId, context.HttpContext.Request.Path);
        return ValueTask.CompletedTask;
    };
});
```

**Applying the policy — unchanged pattern, already used for `"auth"`:**
```csharp
[HttpPost]
[EnableRateLimiting("mutations")]
public async Task<IActionResult> Create([FromBody] CreateGameRequest request) { /* ... */ }
```

### Pattern 3: JWT hardening (AUTH-01/AUTH-02 / D-01, D-04)

```csharp
// Source: existing Program.cs (grounded), extended per D-04
// [VERIFIED: learn.microsoft.com/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.validalgorithms
//  — "If set to a non-empty collection, only the algorithms listed will be considered valid."]
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer           = true,
    ValidateAudience         = true,
    ValidateLifetime         = true,
    ValidateIssuerSigningKey = true,
    RequireExpirationTime    = true,                                  // D-04, new
    ValidAlgorithms          = [SecurityAlgorithms.HmacSha256],        // D-04, new
    ValidIssuer              = jwt["Issuer"],
    ValidAudience            = jwt["Audience"],
    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!))
    // ClockSkew intentionally left at library default (5 min) per D-03
};
```

```csharp
// AuthService.cs — D-01, D-14
// Source: existing AuthService.cs, grounded read
public class AuthService(GamePlannerDbContext db, IConfiguration config, ILogger<AuthService> logger)
{
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.IsActive);

        if (user is null || user.PasswordHash is null)
        {
            logger.LogWarning("Failed login attempt for {Email}: user not found or inactive", request.Email);
            return null;
        }
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for {Email}: invalid password", request.Email);
            return null;
        }
        // ... unchanged
    }

    public string GenerateToken(User user)
    {
        // ...
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims.ToArray(),
            expires: DateTime.UtcNow.AddHours(24),   // D-01, was AddDays(7)
            signingCredentials: creds);
        // ...
    }
}
```

### Pattern 4: CORS origin/header/method lockdown (NET-01/NET-02 / D-05 through D-07)

```csharp
// Source: existing Program.cs, extended per D-05/D-06/D-07
// [VERIFIED: learn.microsoft.com/aspnet/core/security/cors — WithOrigins/WithHeaders/WithMethods syntax]
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(builder.Configuration["App:FrontendUrl"]!)   // D-05, was hardcoded literal
              .WithHeaders("Authorization", "Content-Type")             // D-06, was AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE"));            // D-07, was AllowAnyMethod()
});
```

### Anti-Patterns to Avoid
- **Registering `UseRateLimiter()` before `UseAuthentication()`/`UseAuthorization()` when any rate-limit policy needs claim-based partitioning:** this is the exact bug in the *current* `Program.cs` — it works today only because the existing `"auth"` policy doesn't read `HttpContext.User` at all. Adding a user-partitioned policy without reordering the pipeline will silently degrade to "everyone shares one partition," defeating D-11.
- **Using `AddFixedWindowLimiter(policyName, options)` when per-user partitioning is required:** this overload has no partition-key parameter; it is a single shared limiter for all callers under that policy name. Use `AddPolicy<TKey>(policyName, partitioner)` instead.
- **Logging every request without an `Origin` header as a CORS rejection:** produces noise, not signal. Only log when `Origin` is present and mismatched.
- **Enabling `AllowCredentials()` alongside a CORS policy:** not needed and not requested — this API is bearer-token authenticated via the `Authorization` header, not cookie-based, so `AllowCredentials` should not be added (matches the existing Out-of-Scope note that CSRF middleware doesn't apply here either).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Rate limiting counters/windows | Custom in-memory counter dictionary keyed by user id | `System.Threading.RateLimiting` `AddPolicy`/`RateLimitPartition.GetFixedWindowLimiter` | Already in-box, thread-safe, handles window rollover and queueing correctly; a hand-rolled counter risks race conditions under concurrent requests |
| JWT signature/claims validation | Manual token parsing/claim checks | `TokenValidationParameters` + `JwtBearerDefaults.AuthenticationScheme` (already in place) | Hand-rolled validation is a classic source of algorithm-confusion and expiry-bypass vulnerabilities; the built-in validator with `ValidAlgorithms` pinned closes exactly this class of bug |
| CORS origin/header/method enforcement | Custom middleware writing `Access-Control-*` headers manually | Built-in `AddCors`/`UseCors` (already in place, just being reconfigured) | The built-in middleware correctly handles preflight semantics, `Vary: Origin`, and OPTIONS short-circuiting — hand-rolling this is a common source of subtle CORS bypass bugs |

**Key insight:** This entire phase is a *reconfiguration* exercise, not new engineering — every mechanism needed already ships in ASP.NET Core 8's shared framework and is already partially used in this codebase. The only genuinely new code is (1) the ~15-line CORS-rejection-logging middleware (D-16, no built-in equivalent exists) and (2) the `.Where(g => g.UserId == userId)` one-line fix in `SessionManager.cs` (folded todo).

## Common Pitfalls

### Pitfall 1: `AddFixedWindowLimiter` does not partition by key
**What goes wrong:** Registering the new mutation-endpoint policy with `options.AddFixedWindowLimiter("mutations", o => {...})` (mirroring the `"auth"` policy's exact syntax) compiles fine and appears to work in casual testing, but every user shares the same 100-requests/hour bucket — one user's heavy backlog editing session rate-limits every other user simultaneously.
**Why it happens:** `AddFixedWindowLimiter(policyName, configureOptions)` is a convenience overload with no partition-key parameter; internally it uses a single fixed partition for all requests under that policy name.
**How to avoid:** Use `options.AddPolicy<string>("mutations", partitionerFunc)` where `partitionerFunc` returns `RateLimitPartition.GetFixedWindowLimiter(userId, ...)`, as shown in Pattern 2 above.
**Warning signs:** During manual testing, one authenticated user's requests being rejected causes a *different* user's requests to also start returning 429 — this is the tell that partitioning isn't actually happening.

### Pitfall 2: Middleware pipeline order breaks claim-based rate limiting (the critical grounding finding)
**What goes wrong:** The current `Program.cs` pipeline is `UseCors → UseRateLimiter → UseAuthentication → UseAuthorization`. If the new `"mutations"` policy's partitioner reads `httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` at this point in the pipeline, `HttpContext.User` has not yet been populated by JWT validation — it will be an anonymous/unauthenticated principal, and the partition key will always resolve to the fallback value (e.g., `"anonymous"`), collapsing all users into one partition.
**Why it happens:** `UseAuthentication()` is what parses the `Authorization: Bearer <jwt>` header and sets `HttpContext.User`. Any middleware running before it sees an empty/anonymous principal, regardless of what the client actually sent.
**How to avoid:** Reorder the pipeline to `UseCors → UseAuthentication → UseRateLimiter → UseAuthorization → MapControllers`, matching the order used in Microsoft's own official sample project for this exact scenario `[VERIFIED: github.com/dotnet/AspNetCore.Docs.Samples — fundamentals/middleware/rate-limit/WebRateLimitAuth/Program.cs, "important to add [UseRateLimiter] after UseAuthentication because the limiter uses auth info"]`.
**Warning signs:** Same as Pitfall 1 — cross-user partition bleed during manual verification of ROADMAP success criterion 5.

### Pitfall 3: CORS-rejection logging noise from non-browser requests
**What goes wrong:** A pre-`UseCors()` middleware that logs on *any* Origin mismatch, without checking that an `Origin` header is present at all, generates a warning log line for every Swagger UI call, curl/Postman test, or server-to-server request — none of which are actual CORS rejections (browsers are the only clients that send `Origin` and enforce CORS).
**Why it happens:** The middleware pattern in D-16 is a manual string comparison, not the actual CORS evaluation logic — it's easy to skip the "is there even an Origin header" guard.
**How to avoid:** Only log when `context.Request.Headers.Origin` is non-empty, as shown in Pattern 1.
**Warning signs:** Log volume spikes during normal Swagger-UI-driven manual testing sessions with no actual cross-origin browser traffic involved.

### Pitfall 4: Shortened JWT lifetime interacting with the folded `SessionManager` fix during manual verification
**What goes wrong:** Not a functional bug, but a testing-sequencing trap: if the manual full-browser CORS walkthrough (D-08) and the 24-hour JWT lifetime change (D-01) are tested far apart in time, a stale token could expire mid-walkthrough, producing a 401 that looks like a CORS or rate-limit issue but is actually just an expired-session redirect (already handled correctly by `AuthInterceptor`, per D-02).
**Why it happens:** 24 hours is long enough that this is unlikely within a single session, but worth flagging since the plan's verification steps will span JWT, CORS, and rate-limiting checks in sequence.
**How to avoid:** Log in fresh immediately before starting the CORS/rate-limit manual walkthrough, don't reuse a token from a much earlier session.
**Warning signs:** Unexpected redirect to `/login` mid-walkthrough with no rate-limit or CORS-related console error.

### Pitfall 5: `SessionController` currently has no `GetUserId()` helper at all
**What goes wrong:** The folded todo's fix location is `SessionManager.cs:15`, but `SessionManager.GetRecommendationAsync(SessionRequest request)` doesn't currently receive a `userId` parameter, and `SessionController.Recommend` doesn't call any `GetUserId()`-equivalent method (unlike `GamesController`/`GameJournalController`, which both have one). Fixing only `SessionManager.cs` without also updating `SessionController.cs` and the manager's method signature leaves the bug in place.
**Why it happens:** The bug is a genuine gap in the original implementation — `SessionController` was never wired to extract the user id in the first place, unlike its sibling controllers.
**How to avoid:** The plan must include: (1) add a `GetUserId()` helper to `SessionController` (matching the existing pattern in `GamesController`/`GameJournalController`), (2) add a `userId` parameter to `SessionManager.GetRecommendationAsync`, (3) add `.Where(g => g.UserId == userId)` (or equivalent) to the LINQ query at line 15.
**Warning signs:** If the plan only diffs `SessionManager.cs`, this is incomplete — check `SessionController.cs` is also touched.

## Code Examples

Already embedded above in `## Architecture Patterns` (Patterns 1–4), since each is directly tied to a specific locked decision. No separate section needed — see there for: CORS-rejection middleware, `ICorsService` decorator fallback, `AddPolicy` per-user rate limiter with `OnRejected`, JWT `TokenValidationParameters` hardening, `AuthService` logging, CORS `WithOrigins`/`WithHeaders`/`WithMethods`.

## State of the Art

| Old Approach (current `Program.cs`/`AuthService.cs`) | Current/Recommended Approach | When Changed | Impact |
|--------------------------------------------------------|------------------------------|---------------|--------|
| `DateTime.UtcNow.AddDays(7)` JWT lifetime | `DateTime.UtcNow.AddHours(24)` | This phase (D-01) | Reduces stolen-token exploitation window from 7 days to 1 day |
| `TokenValidationParameters` without `ValidAlgorithms`/`RequireExpirationTime` | Both explicitly set | This phase (D-04) | Defense-in-depth against algorithm-confusion attacks; `RequireExpirationTime` is already `true` by default in the library, but explicit is per AUTH-02's "explicitly reviewed and configured" wording |
| Hardcoded `"http://localhost:4200"` CORS origin | `builder.Configuration["App:FrontendUrl"]` | This phase (D-05) | Enables environment-based deployment without code changes; no functional change today since the config value is already `"http://localhost:4200"` |
| `AllowAnyHeader()`/`AllowAnyMethod()` | `WithHeaders("Authorization","Content-Type")`/`WithMethods("GET","POST","PUT","DELETE")` | This phase (D-06/D-07) | Closes the overly-permissive CORS surface; verified as a no-functional-change swap against actual frontend code |
| `AddFixedWindowLimiter("auth", ...)` only, no mutation-endpoint coverage | New `AddPolicy<string>("mutations", ...)`, partitioned by user id | This phase (D-09 through D-12) | Extends abuse protection to write-path endpoints, fairly partitioned per user |
| `UseCors → UseRateLimiter → UseAuthentication → UseAuthorization` | `UseCors → UseAuthentication → UseRateLimiter → UseAuthorization` | This phase (required by D-11, not separately decided but architecturally necessary) | Without this reorder, D-11's per-user partitioning silently does not work |
| No `ILogger` in `AuthService`/`AuthController` | `ILogger<AuthService>` injected, `LogWarning` on failed login | This phase (D-14) | First security-relevant logging in the auth path |
| No CORS rejection visibility | Pre-`UseCors()` middleware logging Origin mismatches | This phase (D-16) | Closes OBS-01's CORS-rejection-visibility gap; the only genuinely new abstraction this phase introduces |

**Deprecated/outdated:** None — this phase doesn't touch any deprecated API. All primitives used (`TokenValidationParameters`, `AddCors`, `System.Threading.RateLimiting`) are the current, non-deprecated ASP.NET Core 8 APIs, confirmed current as of the aspnetcore-8.0 doc moniker fetched during this research.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The `ICorsService` decorator fallback pattern (Pattern 1, fallback) will correctly override the DI-registered `ICorsService` via last-registration-wins semantics, without needing a decoration library (e.g., Scrutor) | Architecture Patterns → Pattern 1 (fallback) | Low — this is documented as a fallback only, not the primary recommendation; if wrong, the primary pre-`UseCors()` middleware pattern (fully verified) is unaffected and remains the recommended approach |
| A2 | No Phase 1 (Angular EOL upgrade) artifact recorded any new custom outgoing request headers beyond `Authorization`; confirmed by reading `auth.interceptor.ts` directly and the Phase 1 final SUMMARY, but the full frontend codebase wasn't re-scanned line-by-line for every HTTP call site in this research session | User Constraints (D-06/D-07 confirmation) | Low-Medium — if a Phase 1 upgrade hop silently introduced a new header (e.g., via an Angular HttpClient default change), the CORS `WithHeaders` allow-list would need one more entry; the plan should include a final pre-implementation grep across `game-planner-ui/src/app` for `setHeaders`/`HttpHeaders` as a cheap double-check (already done once in this research, found only `Authorization`) |

**If this table is empty:** N/A — two low-risk assumptions logged above; both have low blast radius and are called out with mitigations.

## Open Questions

1. **Exact wording of the `OnRejected` log statement's `{PolicyName}` parameter (D-15)**
   - What we know: `RateLimiterOptions.OnRejected` is a single global callback shared across all policies (`"auth"` and the new `"mutations"` policy) — the callback's `RateLimitLease`/`OnRejectedContext` doesn't directly expose which named policy triggered the rejection.
   - What's unclear: Whether to derive the policy name from `context.HttpContext.Request.Path` pattern-matching (e.g., "was this `/api/auth/*`?") or to skip the `{PolicyName}` field and log only `{UserOrIp}` + `{Endpoint}` (which is sufficient to distinguish the two policies in practice, since `"auth"` only applies to `/api/auth/login|register` and `"mutations"` applies elsewhere).
   - Recommendation: Log `{UserOrIp}` and `{Endpoint}` (path); omit an explicit `{PolicyName}` field unless `context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName` proves easy to pull out (this metadata accessor exists on `EnableRateLimitingAttribute` but wasn't independently verified in this research session — a quick check during planning/implementation is cheap).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | Building/running `GamePlanner.API` (all of this phase's changes) | ✓ | 8.0.420 (also 9.0.317 present) | — |
| SQL Server (local, via SSMS/Trusted_Connection) | `AuthService.LoginAsync`/`RegisterAsync` (needed for manual verification, not for the code changes themselves) | Not probed this session (developer-managed local service, per CLAUDE.md) | — | Developer starts SQL Server before manual verification, per existing project convention |

**Missing dependencies with no fallback:** None — this phase introduces no new external dependency.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | Yes | JWT via `Microsoft.AspNetCore.Authentication.JwtBearer`, `TokenValidationParameters` with Issuer/Audience/Lifetime/Algorithm validation (AUTH-01/AUTH-02) — never hand-roll token parsing |
| V3 Session Management | Yes | JWT lifetime bounded to 24h (D-01); no server-side session store (stateless JWT), consistent with existing architecture; `ClockSkew` left at library default (D-03) |
| V4 Access Control | Yes | Existing `[Authorize]` + per-user `UserId` scoping in controllers; this phase's folded fix (`SessionManager.cs`) closes a V4 gap (missing user-scoping filter, cross-user data leak) |
| V5 Input Validation | Partial | Not a focus of this phase — existing `AuthController` validation (email format, password length) is untouched; no new user input surfaces are introduced |
| V6 Cryptography | Yes | `SymmetricSecurityKey` + `HmacSha256` already used for JWT signing (unchanged); D-04 pins `ValidAlgorithms` to prevent algorithm-confusion/downgrade attacks — a V6-relevant hardening, using the built-in `SecurityAlgorithms` constants, never hand-rolled |
| V13 API and Web Service | Yes | CORS lockdown (NET-01/NET-02) and rate limiting (NET-03) are both direct V13 (API security) controls — restricting allowed origins/headers/methods and bounding request volume per authenticated user |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| JWT algorithm confusion (e.g., `alg: none`, or RS256↔HS256 key-confusion attacks) | Tampering / Spoofing | `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]` pinned explicitly (D-04) — rejects any token not signed with the expected algorithm |
| Long-lived stolen-token replay | Spoofing / Elevation of Privilege | JWT lifetime bounded to 24h (D-01); no refresh-token rotation in this milestone (SEC-01 deferred), so this is the primary mitigation for this specific threat this phase |
| Overly permissive CORS (`AllowAnyHeader`/`AllowAnyMethod`) enabling broader cross-origin attack surface | Tampering / Information Disclosure | Explicit `WithHeaders`/`WithMethods` allow-lists (D-06/D-07), derived from actual frontend usage, not defaults |
| Credential-stuffing / brute-force login attempts | Spoofing / Denial of Service | Existing `"auth"` fixed-window rate limiter (10/min, unpartitioned — pre-existing, unchanged this phase) plus new `LogWarning` on failed logins (D-14) for detection/alerting |
| Single-user abuse of mutation endpoints exhausting shared resources for other users | Denial of Service | Per-user-partitioned `"mutations"` rate-limit policy (D-09 through D-12), requiring the middleware-order fix documented in Pitfall 2 to actually partition correctly |
| Cross-user data leak via missing tenant/user-scoping filter | Information Disclosure / Elevation of Privilege | Folded todo fix: add `.Where(g => g.UserId == userId)` to `SessionManager.GetRecommendationAsync`'s query, matching the `UserId`-scoping pattern already used in every other manager (`GameManager`, `GameJournalManager`) |

## Sources

### Primary (HIGH confidence)
- [Rate limiting middleware in ASP.NET Core (aspnetcore-8.0)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0) — `AddPolicy`, `OnRejected`, `PartitionedRateLimiter`, partitioning-by-user-identity examples, `UseRateLimiter` ordering guidance
- [Enable Cross-Origin Requests (CORS) in ASP.NET Core (aspnetcore-8.0)](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-8.0) — CORS behavior on disallowed origin (headers omitted, not a distinct error), middleware ordering, `WithOrigins`/`WithHeaders`/`WithMethods` syntax
- [CorsResult Class API reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.cors.infrastructure.corsresult?view=aspnetcore-8.0) — confirmed `IsOriginAllowed` property exists, "When false, no CORS headers should be sent"
- [TokenValidationParameters.ValidAlgorithms Property API reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.tokens.tokenvalidationparameters.validalgorithms) — confirmed `IEnumerable<string>`, default `null`, non-empty collection restricts accepted algorithms
- [dotnet/AspNetCore.Docs.Samples — WebRateLimitAuth/Program.cs](https://github.com/dotnet/AspNetCore.Docs.Samples/blob/main/fundamentals/middleware/rate-limit/WebRateLimitAuth/Program.cs) — official Microsoft sample confirming `UseAuthentication → UseRateLimiter → UseAuthorization` ordering for claim-based partition keys
- `GamePlanner.API/Program.cs`, `GamePlanner.API/Services/AuthService.cs`, `GamePlanner.API/Controllers/{Auth,Games,GameJournal,Session}Controller.cs`, `GamePlanner.API/Managers/SessionManager.cs`, `GamePlanner.API/appsettings.json`, `GamePlanner.API/appsettings.Development.json`, `GamePlanner.API/GamePlanner.API.csproj`, `game-planner-ui/src/app/interceptors/auth.interceptor.ts`, `game-planner-ui/src/environments/environment.ts` — all read directly in this research session to ground every recommendation in actual current code

### Secondary (MEDIUM confidence)
- [andrewlock.net — A deep dive into the ASP.NET Core CORS library](https://andrewlock.net/a-deep-dive-in-to-the-asp-net-core-cors-library/) — background on `ICorsService`/`ICorsPolicyProvider` architecture, used to construct the fallback decorator pattern (not the primary recommendation)
- `.planning/phases/01-angular-eol-upgrade/01-06-SUMMARY.md` — confirms Phase 1 complete, UAT walkthrough approved, and both pre-existing backend findings (`SessionManager` leak, IGDBId upsert issue) explicitly filed as out-of-scope-for-Phase-1 todos, with the `SessionManager` one now folded into this phase

### Tertiary (LOW confidence)
- General WebSearch results on rate-limiting/CORS/JWT blog posts (codewithmukesh.com, dotnet-guide.com, csharp-coder.com, etc.) — used only to cross-check terminology and confirm the official docs' framing was consistent with community practice; no claim in this document rests solely on a tertiary source

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages, every API confirmed present in the existing `.csproj` or via official Microsoft Learn API reference pages
- Architecture (middleware order, rate-limit partitioning): HIGH — the critical pipeline-order finding is confirmed against an official Microsoft sample project, not inferred
- CORS rejection logging (D-16): MEDIUM — the underlying APIs (`CorsResult.IsOriginAllowed`, CORS-omits-headers-not-errors behavior) are HIGH confidence/verified, but the specific "wrap it in a small middleware" implementation shape is a synthesized pattern (no single official Microsoft sample does exactly this), hence MEDIUM overall for this one recommendation
- Pitfalls: HIGH — Pitfalls 1, 2, and 5 are all grounded in direct reads of the actual current `Program.cs`/`SessionController.cs`/`SessionManager.cs`, not assumptions

**Research date:** 2026-08-31
**Valid until:** 30 days (stable ASP.NET Core 8 LTS APIs, no fast-moving dependencies in this phase)
