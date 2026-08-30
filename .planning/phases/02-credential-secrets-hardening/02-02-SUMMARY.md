---
phase: 02-credential-secrets-hardening
plan: 02
subsystem: api
tags: [worker-status, ef-core, sql-migration, reliability, partial-failure-tracking]

# Dependency graph
requires: []
provides:
  - "WorkerStatus.FailedItemCount (int?) and WorkerStatus.LastPartialFailureDetail (string?, 500-char truncated) columns, mapped by EF convention"
  - "WorkerStatusManager.SetCompletedAsync(string name, int? failedItemCount = null, string? failureDetail = null) — optional parameters, existing call sites unchanged"
  - "Database/35_AlterWorkerStatus_AddFailureTracking.sql applied to GamePlannerDB (idempotent, run twice in SSMS with no error)"
affects: [02-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Nullable int?/string? on WorkerStatus for 'not tracked' vs 'zero failures' honesty — mirrors existing LastError nullability"
    - "Optional-parameter signature extension (not overload) to keep existing call sites compiling untouched"

key-files:
  created:
    - Database/35_AlterWorkerStatus_AddFailureTracking.sql
  modified:
    - GamePlanner.API/Entities/WorkerStatus.cs
    - GamePlanner.API/Managers/WorkerStatusManager.cs

key-decisions:
  - "FailedItemCount and LastPartialFailureDetail added as nullable, not defaulted to 0/empty — null means 'this worker does not count item-level failures', distinct from 'zero failures counted'"
  - "SetFailedAsync left completely untouched; WorkerStatusValue.Failed appears exactly once in WorkerStatusManager.cs — partial failures never route through the failed-run path (RESEARCH Pitfall 4 / D-16)"

requirements-completed: [REL-03]

coverage:
  - id: D1
    description: "GET /api/worker/status returns FailedItemCount and LastPartialFailureDetail on every WorkerStatus row, with no new table and no new endpoint"
    requirement: "REL-03"
    verification:
      - kind: integration
        ref: "WorkerController.GetAll returns the entity list directly (no DTO layer) — new POCO properties surface on the endpoint automatically once EF maps them; confirmed via live app run reading/writing WorkerStatus rows with no schema error"
        status: pass
    human_judgment: false
  - id: D2
    description: "A sync run that completed with some individual item failures is recorded Status=Completed with non-zero FailedItemCount, never routed through SetFailedAsync"
    requirement: "REL-03"
    verification:
      - kind: other
        ref: "grep -c 'WorkerStatusValue.Failed' WorkerStatusManager.cs == 1 (SetFailedAsync only); SetCompletedAsync's new parameters are the only path that sets FailedItemCount/LastPartialFailureDetail"
        status: pass
    human_judgment: false
  - id: D3
    description: "A worker that does not track item-level failures leaves FailedItemCount null rather than asserting zero"
    requirement: "REL-03"
    verification:
      - kind: other
        ref: "FailedItemCount declared int? with no default assignment in SetCompletedAsync beyond the optional parameter's own default (null); all five existing call sites omit the new arguments and therefore leave both new columns null"
        status: pass
    human_judgment: false
  - id: D4
    description: "The five existing SetCompletedAsync call sites compile unchanged because the new parameters are optional"
    requirement: "REL-03"
    verification:
      - kind: integration
        ref: "dotnet build GamePlanner.sln exits 0 with no changes to any Workers/*.cs file in this plan's diff (git status --short confirms only the three files in <files> changed)"
        status: pass
    human_judgment: false
  - id: D5
    description: "LastPartialFailureDetail is truncated to at most 500 characters, matching the existing LastError truncation convention"
    requirement: "REL-03"
    verification:
      - kind: other
        ref: "SetCompletedAsync applies failureDetail[..Math.Min(failureDetail.Length, 500)] — same Math.Min range-index form as SetFailedAsync's LastError truncation on the pre-existing line"
        status: pass
    human_judgment: false
  - id: D6
    description: "Database/35_AlterWorkerStatus_AddFailureTracking.sql applied cleanly to GamePlannerDB, idempotent on re-run"
    requirement: "REL-03"
    verification:
      - kind: human
        ref: "User ran the script twice in SSMS (per CLAUDE.md's manual-migration rule); confirmed no error on either run and both columns present via sys.columns query. Followed by live app start: log contains 'GameSyncWorker started', no 'invalid column' error."
        status: pass
    human_judgment: true
---

# Phase 02 Plan 02: WorkerStatus Partial-Failure Tracking Summary

**WorkerStatus gains two nullable columns (FailedItemCount, LastPartialFailureDetail) and SetCompletedAsync gains matching optional parameters, so a sync run can report "completed, but N items failed" through the existing GET /api/worker/status endpoint without ever being misreported as a failed run — migration applied and live-verified.**

## Performance

- **Duration:** ~12 min (excluding checkpoint wait for manual SSMS application)
- **Tasks:** 2 (1 auto + 1 human-action checkpoint)
- **Files modified:** 3 (1 new)

## Accomplishments
- `WorkerStatus.cs` gained `FailedItemCount` (`int?`) and `LastPartialFailureDetail` (`string?`) as plain nullable auto-properties, no attributes, matching the file's existing flat aligned style.
- `WorkerStatusManager.SetCompletedAsync` signature extended to `SetCompletedAsync(string name, int? failedItemCount = null, string? failureDetail = null)` — optional parameters, not a second overload, so all five existing call sites (`UpcomingGamesSyncWorker`, `GameImageSyncWorker`, `GameSyncWorker`, `GameHLTBSyncWorker` x2, `SteamPlaytimeSyncWorker`) compile untouched and, unless updated in a future plan, continue to leave both new columns `null`.
- `failureDetail` is truncated with the same `Math.Min` range-index expression already used by `SetFailedAsync`'s `LastError` truncation, so both string columns share one 500-char convention.
- `SetFailedAsync` was not touched; `WorkerStatusValue.Failed` still appears exactly once in the file (inside `SetFailedAsync`), keeping the partial-failure path structurally separate from the failed-run path (RESEARCH Pitfall 4, D-16).
- `Database/35_AlterWorkerStatus_AddFailureTracking.sql` created following the `24_AlterGames_AddRatingFields.sql` convention: `USE GamePlannerDB; GO`, two `IF NOT EXISTS (SELECT 1 FROM sys.columns ...)`-guarded `ALTER TABLE` statements (one per column), each followed by its own `GO`.
- User applied the script twice in SSMS (per `CLAUDE.md`'s manual-migration rule) — second run was a clean no-op confirming idempotency — and confirmed both columns present via `sys.columns`.
- Live app start (`ASPNETCORE_ENVIRONMENT=Development dotnet run`) verified: log contains `GameSyncWorker started` and multiple successful `WorkerStatus`/`Games` reads and writes, with no "invalid column" error anywhere in the captured output.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add partial-failure columns to WorkerStatus and extend SetCompletedAsync** - `2015991` (feat)
2. **Task 2: Apply Database/35_AlterWorkerStatus_AddFailureTracking.sql in SSMS** - human-action checkpoint, no code commit (schema-only change applied directly to the database by the user)

## Files Created/Modified
- `GamePlanner.API/Entities/WorkerStatus.cs` - Added `FailedItemCount` (`int?`) and `LastPartialFailureDetail` (`string?`) after `LastError`
- `GamePlanner.API/Managers/WorkerStatusManager.cs` - `SetCompletedAsync` extended with two optional parameters; assigns both new properties (with truncation) before `SaveChangesAsync`; `SetFailedAsync` unchanged
- `Database/35_AlterWorkerStatus_AddFailureTracking.sql` - New idempotent migration adding `FailedItemCount INT NULL` and `LastPartialFailureDetail NVARCHAR(500) NULL` to `WorkerStatus`

## Decisions Made
- **Nullable, not defaulted:** `FailedItemCount` stays `int?` (never coerced to `0`) so "not tracked by this worker" remains distinguishable from "zero failures counted" — matches the plan's explicit design intent and the RESEARCH honesty requirement.
- **Optional parameters over a second overload:** keeps all five existing `SetCompletedAsync` call sites compiling with zero changes, deferring the actual wiring of failure counts into worker call sites to plan 02-03.
- **SetFailedAsync left untouched:** partial failures never risk being reported as a failed overall run; verified structurally via the single-occurrence grep gate on `WorkerStatusValue.Failed`.

## Deviations from Plan

None — plan executed exactly as written. All automated verify gates (build, 7 grep/test checks, git diff scope) passed on the first attempt. The SSMS checkpoint completed cleanly on the user's first reported attempt with no error.

## Issues Encountered

None.

## User Setup Required

None further for this plan. `Database/35_AlterWorkerStatus_AddFailureTracking.sql` has already been applied to `GamePlannerDB` by the user during this session's checkpoint.

## Next Phase Readiness

- REL-03's schema and manager-signature contract is closed: two nullable columns exist on `WorkerStatus`, `GET /api/worker/status` will surface them (currently always `null` since no call site passes the new arguments yet), and the failed-vs-partial-failure distinction is structurally enforced.
- Plan 02-03 (the sibling plan that actually wires per-worker failure counts into `SetCompletedAsync` calls) is unblocked — the destination it needs (`SetCompletedAsync(name, failedItemCount, failureDetail)` plus the underlying columns) now exists and is live in the database.
- Reminder for 02-03: `LastPartialFailureDetail` is a new credential/PII sink returned verbatim by `GET /api/worker/status` (T-02-07, flagged prohibition on this plan) — 02-03 must only compose non-secret named context (counts, game names, user ids) into it, never raw exception text, request URLs, or credential material.

## Self-Check: PASSED

- FOUND: GamePlanner.API/Entities/WorkerStatus.cs
- FOUND: GamePlanner.API/Managers/WorkerStatusManager.cs
- FOUND: Database/35_AlterWorkerStatus_AddFailureTracking.sql
- FOUND commit: 2015991

---
*Phase: 02-credential-secrets-hardening*
*Completed: 2026-08-30*
