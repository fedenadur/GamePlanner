---
phase: 03-auth-cors-rate-limiting-hardening
plan: 02
subsystem: auth
tags: [aspnet-core, rate-limiting, ef-core, jwt, security]

# Dependency graph
requires:
  - phase: 03-auth-cors-rate-limiting-hardening
    provides: "mutations rate-limit policy registered and proven end-to-end on GamesController.Delete (plan 03-01)"
provides:
  - "mutations policy bound to all nine in-scope POST/PUT/DELETE actions across GamesController, GameJournalController, SessionController"
  - "SessionController.GetUserId() helper and Unauthorized guards, matching GamesController/GameJournalController"
  - "user-scoped db.Games queries in SessionManager.GetRecommendationAsync and SessionPlannerManager.GetRecommendationsAsync"
affects: [03-03-auth-cors-rate-limiting-hardening]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "GetUserId() copied verbatim across all four controllers (Games, GameJournal, Session) rather than extracted to a shared base — matches existing per-controller duplication convention"

key-files:
  created: []
  modified:
    - GamePlanner.API/Controllers/GamesController.cs
    - GamePlanner.API/Controllers/GameJournalController.cs
    - GamePlanner.API/Controllers/SessionController.cs
    - GamePlanner.API/Managers/SessionManager.cs
    - GamePlanner.API/Managers/SessionPlannerManager.cs

key-decisions:
  - "Both SessionManager and SessionPlannerManager fixed for the cross-user leak, not just the one named in CONTEXT/RESEARCH/PATTERNS — the todo's own reproduction traced to SessionPlannerManager (via POST /api/session/plan), which the source artifacts had missed"
  - "GameHLTB lookups in SessionPlannerManager left unfiltered by UserId — it is shared reference data (HowLongToBeat completion times) with no UserId column, accepted as T-03-14 in the threat register"

requirements-completed: [NET-03]

coverage:
  - id: D1
    description: "All four GamesController mutation actions (CreateFromIgdb, Create, Update, Delete) carry [EnableRateLimiting(\"mutations\")]"
    requirement: "NET-03"
    verification:
      - kind: manual_procedural
        ref: "grep count of [HttpPost]/[HttpPut]/[HttpDelete] vs EnableRateLimiting(\"mutations\") in GamesController.cs = 4/4"
        status: pass
    human_judgment: false
  - id: D2
    description: "All three GameJournalController mutation actions (CreateEntry, UpdateEntry, DeleteEntry) carry [EnableRateLimiting(\"mutations\")]"
    requirement: "NET-03"
    verification:
      - kind: manual_procedural
        ref: "grep count of [HttpPost]/[HttpPut]/[HttpDelete] vs EnableRateLimiting(\"mutations\") in GameJournalController.cs = 3/3"
        status: pass
    human_judgment: false
  - id: D3
    description: "Both SessionController actions (Recommend, Plan) resolve a caller user id, reject with 401 when absent, and carry [EnableRateLimiting(\"mutations\")]"
    requirement: "NET-03"
    verification:
      - kind: manual_procedural
        ref: "curl POST /api/session/plan with no Authorization header -> 401; grep of SessionController.cs shows 2x return Unauthorized(), 2x EnableRateLimiting(\"mutations\"), 2x userId.Value"
        status: pass
    human_judgment: false
  - id: D4
    description: "POST /api/session/recommend and POST /api/session/plan return only the caller's own games; an empty-library account gets a no-games response"
    requirement: "NET-03"
    verification:
      - kind: manual_procedural
        ref: "live run against http://localhost:5127: gsd-0302-empty@example.test (0 games) got {\"games\":[]}/no-games message from both endpoints; gsd-0302-owner@example.test (1 game, id=108158) got only that game back from both endpoints"
        status: pass
    human_judgment: false
  - id: D5
    description: "SessionManager and SessionPlannerManager db.Games queries both filter on g.UserId == userId; no unfiltered db.Games read remains in either manager"
    requirement: "NET-03"
    verification:
      - kind: manual_procedural
        ref: "dotnet build GamePlanner.sln exits 0; grep confirms g.UserId == userId present in both SessionManager.cs and SessionPlannerManager.cs"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-08-31
status: complete
---

# Phase 3 Plan 2: Broadcast Mutation Rate Limiting and Close Session-Planner Data Leak Summary

**Bound the shared `mutations` rate-limit policy to the remaining eight game/journal/session write endpoints and closed a cross-user game-library leak in both session-recommendation managers, proven live with a zero-game account and a one-game account against a running instance.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-08-31T12:05:01Z
- **Tasks:** 2 completed
- **Files modified:** 5 (`GamesController.cs`, `GameJournalController.cs`, `SessionController.cs`, `SessionManager.cs`, `SessionPlannerManager.cs`)

## Accomplishments

