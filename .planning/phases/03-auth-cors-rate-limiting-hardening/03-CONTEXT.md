# Phase 3: Auth, CORS & Rate Limiting Hardening - Context

**Gathered:** 2026-08-31
**Status:** Ready for planning

<domain>
## Phase Boundary

The JWT auth flow, CORS policy, and rate limiting are deliberately configured for the app's real trust boundary, and security-relevant rejections become visible in logs. Confined to `Program.cs` (auth/CORS/rate-limiter composition root), `AuthService.cs`/`AuthController.cs` (token generation, failed-login logging), `appsettings.json`/`appsettings.Development.json` (`Jwt:*`, `App:FrontendUrl` config), and the `GamesController`/`GameJournalController`/`SessionController` mutation endpoints. Requirements: AUTH-01, AUTH-02, NET-01, NET-02, NET-03, OBS-01.

One pending todo — the `SessionManager.cs:15` cross-user data leak — was folded into this phase's scope at the user's explicit request (see Folded Todos below), even though it's an access-control/data-isolation bug rather than a JWT/CORS/rate-limiting/logging concern per se.

</domain>

<decisions>
## Implementation Decisions

### AUTH-01/AUTH-02 — JWT lifetime & TokenValidationParameters
- **D-01:** JWT lifetime reduced from 7 days (`AuthService.cs:71`, `DateTime.UtcNow.AddDays(7)`) to **24 hours**. No refresh-token rotation is in scope this milestone (SEC-01 is deferred to v2) — this is a straight lifetime cut, not a UX redesign.
- **D-02:** Frontend expiry UX stays as-is: `AuthInterceptor` already catches HTTP 401 and calls `auth.logout()` (redirect to `/login`). No new "session expired" message — this remains a backend/auth-config phase, not a frontend UX phase.
- **D-03:** `TokenValidationParameters.ClockSkew` stays at the library default (5 minutes) — absorbs reasonable clock drift on a single-server deployment; tightening it was considered and rejected as unnecessary risk for no real security gain here.
- **D-04:** Explicitly pin `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]` and `RequireExpirationTime = true` on `TokenValidationParameters` (`Program.cs`) as defense-in-depth against algorithm-confusion attacks — satisfies AUTH-02's "explicitly reviewed and configured" wording beyond the Issuer/Audience/Lifetime validation that already exists.

