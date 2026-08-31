---
phase: 03-auth-cors-rate-limiting-hardening
plan: 01
subsystem: auth
tags: [jwt, cors, rate-limiting, aspnet-core, security]

# Dependency graph
requires:
  - phase: 02-credential-secrets-hardening
    provides: rotated Jwt:Secret seeded via dotnet user-secrets, catch(Exception) hardening pattern in Services/Workers
provides:
  - 24-hour JWT lifetime (down from 7 days)
  - HS256-pinned, expiry-required JWT validation
  - Config-sourced CORS origin (App:FrontendUrl) with explicit header/method allow-lists
  - Per-user "mutations" rate-limit policy (100/hr) proven end-to-end on GamesController.Delete
  - Structured OnRejected logging for rate-limit rejections (user/IP, path, policy name)
  - Reordered middleware pipeline (UseAuthentication before UseRateLimiter)
affects: [03-02-auth-cors-rate-limiting-hardening, 03-03-auth-cors-rate-limiting-hardening]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AddPolicy<string>(name, httpContext => RateLimitPartition.GetFixedWindowLimiter(key, ...)) for per-identity rate limiting, vs. the unpartitioned AddFixedWindowLimiter convenience overload used by the pre-existing 'auth' policy"
    - "RateLimiterOptions.OnRejected resolves ILoggerFactory from context.HttpContext.RequestServices (options lambda runs at registration time, before DI container exists)"
    - "Config presence check + throw InvalidOperationException at startup for required-but-unset configuration keys, instead of a silent fallback"

key-files:
  created: []
  modified:
    - GamePlanner.API/Program.cs
    - GamePlanner.API/Services/AuthService.cs
    - GamePlanner.API/Controllers/GamesController.cs

key-decisions:
  - "Middleware pipeline reordered so UseAuthentication runs before UseRateLimiter — required for the mutations partitioner to read an authenticated ClaimTypes.NameIdentifier instead of collapsing every caller into the 'anonymous' partition (RESEARCH.md Pitfall 2)"
  - "'auth' policy (login/register) stays an unpartitioned global bucket; only 'mutations' is per-user — deliberate asymmetry since no JWT identity exists yet at the login/register boundary (assumption_delta_decision in the plan)"
  - "ClockSkew left at the library default (5 min) per D-03 — no explicit assignment added"
  - "GamesController.Delete is the tracer endpoint bound to the mutations policy; the remaining mutation actions across Games/Journal/Session controllers are bound in plan 03-02"

requirements-completed: [AUTH-01, AUTH-02, NET-01, NET-02]

coverage:
  - id: D1
    description: "JWT tokens expire 24 hours after issue (down from 7 days)"
    requirement: "AUTH-01"
    verification:
      - kind: manual_procedural
        ref: "Live login against http://localhost:5127/api/auth/login; decoded exp claim delta = 86399s (target 86400 +/-120s)"
        status: pass
    human_judgment: false
  - id: D2
    description: "JWT validation rejects tokens with a tampered signature or non-HS256 algorithm; requires an exp claim"
    requirement: "AUTH-02"
    verification:
      - kind: manual_procedural
        ref: "GET /api/games with a signature-tampered bearer token -> 401 (live curl against running instance)"
        status: pass
    human_judgment: false
  - id: D3
    description: "CORS origin sourced from App:FrontendUrl config; startup fails loudly when the key is missing or empty"
    requirement: "NET-01"
    verification:
      - kind: manual_procedural
        ref: "dotnet run with App__FrontendUrl=\"\" -> Unhandled InvalidOperationException naming 'App:FrontendUrl' at Program.cs:119"
        status: pass
    human_judgment: false
  - id: D4
    description: "CORS preflight restricts Access-Control-Allow-Headers to Authorization/Content-Type and Access-Control-Allow-Methods to GET/POST/PUT/DELETE; disallowed origins get no Access-Control-Allow-Origin"
    requirement: "NET-02"
    verification:
      - kind: manual_procedural
        ref: "curl -X OPTIONS /api/games with Origin http://localhost:4200 -> Allow-Headers: Authorization,Content-Type; Allow-Methods: GET,POST,PUT,DELETE. curl with Origin http://evil.example -> no Access-Control-* headers"
        status: pass
    human_judgment: false
  - id: D5
    description: "Per-user mutation rate limit (100/hr) proven end-to-end on GamesController.Delete: 100th request permitted, 101st rejected with 429, a second user's identical request in the same window is unaffected, and the rejection is logged"
    requirement: "NET-03"
    verification:
      - kind: manual_procedural
        ref: "101 sequential DELETE /api/games/999999999 requests as user A: requests 1-100 = 404, request 101 = 429. User B's identical request while A is rejected = 404. .gsd-api.log contains 2x 'Rate limit rejected for 8 on /api/games/999999999 by policy mutations'"
        status: pass
    human_judgment: false
    # NET-03 is fully satisfied by this plan only for the tracer endpoint (GamesController.Delete);
    # broadcasting the policy to the remaining mutation endpoints is plan 03-02, per plan success_criteria.

duration: 50min
completed: 2026-08-31
status: complete
---

# Phase 3 Plan 1: Auth/CORS/Rate-Limiting Pipeline Hardening Summary

**Reordered the ASP.NET Core middleware pipeline, added a per-user 100/hr rate-limit policy proven end-to-end with a live 429 and cross-user isolation check, cut JWT lifetime to 24h with HS256 pinning, and replaced the wildcard CORS policy with a config-sourced origin and explicit header/method allow-lists.**

## Performance