- Added `[EnableRateLimiting("mutations")]` to `GamesController.CreateFromIgdb`, `Create`, and `Update` (`Delete` was already bound in plan 03-01)
- Added `using Microsoft.AspNetCore.RateLimiting;` and `[EnableRateLimiting("mutations")]` to all three `GameJournalController` mutation actions (`CreateEntry`, `UpdateEntry`, `DeleteEntry`)
- Added a `GetUserId()` helper to `SessionController` — copied verbatim from `GamesController` — since it previously had no user-identity extraction at all
- `SessionController.Recommend` and `SessionController.Plan` now resolve the caller's user id, return `401 Unauthorized` when it cannot be resolved, and both carry `[EnableRateLimiting("mutations")]`
- Threaded `int userId` through `SessionManager.GetRecommendationAsync` and added `g.UserId == userId` to its `db.Games` predicate
- Threaded `int userId` through `SessionPlannerManager.GetRecommendationsAsync` and added `g.UserId == userId` to its `db.Games` predicate — this is the manager the folded todo's reproduction actually hit (`POST /api/session/plan`), which the phase's CONTEXT/RESEARCH/PATTERNS artifacts had not identified
- Live-verified end-to-end: a freshly registered account with zero games got `{"games":[]}` / a null-game no-games message from both session endpoints; an account owning one game got back only that game from both endpoints; an unauthenticated request to `/api/session/plan` returned `401`

## Task Commits

Each task was committed atomically:

1. **Task 1: Apply the mutations policy to every game and journal mutation action** - `07a5c87` (feat)
2. **Task 2: Scope both session-planner queries to the calling user and rate-limit the session endpoints** - `06ea53e` (fix)

## Files Created/Modified

- `GamePlanner.API/Controllers/GamesController.cs` - Added `[EnableRateLimiting("mutations")]` to `CreateFromIgdb`, `Create`, `Update`
- `GamePlanner.API/Controllers/GameJournalController.cs` - Added the `RateLimiting` import and `[EnableRateLimiting("mutations")]` to all three mutation actions
- `GamePlanner.API/Controllers/SessionController.cs` - Added `GetUserId()`, `Unauthorized()` guards, `[EnableRateLimiting("mutations")]` on both actions, `userId.Value` passed into both manager calls
- `GamePlanner.API/Managers/SessionManager.cs` - `GetRecommendationAsync` now takes `int userId` and filters `db.Games` on it
- `GamePlanner.API/Managers/SessionPlannerManager.cs` - `GetRecommendationsAsync` now takes `int userId` and filters `db.Games` on it

## Decisions Made

- Fixed the cross-user leak in both `SessionManager` and `SessionPlannerManager`, not only the one named in CONTEXT.md/RESEARCH.md/PATTERNS.md (`SessionManager.cs:15`) — reading `SessionPlannerManager.cs` during planning showed the same unfiltered `db.Games.Where(g => g.IsActive)` pattern at line 38-39, and it is the manager the original todo's reproduction (`POST /api/session/plan`) actually exercised
- Left `db.GamesHLTB` unfiltered in `SessionPlannerManager` — it is shared, non-user-scoped HowLongToBeat reference data with no `UserId` column (T-03-14, accepted)
- Copied `GetUserId()` into `SessionController` verbatim rather than extracting a shared base-controller helper, matching the existing byte-identical duplication already present across `GamesController`/`GameJournalController`

## Deviations from Plan

None - plan executed exactly as written, including the planner's own noted correction (fixing both session managers instead of only the one named in the source artifacts, which the plan itself called out as a planning-time finding rather than an in-flight deviation).

## Issues Encountered

- The plan 03-01 throwaway accounts (`gsd-rl-a@example.test`, `gsd-rl-b@example.test`) could not be logged into — their passwords were not recorded anywhere accessible to this plan. Registered two new throwaway accounts instead (`gsd-0302-empty@example.test` with zero games, `gsd-0302-owner@example.test` with one manually created game) to prove the isolation behavior. Both accounts and the one test game (`GSD-0302-Test-Game`, id 108158) remain in the local dev database — acceptable for a personal/local, non-production app per `PROJECT.md` constraints, matching plan 03-01's precedent.
- Did not construct a JWT with a missing/unparseable `NameIdentifier` claim to directly exercise that specific 401 branch, since `[Authorize]` already rejects any token that fails signature/claims validation before `GetUserId()` runs, and forging a validly-signed token with a deliberately malformed claim requires the (secret) signing key. Verified the surrounding contract instead: no-token requests return 401, and `GetUserId()` is a byte-identical copy of the pattern already proven correct in `GamesController`/`GameJournalController`.
- Temporary files created during live verification (registration/login response JSON, extracted bearer tokens, `.gsd-api.log`) were deleted after the verification pass; none were committed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- NET-03 is now fully satisfied: all nine in-scope game/journal/session mutation actions resolve the shared `mutations` policy, and no read endpoint carries a rate limit
- The folded cross-user data leak (`.planning/todos/pending/2026-08-29-session-planner-leaks-cross-user-game-data.md`) is closed on both affected endpoints and can be moved out of pending todos at phase close
- Plan 03-03 can proceed independently — this plan touched no `Program.cs` middleware or logging surface

---
*Phase: 03-auth-cors-rate-limiting-hardening*
*Completed: 2026-08-31*

## Self-Check: PASSED

- FOUND: GamePlanner.API/Controllers/GamesController.cs
- FOUND: GamePlanner.API/Controllers/GameJournalController.cs
- FOUND: GamePlanner.API/Controllers/SessionController.cs
- FOUND: GamePlanner.API/Managers/SessionManager.cs
- FOUND: GamePlanner.API/Managers/SessionPlannerManager.cs
- FOUND commit: 07a5c87
- FOUND commit: 06ea53e
