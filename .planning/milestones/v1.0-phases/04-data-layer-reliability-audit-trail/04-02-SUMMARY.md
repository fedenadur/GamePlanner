---
phase: 04-data-layer-reliability-audit-trail
plan: 02
subsystem: infra
tags: [background-worker, concurrent-dictionary, xunit, thread-safety]

requires:
  - phase: 04-01
    provides: "GamePlanner.Tests xUnit harness (real SQL Server, no mocks) that this plan adds a file to"
provides:
  - "SteamTokenStore.RemoveExpired() sweep + Count property, bounding the store to live entries"
  - "SteamTokenCleanupWorker (10-minute BackgroundService) reporting to WorkerStatus"
affects: [04-03, gsd-secure-phase]

tech-stack:
  added: []
  patterns:
    - "BackgroundService workers replicate the SteamPlaytimeSyncWorker shape byte-for-byte: WorkerName const, static readonly Interval, ExecuteAsync while-loop, RunAsync scope/try/catch/status block"
    - "Singleton services injected directly into a worker's primary constructor (not resolved from the per-run scope) when the service itself has no scoped dependencies"
    - "ConcurrentDictionary sweep-then-TryRemove loop (no RemoveWhere exists on ConcurrentDictionary) so a sweep and a concurrent mutation can never double-count or throw"

key-files:
  created:
    - GamePlanner.API/Workers/SteamTokenCleanupWorker.cs
    - Database/37_SeedWorkerStatus_SteamTokenCleanup.sql
    - GamePlanner.Tests/SteamTokenStoreSweepTests.cs
  modified:
    - GamePlanner.API/Services/SteamTokenStore.cs
    - GamePlanner.API/Program.cs

key-decisions:
  - "Logged the removal count at LogInformation, not LogWarning -- unlike the analog's failedCount>0 case, a non-zero removed count is not a failure, only a completed-sweep fact worth recording"
  - "D-14's live /workers + log-line check deferred to end-of-phase human UAT, consistent with config.json workflow.human_verify_mode: end-of-phase and the single end-of-phase UAT pattern used in Phase 3"

requirements-completed: [REL-02]

coverage:
  - id: D1
    description: "SteamTokenStore.RemoveExpired() sweeps only entries whose Expiry is already past, using TryRemove so a concurrent redeem cannot be double-counted or lost"
    requirement: "REL-02"
    verification:
      - kind: unit
        ref: "GamePlanner.Tests/SteamTokenStoreSweepTests.cs#RemoveExpired_ShrinksStore_ToOnlyLiveEntries"
        status: pass
    human_judgment: false
  - id: D2
    description: "A sweep never removes an entry whose Expiry is still in the future -- live codes still redeem after a sweep runs"
    requirement: "REL-02"
    verification:
      - kind: unit
        ref: "GamePlanner.Tests/SteamTokenStoreSweepTests.cs#RemoveExpired_NeverRemovesLiveEntries_TheyStillRedeem"
        status: pass
    human_judgment: false
  - id: D3
    description: "RemoveExpired() is idempotent -- a second sweep on an already-swept store returns 0 and changes nothing"
    requirement: "REL-02"
    verification:
      - kind: unit
        ref: "GamePlanner.Tests/SteamTokenStoreSweepTests.cs#RemoveExpired_IsIdempotent_SecondSweepRemovesNothing"
        status: pass
    human_judgment: false
  - id: D4
    description: "RemoveExpired() on an empty store returns 0, so the worker's if (removed > 0) guard stays silent on a quiet sweep"
    requirement: "REL-02"
    verification:
      - kind: unit
        ref: "GamePlanner.Tests/SteamTokenStoreSweepTests.cs#RemoveExpired_OnEmptyStore_ReturnsZero"
        status: pass
    human_judgment: false
  - id: D5
    description: "SteamTokenCleanupWorker runs on a 10-minute interval, matches the canonical BackgroundService shape, is registered as the 7th hosted service, is seeded into WorkerStatus, and shows up on the /workers dashboard reporting a completed run with a count-only log line -- the live D-14 check"
    requirement: "REL-02"
    verification: []
    human_judgment: true
    rationale: "Requires the API and Angular app running live, waiting past the 5-minute token expiry and the 10-minute sweep interval, and reading the API log -- not reproducible from an offline unit test. Deferred to end-of-phase human UAT per config.json workflow.human_verify_mode: end-of-phase."

duration: 20min
completed: 2026-08-31
status: complete
---

# Phase 4 Plan 02: SteamTokenStore Cleanup Worker Summary

**A 10-minute `SteamTokenCleanupWorker` sweeps `SteamTokenStore`'s expired entries via a new `RemoveExpired()` method, bounding the previously-unbounded `ConcurrentDictionary` and reporting to `WorkerStatus` like every other background task.**

