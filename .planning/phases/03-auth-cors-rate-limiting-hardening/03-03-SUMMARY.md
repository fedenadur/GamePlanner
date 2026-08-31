---
phase: 03-auth-cors-rate-limiting-hardening
plan: 03
subsystem: auth
tags: [logging, cors, aspnet-core, security, observability]

# Dependency graph
requires:
  - phase: 03-auth-cors-rate-limiting-hardening
    provides: "Program.cs middleware pipeline order (UseCors -> UseAuthentication -> UseRateLimiter) and AllowAngular CORS policy name, established in plan 03-01"
provides:
  - "ILogger<AuthService> wired into LoginAsync, with a structured warning on both failure branches (unknown/inactive email, wrong password)"
  - "First custom middleware in the project (GamePlanner.API.Middleware namespace): CorsRejectionLoggingMiddleware, an observation-only Origin-mismatch logger registered ahead of UseCors"
  - "OBS-01 fully satisfied: failed logins, CORS rejections, and rate-limit rejections (from 03-01) all reach the same configured ILogger stream"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Custom pre-UseCors() middleware as the only way to observe a CORS Origin mismatch server-side, since ASP.NET Core's CORS middleware has no OnRejected-equivalent hook (D-16)"
    - "Non-empty-Origin guard before comparing against App:FrontendUrl, to avoid logging every non-browser request (curl/Swagger/server-to-server) as a false-positive rejection"

key-files:
  created:
    - GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs
  modified:
    - GamePlanner.API/Services/AuthService.cs
    - GamePlanner.API/Program.cs

key-decisions:
  - "Two distinct failure-branch log reasons ('user not found or inactive' vs 'invalid password') stay operator-only -- AuthController.Login still returns one indistinguishable Unauthorized('Invalid email or password.') response for both, so the new logging cannot become a user-enumeration oracle (T-03-11)"
  - "CorsRejectionLoggingMiddleware never short-circuits, sets a status code, or touches Access-Control-* headers -- it observes and always calls next(context); UseCors remains solely responsible for the actual CORS decision"
  - "Implemented the primary middleware pattern from RESEARCH.md D-16, not the ICorsService decorator fallback -- single-origin, single-policy app doesn't need the decorator's extra complexity"

requirements-completed: [OBS-01]

coverage:
  - id: D1
    description: "AuthService.LoginAsync emits exactly one structured warning naming the email and a distinct reason on each of the two failure branches (unknown/inactive email, wrong password); a successful login emits none"
    requirement: "OBS-01"
    verification:
      - kind: manual_procedural
        ref: "Live login against http://localhost:5127/api/auth/login: unknown email and wrong-password attempts each added exactly one 'Failed login attempt' warning naming the email and the branch-specific reason; a subsequent successful login (freshly registered gsd-obs-probe@example.test) added zero"
        status: pass
    human_judgment: false
  - id: D2
    description: "No log statement added by this plan contains the submitted password, the issued JWT, or the stored password hash"
    requirement: "OBS-01"
    verification:
      - kind: manual_procedural
        ref: "grep -c of both submitted probe passwords (GsdProbe!2026, WrongPassword!2026, GsdObsProbe!2026) against the captured log = 0; static grep of logger.Log lines in AuthService.cs for Password/PasswordHash/Token = 0"
        status: pass
    human_judgment: false
  - id: D3
    description: "CorsRejectionLoggingMiddleware logs exactly one warning naming origin, configured origin, method, and path when the Origin header is present and mismatched; logs nothing when Origin is absent or matches the configured origin"
    requirement: "OBS-01"
    verification:
      - kind: manual_procedural
        ref: "Live requests against http://localhost:5127/api/games: Origin: http://evil.example added exactly one 'CORS rejection' warning; no Origin header added zero; Origin: http://localhost:4200 (configured) added zero"
        status: pass
    human_judgment: false
  - id: D4
    description: "Middleware registered between UseHttpsRedirection and UseCors; 03-01's UseCors < UseAuthentication < UseRateLimiter ordering is intact"
    requirement: "OBS-01"
    verification:
      - kind: unit
        ref: "Plan verify script: line-number comparison of app.UseHttpsRedirection() < UseMiddleware<CorsRejectionLoggingMiddleware> < app.UseCors( < app.UseAuthentication() < app.UseRateLimiter() in Program.cs -- all conditions true"
        status: pass
    human_judgment: false

duration: 5min
completed: 2026-08-31
status: complete
---

# Phase 3 Plan 3: Failed-Login and CORS-Rejection Security Logging Summary

**Wired `ILogger<AuthService>` into both `LoginAsync` failure branches and added the project's first custom middleware — an observation-only `CorsRejectionLoggingMiddleware` ahead of `UseCors` — completing OBS-01's three required event classes (failed logins, CORS rejections, rate-limit rejections).**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-08-31T12:07:01Z
- **Completed:** 2026-08-31T12:09:56Z
- **Tasks:** 2 completed
- **Files modified:** 3 (`AuthService.cs`, `Program.cs`, new `CorsRejectionLoggingMiddleware.cs`)