### NET-01/NET-02 — CORS lockdown
- **D-05:** CORS origin loads from the existing (currently unused) `App:FrontendUrl` config key (`appsettings.json`) instead of the hardcoded `"http://localhost:4200"` literal in `Program.cs`. Single-origin only — no comma-separated multi-origin support, since there's no current need for more than one deployed frontend.
- **D-06:** CORS allowed headers restricted to exactly `Authorization` + `Content-Type` — confirmed by scanning `game-planner-ui/src/app` (only `AuthInterceptor` sets a custom header, `Authorization`; POST/PUT bodies use the browser's default `Content-Type`). No other custom headers exist anywhere in the frontend.
- **D-07:** CORS allowed methods restricted to exactly `GET, POST, PUT, DELETE` — confirmed by scanning frontend services; no PATCH or other verbs are used.
- **D-08:** Verification for ROADMAP.md success criterion 4 ("real Angular SPA can still call every endpoint with no CORS errors") is a **full manual browser walkthrough**, not a scripted/curl-based preflight check — matches the criterion's explicit wording.

### NET-03 — Rate limiting on data-mutation endpoints
- **D-09:** Scope is exactly **Games + Journal + Session controllers** (`GamesController`, `GameJournalController`, `SessionController`) POST/PUT/DELETE endpoints — matches NET-03's literal wording and ROADMAP.md success criterion 5. `ProfileController`, `NowPlayingController`, `TrackedGamesController`, `SteamController` (import), `SyncController`/`WorkerController` (admin-ish run triggers) are explicitly **not** in scope for this phase.
- **D-10:** Limit: **100 requests / hour per user** — generous enough for normal backlog/journal/session editing bursts while still bounding abuse; matches the illustrative number in `CONCERNS.md`'s fix-approach note (not previously a locked decision, now is).
- **D-11:** Partition key: the new limiter partitions by **JWT user ID** (the same `NameIdentifier` claim controllers already extract) — required by ROADMAP.md success criterion 5 ("one user's rejections don't affect another user's requests"). This was the only viable mechanism given that requirement, not a genuine multi-option choice.
- **D-12:** One **shared rate-limiter policy** across all in-scope endpoints (not a separate named policy per controller) — mirrors the existing single `"auth"` policy pattern in `Program.cs`; no evidence yet that games/journal/session need different limits from each other.

### OBS-01 — Security event logging
- **D-13:** Mechanism is **structured `ILogger` output only** — no new logging infrastructure (Seq/ELK is explicitly out of scope per `PROJECT.md`). "Central log stream" means the project's existing single configured `Logging` provider (console/file via `appsettings.json` `Logging` section), following the same `ILogger<T>` + named-parameter convention used everywhere else in the codebase.
- **D-14:** Failed logins: `AuthService.LoginAsync` currently has **zero logging** — no `ILogger` is even injected. Add `logger.LogWarning("Failed login attempt for {Email}: {Reason}", ...)` on both the "user not found/inactive" and "wrong password" branches. Never log the password. `AuthService` needs `ILogger<AuthService>` added to its constructor.
- **D-15:** Rate-limit rejections (both the existing `"auth"` limiter and the new games/journal/session mutation limiter) get logged via the standard ASP.NET Core `RateLimiterOptions.OnRejected` callback — one shared handler logging `{UserId or IP}`, `{Endpoint}`, `{PolicyName}` before the 429 response is sent.
- **D-16:** CORS rejections are the one item flagged as **needing research during planning**, not fully locked here: ASP.NET Core's CORS middleware has no built-in rejection hook analogous to `OnRejected` (a blocked preflight just omits CORS headers; the browser enforces the block client-side, so the server doesn't see it as an "error"). The user's direction is to attempt logging at the `Program.cs` CORS-policy level via a custom check before the CORS middleware — `gsd-phase-researcher` should investigate the concrete mechanism (custom middleware inspecting the `Origin` header ahead of `app.UseCors()`, or similar) and document a working approach before planning locks it in. — **Reversibility:** reversible — this is a logging-only addition with no functional/security behavior change if it turns out infeasible and gets scoped down to policy-level logging only.

### Claude's Discretion
- Exact wording/structured-parameter names of the new log statements (D-14, D-15) — follow this project's existing `ILogger<T>` + named-parameter conventions (e.g., patterns already used in `IgdbService`, `SteamService`).
- Concrete implementation mechanism for CORS rejection logging (D-16) — left to research/planning to resolve, per the note above.

### Folded Todos
- **Session planner leaks cross-user game data** (`.planning/todos/pending/2026-08-29-session-planner-leaks-cross-user-game-data.md`, `SessionManager.cs:15`, major severity) — folded into Phase 3 scope at explicit user request during this discussion, despite not being a native fit for the phase's JWT/CORS/rate-limiting/logging domain (it's a missing `.Where(g => g.UserId == userId)` filter — an access-control/data-isolation bug). It was reviewed-and-rejected the same way during Phase 2's discussion; this time the user chose to fold it in rather than defer it further. Downstream planning should treat this as an added, self-contained fix within `SessionManager.cs`, independent of the AUTH/NET/OBS decisions above.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & requirements
- `.planning/ROADMAP.md` — Phase 3 section: goal, success criteria, AUTH-01/02/NET-01/02/03/OBS-01 requirements
- `.planning/REQUIREMENTS.md` — AUTH-01, AUTH-02, NET-01, NET-02, NET-03, OBS-01 definitions