## Performance

- **Duration:** ~20 min
- **Tasks:** 2 completed
- **Files modified:** 5 (3 new, 2 modified)

## Accomplishments

- `SteamTokenStore` gained `RemoveExpired()` (enumerate + `TryRemove` for each already-expired entry, returns the removed count), a `Count` property, and an optional `lifetime` parameter on `Store()` so tests can seed already-expired entries without waiting
- New `SteamTokenCleanupWorker` replicates `SteamPlaytimeSyncWorker`'s exact `BackgroundService` shape (10-minute interval, `WorkerStatusManager` scope/try/catch/status block) and never names the stored payload type (`AuthResponse`) anywhere in the file
- Registered as the 7th `AddHostedService` call in `Program.cs`, immediately after `SteamPlaytimeSyncWorker`
- `Database/37_SeedWorkerStatus_SteamTokenCleanup.sql` seeds the `WorkerStatus` row so the worker appears on `/workers` from first boot
- 4 new offline xUnit facts in `GamePlanner.Tests/SteamTokenStoreSweepTests.cs` prove: the store shrinks to exactly its live-entry count, live entries still redeem after a sweep, a second sweep is a no-op, and an empty store sweeps to zero
- The production call site (`AuthController.SteamCallback` -> `steamTokenStore.Store(result)`) is untouched -- still a single argument, so the five-minute login window is unchanged for real callers

## Task Commits

Each task was committed atomically:

1. **Task 1: Sweep method, cleanup worker, DI registration and WorkerStatus seed** - `b022f9b` (feat)
2. **Task 2: Prove the sweep — automated shrink test plus a live worker check** - `6cdcaea` (test)

## Files Created/Modified

- `GamePlanner.API/Services/SteamTokenStore.cs` - Added `RemoveExpired()`, `Count`, `DefaultLifetime`, and an optional `lifetime` parameter on `Store()`
- `GamePlanner.API/Workers/SteamTokenCleanupWorker.cs` - New `BackgroundService`, 10-minute sweep, reports to `WorkerStatus`, count-only logging
- `GamePlanner.API/Program.cs` - Registered `SteamTokenCleanupWorker` as the 7th hosted service
- `Database/37_SeedWorkerStatus_SteamTokenCleanup.sql` - New migration seeding the `WorkerStatus` row; applied via `sqlcmd`
- `GamePlanner.Tests/SteamTokenStoreSweepTests.cs` - 4 new xUnit facts covering shrink, live-entry survival, idempotency, and the empty-store case

## Decisions Made

- Logged the non-zero removal count at `LogInformation` rather than mirroring the analog's `LogWarning` for `failedCount > 0` -- a completed sweep that removed entries is not a failure condition, only `if (removed > 0)` was mirrored from the analog (the silence-on-quiet-sweep convention), not the log level
- Deferred the D-14 live `/workers` dashboard + log-line check to end-of-phase human UAT, consistent with `config.json` `workflow.human_verify_mode: end-of-phase` and the single end-of-phase UAT pattern already used in Phase 3 (see `coverage` D5 for the rationale)

## Deviations from Plan

None - plan executed exactly as written. Both tasks' automated `<verify>` blocks passed on the first attempt with no auto-fixes needed.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. `Database/37_SeedWorkerStatus_SteamTokenCleanup.sql` was applied directly to the local dev database (`sqlcmd -S localhost -d GamePlannerDB -E -C -b -i Database/37_SeedWorkerStatus_SteamTokenCleanup.sql`) as part of this execution.

## Next Phase Readiness

- REL-02 is code-complete and offline-proven; only the live `/workers` dashboard confirmation (D-14) remains, deferred to end-of-phase UAT alongside plan 04-03's checks
- No blockers for 04-03 (`sp_UpsertIGDBGame` fix / `IX_Games_IGDBId` index) -- fully independent of this plan's files
- `GamePlanner.Tests` now carries 9 passing facts total (5 from 04-01, 4 from this plan) with no project-file changes needed for either addition

---
*Phase: 04-data-layer-reliability-audit-trail*
*Completed: 2026-08-31*

## Self-Check: PASSED

- FOUND: `GamePlanner.API/Services/SteamTokenStore.cs`
- FOUND: `GamePlanner.API/Workers/SteamTokenCleanupWorker.cs`
- FOUND: `GamePlanner.API/Program.cs`
- FOUND: `Database/37_SeedWorkerStatus_SteamTokenCleanup.sql`
- FOUND: `GamePlanner.Tests/SteamTokenStoreSweepTests.cs`
- FOUND: `.planning/phases/04-data-layer-reliability-audit-trail/04-02-SUMMARY.md`
- FOUND commit: `b022f9b`
- FOUND commit: `6cdcaea`