## Accomplishments

- `AuthService` primary constructor now takes `ILogger<AuthService> logger` (previously had zero logging), matching `IgdbService`'s injection style
- Both `LoginAsync` failure branches (`user is null || user.PasswordHash is null`, `!BCrypt.Net.BCrypt.Verify(...)`) emit `logger.LogWarning("Failed login attempt for {Email}: {Reason}", ...)` with a distinct reason literal per branch — `"user not found or inactive"` and `"invalid password"`
- `AuthController.Login` untouched: still returns one `Unauthorized("Invalid email or password.")` for both branches, keeping the internal distinction operator-only
- Created `GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs` — the project's first custom middleware, in a new `GamePlanner.API.Middleware` namespace/folder
- The middleware compares the incoming `Origin` header against `App:FrontendUrl` (the same config key `UseCors` reads) and logs a warning only when `Origin` is non-empty and mismatched — guards against false-positive noise from curl/Swagger/server-to-server calls that send no `Origin` header at all
- Registered `app.UseMiddleware<GamePlanner.API.Middleware.CorsRejectionLoggingMiddleware>()` between `app.UseHttpsRedirection()` and `app.UseCors("AllowAngular")`; plan 03-01's `UseCors < UseAuthentication < UseRateLimiter` ordering left untouched
- Proved all of the above live: two failed-login attempts (unknown email, wrong password) each logged exactly one warning with the correct email and reason; a fresh successful login logged none; neither submitted password ever appeared in the log; a mismatched-Origin request logged exactly one CORS-rejection warning; a no-Origin request and a matching-Origin request each logged zero

## Task Commits

Each task was committed atomically:

1. **Task 1: Log failed login attempts from AuthService** - `61fa076` (feat)
2. **Task 2: Add and register the CORS-rejection logging middleware** - `821a851` (feat)

## Files Created/Modified

- `GamePlanner.API/Services/AuthService.cs` - Added `ILogger<AuthService>` to the primary constructor; `LogWarning` on both `LoginAsync` failure branches with named-parameter templates, no secret material logged
- `GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs` (new) - Observation-only middleware logging a structured warning on Origin/App:FrontendUrl mismatch, never short-circuiting the request
- `GamePlanner.API/Program.cs` - Registered the new middleware between `UseHttpsRedirection` and `UseCors`

## Decisions Made

- Followed RESEARCH.md D-16's primary recommendation (the simple pre-`UseCors()` middleware) rather than the `ICorsService` decorator fallback — single-origin, single-policy app, and the primary pattern is fully specified and verified
- Success path (`RegisterAsync`, `GenerateToken`, successful `LoginAsync`) stays silent — OBS-01 asks for failed-login visibility, not an audit log of every login

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched RESEARCH.md/PATTERNS.md's fully-specified code verbatim.

## Issues Encountered

- The plan's precondition for Task 1 (a registered account existing) was satisfied by the throwaway `gsd-rl-a@example.test` account left over from plan 03-01's rate-limit verification. A second throwaway account (`gsd-obs-probe@example.test`) was registered during this plan's verification specifically to prove the "successful login logs nothing" behavior (the plan's example curl commands only cover the two failure cases) — left in the local dev database, acceptable for a personal/local, non-production app per `PROJECT.md` constraints, consistent with plan 03-01's precedent.
- Live verification ran against `http://localhost:5127` (the default `dotnet run` http profile), same as plan 03-01, not the plan's `https://localhost:7174` example — identical application logic exercised.
- Temporary log file (`.gsd-api.log`) and response-capture scratch files created during live verification were deleted after the verification pass; none were committed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- OBS-01 is now fully satisfied: failed logins (this plan), CORS rejections (this plan), and rate-limit rejections (plan 03-01) all reach the project's existing configured `ILogger` stream, with no new logging infrastructure introduced
- This is the last plan in phase 03 (auth-cors-rate-limiting-hardening) — phase-level verification runs next
- Two pre-existing pending todos remain open and out of scope for this phase: session planner cross-user leak fix status (folded into 03-02, already resolved there per STATE.md) and the IGDBId upsert cross-user deactivation issue (`Database/33_...sql`, `34_...sql`, drafted but unapplied)

---
*Phase: 03-auth-cors-rate-limiting-hardening*
*Completed: 2026-08-31*

## Self-Check: PASSED

- FOUND: GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs
- FOUND: GamePlanner.API/Services/AuthService.cs
- FOUND: GamePlanner.API/Program.cs
- FOUND: .planning/phases/03-auth-cors-rate-limiting-hardening/03-03-SUMMARY.md
- FOUND commit: 61fa076
- FOUND commit: 821a851
