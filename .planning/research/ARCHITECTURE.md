# Architecture Research

**Domain:** Security hardening of an existing ASP.NET Core 8 + Angular brownfield app
**Researched:** 2026-08-28
**Confidence:** HIGH (component boundaries, data flow — verified by direct codebase reading) / MEDIUM (framework-pattern recommendations — cross-checked against official .NET docs + community sources)

## Standard Architecture

### System Overview (existing, annotated with hardening touch-points)

```text
┌─────────────────────────────────────────────────────────────────────┐
│ Angular SPA (game-planner-ui)                                        │
│   AuthInterceptor → HttpClient → API                                 │
└───────────────────────────────┬───────────────────────────────────────┘
                                 │ HTTP/JWT
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Program.cs (composition root)                                        │
│   ┌─ CORS policy ───────────────────┐  ← (2) env-based CORS lands here│
│   ┌─ AddRateLimiter (policies) ─────┐  ← (3) new policies added here  │
│   ┌─ DI registrations ──────────────┐  ← (5) hosted cleanup service   │
│   └──────────────────────────────────┘    registered here             │
└───────────────────────────────┬───────────────────────────────────────┘
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Controllers  →  Managers  →  Services  →  Database                   │
│                                                                        │
│  GamesController → GameManager → (EF Core reads, Dapper sp_ writes)  │
│                        │   ← (4) connection/transaction fix here      │
│                        │   ← (6) audit fields set here (or in a       │
│                        │       shared EF SaveChanges interceptor)     │
│                                                                        │
│  SyncManagers/Controllers → IgdbService  → Twitch OAuth (HTTP)        │
│                                  │   ← (1) creds move to body here    │
│                           → SteamService → Steam Web API (HTTP)       │
│                                  │   ← (1) creds move to header here  │
│                                                                        │
│  SteamAuthController → SteamAuthService → SteamTokenStore (singleton)│
│                                              ← (5) cleanup timer here │
└─────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities Relevant to This Milestone

| Component | Responsibility | File |
|-----------|----------------|------|
| `Program.cs` | Composition root: DI, CORS, rate limiter, auth, hosted services | `GamePlanner.API/Program.cs` |
| `IgdbService` | Twitch OAuth token acquisition + IGDB v4 queries | `GamePlanner.API/Services/IgdbService.cs` |
| `SteamService` | Steam Web API queries (vanity resolve, player summary, owned games) | `GamePlanner.API/Services/SteamService.cs` |
| `SteamTokenStore` | Singleton in-memory `ConcurrentDictionary` holding short-lived OpenID handoff codes | `GamePlanner.API/Services/SteamTokenStore.cs` |
| `GameManager` | CRUD for `Game` entity — EF Core for reads, ad-hoc `SqlConnection` + Dapper stored procs for writes | `GamePlanner.API/Managers/GameManager.cs` |
| `GamePlannerDbContext` | EF Core context; single logical connection per request/scope | `GamePlanner.API/Context/GamePlannerDbContext.cs` |
| Workers (6x `BackgroundService`) | Existing pattern for periodic background work; template for the new cleanup service | `GamePlanner.API/Workers/*.cs` |

## Per-Change Architecture Placement

### (1) IGDB/Steam credentials: query string → headers/body

**Component boundary touched:** External Integration Layer (`Services/`) only. Zero blast radius into Managers or Controllers — both services already return the same public method signatures (`FetchBatchAsync`, `GetOwnedGamesAsync`, etc.), so this is an internal implementation change.

**Data flow implications:** No change to data flow direction. Only the transport mechanism of the outbound HTTP request to Twitch/Steam changes:
- `IgdbService.GetTokenAsync()`: replace the query-string URL with `client.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string,string>{ ["client_id"]=ClientId, ["client_secret"]=ClientSecret, ["grant_type"]="client_credentials" }))`. This matches Twitch's own OAuth2 client-credentials spec, which accepts form-body POST — no functional risk.
- `SteamService`: Steam's Web API is GET-only and keyed by `?key=`, so it cannot fully move to a body. The equivalent hardening move here is: (a) send the key via a custom header is **not** supported by Steam's API, so the realistic fix is confined to stopping the key from being logged — inject the key as the last step via `HttpRequestMessage` + `UriBuilder.Query` construction that keeps `ApiKey` out of any log/`ToString()` call site, and ensure `ILogger` calls (`logger.LogInformation("Fetching owned games for SteamID: {SteamId}", steamId)`) never interpolate the full URL. Confirm with the team whether "headers/body" for Steam literally means suppressing the URL from logs/exception messages rather than eliminating query params (Steam's API contract requires them). **Flag this as a scoping question for the phase**, since the CONCERNS.md fix approach ("Authorization headers or request bodies per Steam API documentation") is aspirational — Steam's public Web API does not support header-based auth for these endpoints.

**Where it belongs:** Entirely inside `IgdbService.cs` and `SteamService.cs`. No new component needed. Build order: independent of everything else — do first (cheapest, self-contained, no schema/DI shape change).

### (2) Environment-based CORS configuration

**Component boundary touched:** `Program.cs` composition root only (configuration + middleware pipeline). Introduces a read from `IConfiguration`/`appsettings.{Environment}.json`.

**Data flow implications:** None for internal data flow — this only affects which browser origins are allowed to receive `Access-Control-Allow-Origin` on responses. No Manager/Service/Controller code changes.

**Where it belongs:**
```csharp
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(allowedOrigins)
              .WithHeaders("Authorization", "Content-Type")
              .WithMethods("GET", "POST", "PUT", "DELETE"));
});
```
Add `Cors:AllowedOrigins` to `appsettings.Development.json` (`["http://localhost:4200"]`) and `appsettings.Production.json` (real origin, once known). This is a **config-only** change — respects the existing "no code duplication across environments" pattern already used for `ConnectionStrings` and `Jwt` sections. Build order: independent, do anytime; no dependency on other changes.

### (3) Rate limiting extended to data-modification endpoints

**Component boundary touched:** `Program.cs` (policy definitions) + `Controllers/` (attribute placement via `[EnableRateLimiting("policy-name")]` or route-group `.RequireRateLimiting(...)`). No Manager/Service change.

**Data flow implications:** Adds a request-level gate before the request reaches the controller action — enforced by ASP.NET Core's rate limiting middleware (`UseRateLimiter()`, already wired). A rejected request never reaches `GameManager`/`SteamService`, so no downstream component needs to change.

**Design decision:** ASP.NET Core 8's built-in rate limiter middleware only supports **one policy per endpoint** (confirmed against current framework behavior — stacking two simultaneous policies on the same endpoint is not supported out of the box as of .NET 8). Practical approach for this codebase:
- Define a policy per sensitivity tier, e.g. `"auth"` (existing, 10/min, unauthenticated/IP-based), `"write"` (new, per-authenticated-user via `PartitionedRateLimiter` keyed on the JWT `sub`/`userId` claim, e.g. 100/hour), and optionally `"read"` left unlimited.
- Partition writes by user ID (not IP), since this is a JWT-authenticated app and IP partitioning would be meaningless behind NAT/shared IP or would rate-limit unauthenticated `/auth` traffic identically to authenticated users:
```csharp
options.AddPolicy("write", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.User.FindFirst("userId")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
        factory: _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromHours(1) }));
```
- Apply `[EnableRateLimiting("write")]` at the controller level (or per POST/PUT/DELETE action) on `GamesController`, `GameJournalController` (or equivalent), `SessionController` — i.e. every controller whose CONCERNS.md flags as data-modification. GET-only actions/controllers stay unlimited.

**Where it belongs:** Purely additive in `Program.cs` + controller attributes. Build order: do **after** (2) CORS since both touch `Program.cs`'s middleware section and are easiest to review together in one PR, but there's no hard technical dependency.

### (4) SqlConnection lifecycle fix in GameManager (Dapper + EF mixed)

**Component boundary touched:** `GameManager` only (Business Logic Layer). This is the most structurally significant change of the six.

**Current behavior (verified by reading `GameManager.cs`):** `CreateAsync`/`CreateFromIgdbAsync`/`UpdateAsync`/`DeleteAsync` each open a **separate, independent `SqlConnection`** (via `CreateConnection()`) to execute a stored procedure (`sp_InsertGame`/`sp_UpdateGame`/`sp_DeleteGame`) via Dapper, then immediately go back to `db` (the injected `GamePlannerDbContext`, a second independent connection) to load/mutate the same row and call `db.SaveChangesAsync()`. The `using var conn` does dispose the connection correctly (no literal handle leak per se, and Dapper auto-opens closed connections) — the real defect is **transactional, not disposal**: the stored-proc write and the EF `SaveChangesAsync()` write are two separate, non-atomic operations against two separate connections. If `SaveChangesAsync()` throws after `sp_InsertGame` already committed, the caller gets an exception but the database is left with a half-created `Game` row (correct `Name`/`EstimatedMinutes`, but no `UserId`, no genres/tags, no `IGDBId`/`CoverImageUrl`) — a data-integrity bug, not just a pooled-connection cost.

**Fix approach, respecting the CLAUDE.md convention ("stored procedures only for insert/update/delete, EF LINQ for reads")** — two structurally valid options, pick one for consistency:
- **Option A (minimal, preferred for this milestone):** Reuse the DbContext's own underlying connection for the Dapper call instead of opening a second one, and wrap both operations in a single `IDbContextTransaction`:
```csharp
await using var tx = await db.Database.BeginTransactionAsync();
var conn = db.Database.GetDbConnection(); // reuse EF's connection
var newId = await conn.ExecuteScalarAsync<int>("sp_InsertGame", ..., transaction: (SqlTransaction)tx.GetDbTransaction(), commandType: CommandType.StoredProcedure);
// ... EF mutations ...
await db.SaveChangesAsync();
await tx.CommitAsync();
```
  This keeps the existing stored-proc-for-writes convention intact, removes the second physical connection, and makes the whole Create/Update/Delete path atomic.
- **Option B (larger refactor, out of scope for a hardening-only milestone):** Introduce a dedicated Dapper-repository abstraction that all Managers share. Rejected for this milestone per PROJECT.md's "hardening only, no new abstractions" constraint — flag as a candidate for a future milestone (also echoed in CONCERNS.md's "Mixed Data Access Patterns" recommendation).

**Data flow implications:** No change to callers (`GamesController`) — method signatures unchanged. Internal to `GameManager`; the only new dependency is `Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction`, already part of the referenced EF Core package (no new NuGet package).

**Where it belongs:** `GameManager.cs` only. Build order: this should be planned as its **own phase**, separate from (1)/(2)/(3), because it changes write-path transaction semantics and needs its own targeted tests (per PROJECT.md's testing requirement — "GameManager" is explicitly named). Do after the credential and CORS/rate-limit changes (which are low-risk and independent) so a regression here is easier to isolate in review/testing.

### (5) SteamTokenStore cleanup mechanism

**Component boundary touched:** New addition to the Background Processing Layer (`Workers/`), consuming the existing `Services/SteamTokenStore.cs` singleton. Structurally identical in shape to the six existing `BackgroundService` workers already in the codebase — this is the cleanest "fits the existing pattern" change of the six.

**Data flow implications:** Purely internal in-process cleanup; no HTTP, no DB. The new worker reads/mutates the same `ConcurrentDictionary` that `SteamAuthService`/`SteamAuthController` already write to (`Store`) and read from (`Redeem`). No new coupling between layers — the worker depends only on the existing `SteamTokenStore` singleton, injected the same way workers already inject scoped/singleton dependencies via `IServiceScopeFactory` or direct constructor injection (since `SteamTokenStore` is itself a singleton, direct constructor injection into the hosted service is simpler than scope-factory indirection used for scoped Managers).

**Design decision:** Add a `RemoveExpired()` method to `SteamTokenStore` itself (keeps expiry logic colocated with the data it prunes) and a thin new `SteamTokenCleanupWorker : BackgroundService` that calls it periodically:
```csharp
// SteamTokenStore.cs — add:
public void RemoveExpired()
{
    var now = DateTime.UtcNow;
    foreach (var kvp in _store)
        if (kvp.Value.Expiry < now)
            _store.TryRemove(kvp.Key, out _);
}
```
```csharp
// Workers/SteamTokenCleanupWorker.cs — new, follows existing worker shape:
public class SteamTokenCleanupWorker(SteamTokenStore store, ILogger<SteamTokenCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(ct))
            store.RemoveExpired();
    }
}
```
Register with `builder.Services.AddHostedService<SteamTokenCleanupWorker>();` next to the other five `AddHostedService` calls in `Program.cs`. Given the store entries expire after 5 minutes (`AddMinutes(5)`), a 10-minute sweep interval is sufficient and matches CONCERNS.md's suggested cadence.

**Where it belongs:** New file in `Workers/`, small addition to `Services/SteamTokenStore.cs`, one line in `Program.cs`. No `WorkerStatusManager` integration needed (that table tracks user-facing sync jobs; this is an internal housekeeping task with no state worth surfacing). Build order: fully independent — no dependency on any other change; can be built in parallel with (1)/(2)/(3).

### (6) Minimal audit fields (who/when) on soft-deleted/modified entities

**Component boundary touched:** Data Access Layer (`Entities/`) for new columns, Database (`Database/` numbered SQL scripts, per project convention — no EF migrations) for schema, and either (a) each Manager's write path, or (b) a single cross-cutting `SaveChangesInterceptor` on `GamePlannerDbContext` for the "when" fields.

**Scope reality check against this codebase:** Every relevant entity (`Game`, likely `GameJournalEntry`, `NowPlayingGame`) already carries a `UserId` FK — in a single-user-per-row personal app, "who modified this" is almost always identical to "who owns this row" (there's no admin/shared-editing concept here). So the **highest-value, lowest-risk** interpretation of "audit fields" for this milestone is:
- `ModifiedAt` (DateTime, always updated on any mutating operation) — genuinely new information, not derivable from `UserId`.
- `DeletedAt` (DateTime?, set only on soft-delete, i.e. when `IsActive` flips to `false`) — directly addresses CONCERNS.md's "No Soft Delete Audit Trail" finding and is the most concrete ask in PROJECT.md ("at least on soft-delete (`IsActive`) paths").
- `ModifiedBy`/`DeletedBy` (int?, `UserId`) are lower priority since they'd duplicate the existing `UserId` column in this single-tenant-per-row model — include only if a future multi-user-per-resource or admin-override path is anticipated; otherwise, skip to avoid redundant columns (respects CLAUDE.md's "no unnecessary abstractions/features beyond what's requested").

**Design decision — where the "when" gets set:**
- **Option A (per-Manager, minimal footprint, consistent with current explicit style):** Each Manager sets `ModifiedAt`/`DeletedAt` explicitly right before calling the stored procedure / `SaveChangesAsync()`, e.g. in `GameManager.UpdateAsync`/`DeleteAsync`. Matches this codebase's existing style (explicit, no hidden magic — see CLAUDE.md "no unnecessary abstractions"). Con: must remember to add it to every Manager touched (`GameManager`, `GameJournalManager`, `NowPlayingManager`, etc.) — easy to miss one.
- **Option B (cross-cutting `SaveChangesInterceptor` on `GamePlannerDbContext`):** A single interceptor inspects `ChangeTracker.Entries()` for entities implementing a shared `IAuditable` marker interface (`ModifiedAt`, and `DeletedAt` set when `IsActive` transitions to `false`) and stamps them automatically on every `SaveChangesAsync()`, regardless of which Manager triggered it. This is the standard, current EF Core 8 pattern for this exact problem (confirmed against multiple current EF Core interceptor guides) and scales better as more Managers/entities are touched, but only covers the EF Core write path — **it does not cover the Dapper stored-procedure write path** (`sp_UpdateGame`, `sp_DeleteGame`), which is exactly where `GameManager`'s writes currently happen. Given (4) already restructures `GameManager`'s writes, the two changes should be sequenced together: if (4) keeps the stored-proc calls, add `ModifiedAt`/`DeletedAt` as explicit `@Param` values passed into `sp_UpdateGame`/`sp_DeleteGame` (SQL-side `SET ModifiedAt = GETUTCDATE()` in the stored proc body, matching the existing `sp_` convention); reserve the EF interceptor only for entities that are fully EF-write (if any exist outside `GameManager`).

**Recommendation for this codebase:** Given only `GameManager`'s writes go through stored procs and most other Managers appear to write via EF Core directly, use a **hybrid**: add `ModifiedAt`/`DeletedAt` columns via a new numbered SQL script (`Database/35_AddAuditFields.sql`, updating `sp_UpdateGame`/`sp_DeleteGame` to stamp them), and add a lightweight `SaveChangesInterceptor` registered in `Program.cs` (`optionsBuilder.AddInterceptors(new AuditInterceptor())`) for all other EF-only write paths. This keeps both existing conventions (stored procs for `Game` writes, EF LINQ elsewhere) intact rather than forcing one mechanism project-wide.

**Data flow implications:** Additive columns only — no breaking change to `GameDto`/`.ToDto()` unless the frontend needs to display `ModifiedAt`. Mapping extensions (`GamePlanner.API/Mapping/`) may need one new line each; not a structural change.

**Where it belongs:** `Entities/Game.cs` (+ other soft-deletable entities), a new `Database/35_...sql` script, `GamePlannerDbContext` (interceptor registration), and touched Managers. Build order: do **last**, after (4), since both touch `GameManager`'s write path and stacking them without (4) done first would mean re-touching the same methods twice.

## Suggested Build Order (dependency-driven)

```
Phase A (independent, low-risk, do first — any order within):
  (1) Credential transport fix (IgdbService, SteamService)
  (2) Environment-based CORS
  (5) SteamTokenStore cleanup worker

Phase B (Program.cs middleware, natural pairing with Phase A but touches
         shared file — sequence to avoid merge conflicts):
  (3) Rate limiting on data-modification endpoints

Phase C (structural — GameManager write path, needs its own test coverage):
  (4) SqlConnection/transaction fix in GameManager

Phase D (depends on C — same write path, avoid double-touching):
  (6) Audit fields (ModifiedAt/DeletedAt), stored-proc updates + interceptor
```

**Rationale:** (1), (2), (5) have zero shared surface with each other or with (3)/(4)/(6) — safe to parallelize across phases/PRs. (3) is grouped after A because it edits the same `Program.cs` region as (2) and benefits from being reviewed together, though it has no hard code dependency. (4) is isolated as its own phase because it changes transactional behavior in a Manager that (6) also needs to touch — sequencing (6) after (4) avoids editing `GameManager.UpdateAsync`/`DeleteAsync` twice and avoids audit-field-setting code being written against a write path that's about to be restructured.

## Anti-Patterns to Avoid

### Anti-Pattern: Introducing a generic repository/UoW abstraction to "fix" the Dapper/EF mix

**What people do:** Treat the SqlConnection lifecycle issue as an excuse to build a full repository pattern or Unit-of-Work abstraction across all Managers.
**Why it's wrong:** Violates CLAUDE.md's "no unnecessary abstractions... beyond what's requested" and PROJECT.md's explicit "hardening-only, no feature work" scope. It also isn't necessary — `GameManager` is the only Manager the audit flagged.
**Do this instead:** Fix `GameManager` specifically (transaction wrapping around its existing Dapper calls), leave the mixed-pattern architecture as-is elsewhere; log the broader consolidation as a backlog item for a future milestone (already captured in CONCERNS.md's Medium-priority list).

### Anti-Pattern: Moving Steam API key into a header that Steam's API doesn't accept

**What people do:** Take "move credentials from URL to headers/body" literally for both integrations without checking each provider's actual API contract.
**Why it's wrong:** Steam's public Web API (`ISteamUser`, `IPlayerService`) is unauthenticated-header-agnostic — it only accepts `key` as a query parameter. Attempting a header-based fix here will simply fail at runtime or require reverse-engineering an unsupported flow.
**Do this instead:** For IGDB (Twitch OAuth token endpoint), do the full form-body fix — it's a real OAuth2 client-credentials endpoint. For Steam, treat the fix as "stop leaking the key via logs/exceptions" rather than "eliminate query param" — a narrower, achievable version of the same underlying goal (no plaintext credential exposure in logs/proxies).

### Anti-Pattern: Applying one global rate-limit policy to everything

**What people do:** Reuse the existing `"auth"` fixed-window policy (IP-partitioned, 10/min) for data-modification endpoints too.
**Why it's wrong:** All authenticated traffic from behind the same NAT/proxy would share one bucket, unfairly throttling legitimate multi-request usage (e.g. bulk game import), while `/auth` needs IP-based partitioning specifically because it's pre-authentication.
**Do this instead:** Partition write-endpoint limits by authenticated user ID (JWT claim), not IP, as described in change (3) above.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Twitch OAuth (IGDB token) | `HttpClient.PostAsync` with `FormUrlEncodedContent` body | Standard OAuth2 client-credentials grant; verified against Twitch/OAuth2 spec conventions |
| Steam Web API | `HttpClient.GetAsync` with `?key=` query param (unavoidable per Steam's API contract) | Hardening scope here is log/exception hygiene, not transport relocation |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `Program.cs` ↔ `Controllers` | Middleware pipeline (`UseCors`, `UseRateLimiter`) + attributes (`[EnableRateLimiting]`) | No direct code coupling; policies referenced by string name |
| `GameManager` ↔ `GamePlannerDbContext` | Direct constructor injection (scoped) | After fix (4), also owns the `IDbContextTransaction` wrapping its Dapper calls |
| `SteamTokenCleanupWorker` ↔ `SteamTokenStore` | Direct singleton injection | No `IServiceScopeFactory` needed since the store itself is already a singleton |
| `GamePlannerDbContext` ↔ `AuditInterceptor` (new, if adopted) | `DbContextOptionsBuilder.AddInterceptors()` | Applies to EF-only write paths; does not cover Dapper stored-proc writes (those get audit fields via SQL script update instead) |

## Sources

- [Rate limiting middleware in ASP.NET Core — Microsoft Learn (aspnetcore-8.0)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0) — MEDIUM confidence (verified/cross-checked)
- [dotnet/aspnetcore#44907 — single-policy-per-endpoint limitation](https://github.com/dotnet/aspnetcore/issues/44907) — MEDIUM confidence
- [EF Core Interceptors: The Complete Guide](https://codewithmukesh.com/blog/ef-core-interceptors/) and [SaveChangesInterceptor for Auditing Entities in .NET 8](https://mehmetozkaya.medium.com/ef-core-interceptors-savechangesinterceptor-for-auditing-entities-in-net-8-microservices-6923190a03b9) — MEDIUM confidence, cross-checked pattern consensus
- [.NET BackgroundService + PeriodicTimer patterns](https://okyrylchuk.dev/blog/how-to-use-backgroundservice-in-dotnet-for-long-running-tasks/) — MEDIUM confidence
- Direct codebase reads (HIGH confidence, primary source): `GamePlanner.API/Program.cs`, `GamePlanner.API/Services/IgdbService.cs`, `GamePlanner.API/Services/SteamService.cs`, `GamePlanner.API/Services/SteamTokenStore.cs`, `GamePlanner.API/Managers/GameManager.cs`, `GamePlanner.API/Entities/Game.cs`, `GamePlanner.API/Workers/GameHLTBSyncWorker.cs`
- `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/CONCERNS.md`, `.planning/codebase/CONVENTIONS.md`, `.planning/PROJECT.md` — HIGH confidence, curated project sources

---
*Architecture research for: GamePlanner security hardening milestone (brownfield)*
*Researched: 2026-08-28*