### Codebase state
- `.planning/codebase/CONCERNS.md` §"CORS Policy Hardcoded for Development", §"No Rate Limiting on Data Modification Endpoints" — original findings this phase fixes
- `.planning/codebase/ARCHITECTURE.md` §"Architectural Constraints" (existing fixed-window auth-only rate limiter), §"Authentication"/"Authorization" — current auth/authz posture
- `GamePlanner.API/Program.cs` — auth/CORS/rate-limiter composition root (lines 53-89): `TokenValidationParameters`, `AddFixedWindowLimiter("auth", ...)`, `AddPolicy("AllowAngular", ...)`
- `GamePlanner.API/Services/AuthService.cs` — JWT generation (`GenerateToken`, line 53-75), 7-day lifetime at line 71, no `ILogger` currently injected
- `GamePlanner.API/Controllers/AuthController.cs` — existing `[EnableRateLimiting("auth")]` pattern to follow for the new mutation-endpoint policy
- `GamePlanner.API/Controllers/GamesController.cs`, `GameJournalController.cs`, `SessionController.cs` — in-scope mutation endpoints for NET-03 (POST/PUT/DELETE routes enumerated during discussion)
- `GamePlanner.API/appsettings.json` — `Jwt:Issuer`/`Jwt:Audience` (already set), `App:FrontendUrl` (currently unused by CORS, D-05 wires it up)
- `game-planner-ui/src/app/interceptors/auth.interceptor.ts` — existing 401 → logout() redirect behavior (D-02 keeps this unchanged)
- `.planning/todos/pending/2026-08-29-session-planner-leaks-cross-user-game-data.md` — folded todo, see Folded Todos above
- `GamePlanner.API/Managers/SessionManager.cs` — line 15, missing `UserId` filter (the folded todo's fix location)

### Project state
- `.planning/STATE.md` — records Phase 3's dependency on Phase 1 (CORS allow-list must reflect the final upgraded Angular app's actual outgoing request headers)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `App:FrontendUrl` config key (`appsettings.json`) — already exists, already conventionally named for this exact purpose, currently unused by CORS setup. D-05 wires it up instead of introducing a new key.
- Existing `[EnableRateLimiting("auth")]` attribute pattern on `AuthController` — directly reusable pattern for applying the new mutation-endpoint policy to `GamesController`/`GameJournalController`/`SessionController`.
- Existing `ILogger<T>` + named-parameter structured-logging convention (used throughout `IgdbService`, `SteamService`, Workers) — the pattern to follow for the new D-14/D-15 log statements.

### Established Patterns
- `TokenValidationParameters` in `Program.cs` already validates `Issuer`/`Audience`/`Lifetime`/`IssuerSigningKey` — D-04 extends this same block rather than introducing new auth middleware.
- Rate limiting today is a single named policy (`"auth"`) applied via `[EnableRateLimiting]` attributes — D-12's shared-policy decision extends this exact pattern rather than introducing per-controller variation.
- No `ILogger` injected anywhere in the auth path (`AuthService`, `AuthController`) today — D-14 is a net-new addition, not an extension of existing logging.

### Integration Points
- `Program.cs` is the single composition root touched by nearly every decision in this phase (JWT validation params, CORS policy, rate limiter policies, `OnRejected` handler, and — per D-16 research — CORS rejection logging).
- `AuthService.cs` needs constructor changes (add `ILogger<AuthService>`) to support D-14.
- `GamesController`, `GameJournalController`, `SessionController` need `[EnableRateLimiting("...")]` attributes added to their POST/PUT/DELETE actions for D-09/D-12.
- `SessionManager.cs:15` is the isolated fix location for the folded todo — does not intersect with the AUTH/NET/OBS work above.

</code_context>

<specifics>
## Specific Ideas

No specific external reference implementations mentioned. The user's guidance across all four areas consistently favored the option that most literally satisfied the ROADMAP/REQUIREMENTS wording with the least new surface area (single `App:FrontendUrl` origin over a list, Games/Journal/Session-only rate limiting over broader coverage, one shared rate-limit policy over per-controller policies, `ILogger`-only logging over new infrastructure) — matching the personal/single-developer, hardening-not-features framing of this milestone.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope; the one cross-scope item (session-planner cross-user leak) was explicitly folded in rather than deferred, per user decision.

### Reviewed Todos (not folded)
None this phase — the only match (session-planner leak) was folded in, not deferred.

</deferred>

---

*Phase: 3-Auth, CORS & Rate Limiting Hardening*
*Context gathered: 2026-08-31*
