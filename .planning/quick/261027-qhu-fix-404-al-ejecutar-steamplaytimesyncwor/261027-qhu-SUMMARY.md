---
phase: quick-261027-qhu
plan: 01
subsystem: api-workers
tags: [workers, bugfix, run-now]
requires: []
provides:
  - "POST api/worker/SteamPlaytimeSyncWorker/run accepted (202) instead of 404"
affects:
  - GamePlanner.API/Controllers/WorkerController.cs
  - e2e/tests/uat.spec.ts
tech-stack:
  added: []
  patterns: ["static allowlist + explicit if/else-if dispatch for run-now workers"]
key-files:
  created: []
  modified:
    - GamePlanner.API/Controllers/WorkerController.cs
    - e2e/tests/uat.spec.ts
decisions: []
metrics:
  duration: ~5min
  completed: 2026-10-27
status: complete
---

# Phase quick-261027-qhu Plan 01: Fix 404 on SteamPlaytimeSyncWorker run-now Summary

SteamPlaytimeSyncWorker added to the WorkerController `KnownWorkers` allowlist and `RunWorkerAsync` dispatch, so "Run now" on the Workers page no longer returns 404.

## Tasks

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Wire SteamPlaytimeSyncWorker into WorkerController run-now | ba7bc88 | GamePlanner.API/Controllers/WorkerController.cs |
| 2 | Correct e2e comment about SteamPlaytimeSyncWorker trigger | fcd0b21 | e2e/tests/uat.spec.ts |

## Verification

- `dotnet build GamePlanner.sln`: failed with MSB3027/MSB3021 because the user's running API (GamePlanner.API, PID 31012) locks `bin\Debug\net8.0\GamePlanner.API.exe`. The process was not touched.
- `dotnet build GamePlanner.API/GamePlanner.API.csproj -o <scratchpad>/build`: succeeded, 0 warnings, 0 errors.
- Static gates (STATIC_OK, COMMENT_OK): passed. The uat.spec.ts diff is 2 added / 2 removed comment lines.
- Live HTTP check (202 on SteamPlaytimeSyncWorker, 404 on NoSuchWorker, WorkerStatus transition): PENDING. `UAT_PASSWORD` is not set in the environment, and the running API is the old build. The user must restart the API, then run the UAT login and POST `/api/worker/SteamPlaytimeSyncWorker/run`.

## Deviations from Plan

None. The plan's documented fallback for a locked DLL was used (scratch output build, live check reported as pending).

## Known Stubs

None.

## Threat Flags

None. The allowlist remains a static set of compile-time constants (T-qhu-01); no credentials were used or written (T-qhu-03).

## Self-Check: PASSED

- WorkerController.cs and uat.spec.ts modified as described; commits ba7bc88 and fcd0b21 exist.
- Unrelated working-tree changes (GameImageManager.cs, game-detail/*, untracked folders) remain unstaged and untouched.