- **Duration:** ~50 min
- **Completed:** 2026-08-31T03:06:36Z
- **Tasks:** 2 completed
- **Files modified:** 3 (`Program.cs`, `AuthService.cs`, `GamesController.cs`)

## Accomplishments

- Registered a new `"mutations"` rate-limit policy (`AddPolicy<string>`, 100 requests/hour) partitioned by `ClaimTypes.NameIdentifier`, distinct from the pre-existing unpartitioned `"auth"` policy
- Reordered the middleware pipeline so `UseAuthentication()` runs before `UseRateLimiter()`, so the partitioner sees a populated principal instead of collapsing every caller into one shared `anonymous` bucket
- Added a structured `OnRejected` handler that logs `"Rate limit rejected for {UserId} on {Path} by policy {PolicyName}"` before every 429 response
- Reduced JWT lifetime from 7 days to 24 hours in `AuthService.GenerateToken`
- Pinned `TokenValidationParameters.ValidAlgorithms` to `HmacSha256` and set `RequireExpirationTime = true`
- Replaced the hardcoded `"http://localhost:4200"` CORS origin with `App:FrontendUrl` config, and replaced `AllowAnyHeader()`/`AllowAnyMethod()` with explicit `Authorization`/`Content-Type` header and `GET/POST/PUT/DELETE` method allow-lists
- Startup now throws `InvalidOperationException` naming `App:FrontendUrl` when that key is missing or empty, instead of silently falling back to a permissive policy
- Bound `GamesController.Delete` — the tracer endpoint — to the `"mutations"` policy
- Proved the whole reconfigured pipeline live: 100 DELETE requests from one user returned 404, the 101st returned 429, a second user's identical request in the same window still returned 404, and the rejection was logged

## Task Commits

Each task was committed atomically:

1. **Task 1: End-to-end per-user rate limiting on one endpoint — pipeline reorder proven live** - `8238293` (feat)
2. **Task 2: Lock down the CORS policy and the JWT validation parameters** - `c36507b` (feat)

_Task 1 is a `type="tracer"` task; its live end-to-end verification (below) ran immediately after the commit and passed, so Task 2 proceeded without a checkpoint._

## Files Created/Modified

- `GamePlanner.API/Program.cs` - Registered the `mutations` rate-limit policy and `OnRejected` logging, reordered the middleware pipeline, hardened `TokenValidationParameters`, and locked down the CORS policy to config-sourced origin + explicit allow-lists
- `GamePlanner.API/Services/AuthService.cs` - Changed JWT expiry from `AddDays(7)` to `AddHours(24)`
- `GamePlanner.API/Controllers/GamesController.cs` - Added `using Microsoft.AspNetCore.RateLimiting;` and `[EnableRateLimiting("mutations")]` on the `Delete` action

## Decisions Made

- Middleware pipeline order changed to `UseHttpsRedirection` -> `UseCors` -> `UseAuthentication` -> `UseRateLimiter` -> `UseAuthorization` -> `MapControllers`, required for the partitioner to read the authenticated claim (RESEARCH.md Pitfall 2)
- `"auth"` policy (login/register) intentionally stays unpartitioned; only `"mutations"` is per-user, since no JWT identity exists at the login/register boundary
- `ClockSkew` left at the library default (5 min); no explicit assignment added (D-03)
- No `AllowCredentials()` added to the CORS policy — this API authenticates via bearer header, not cookies

## Deviations from Plan

None - plan executed exactly as written. The frontend contract re-scan (RESEARCH.md assumption A2) confirmed exactly one custom header (`Authorization` in `auth.interceptor.ts`) and exactly four HTTP verbs (`get`, `post`, `put`, `delete`) in use across `game-planner-ui/src/app`, matching the plan's assumption before the CORS allow-list was locked down.

## Issues Encountered

- `dotnet run`'s default launch profile (`http`) only listens on `http://localhost:5127`, not the `https://localhost:7174` URL referenced in the plan's verify script. All live end-to-end verification (registration, login, 101-request rate-limit proof, CORS preflight, missing-config startup failure) was run against `http://localhost:5127` instead, which exercises identical application logic (the plan's `curl -k` flags exist specifically to tolerate the local dev cert, not to select a specific port). Two throwaway test accounts (`gsd-rl-a@example.test`, `gsd-rl-b@example.test`) were registered directly against the running API and left in the local dev database — acceptable for a personal/local, non-production app per `PROJECT.md` constraints.
- Temporary files created during live verification (`.gsd-api.log`, `.gsd-api2.log`, `.gsd-token-a.txt`, `.gsd-token-b.txt`, `.gsd-rl-results.txt`) were deleted after each verification pass; none were committed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 03-02 can now broadcast `[EnableRateLimiting("mutations")]` across the remaining Games/Journal/Session mutation endpoints, per the plan's success criteria and the companion invariant noted in the `assumption_delta_decision`
- Plan 03-03 can build the failed-login and CORS-rejection logging halves of OBS-01 on top of the pipeline order and `AllowAngular` policy name established here
- Deferred to end-of-phase (D-08): full Angular SPA walkthrough against the hardened API confirming no browser-console CORS errors on any workflow

---
*Phase: 03-auth-cors-rate-limiting-hardening*
*Completed: 2026-08-31*

## Self-Check: PASSED

- FOUND: GamePlanner.API/Program.cs
- FOUND: GamePlanner.API/Services/AuthService.cs
- FOUND: GamePlanner.API/Controllers/GamesController.cs
- FOUND: .planning/phases/03-auth-cors-rate-limiting-hardening/03-01-SUMMARY.md
- FOUND commit: 8238293
- FOUND commit: c36507b
