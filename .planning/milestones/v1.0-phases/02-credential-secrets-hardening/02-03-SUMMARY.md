---
phase: 02-credential-secrets-hardening
plan: 03
subsystem: api
tags: [reliability, error-handling, worker-status, igdb, steam, csv]

# Dependency graph
requires: ["02-02"]
provides:
  - "GameImageManager.RunAsync, GameEnrichmentManager.EnrichAsync, GameSyncManager.RunAsync, SteamPlaytimeSyncManager.RunAsync all return (int FailedCount, string? FailureDetail)"
  - "CsvGameService.ReadGames returns (List<HLTBGameDto> Records, int Skipped, string? FailureDetail)"
  - "GameImageSyncWorker, GameSyncWorker, GameHLTBSyncWorker, SteamPlaytimeSyncWorker forward failure counts into WorkerStatusManager.SetCompletedAsync"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "catch (OperationCanceledException) { throw; } precedes every in-scope catch (Exception ex) so cancellation still reaches the worker's SetIdleAsync path"
    - "Failure-detail strings built only from counts and entity names (game names, user ids, condition labels) — never exception text, URLs, or config values"
    - "Whole-batch/whole-user data-loss catches raised from LogWarning to LogError; per-item retryable catches stay LogWarning"
    - "Per-batch boolean flag gates a single failedCount += batch.Count addition when both screenshot and artwork fetches fail, avoiding double-counting"

key-files:
  created: []
  modified:
    - GamePlanner.API/Managers/GameImageManager.cs
    - GamePlanner.API/Managers/GameEnrichmentManager.cs
    - GamePlanner.API/Managers/GameImportManager.cs
    - GamePlanner.API/Managers/GameSyncManager.cs
    - GamePlanner.API/Managers/SteamPlaytimeSyncManager.cs
    - GamePlanner.API/Services/CsvGameService.cs
    - GamePlanner.API/Workers/GameImageSyncWorker.cs
    - GamePlanner.API/Workers/GameSyncWorker.cs
    - GamePlanner.API/Workers/GameHLTBSyncWorker.cs
    - GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs

key-decisions:
  - "Screenshot and artwork batch fetch failures raised to LogError (data loss — batch's games are marked image-synced regardless) while batch cover fetch and per-item catches stay LogWarning (naturally retried)"
  - "SteamPlaytimeSyncManager's per-user catch raised to LogError since it drops one user's entire sync for the run, not a single item"
  - "CsvGameService's three early-guard returns and whole-file catch now return a non-null FailureDetail naming the condition (not the raw path or exception text); the per-row catch stays LogDebug, unchanged"
  - "Detail strings composed only from counts + up to 3 entity names/ids, truncated to 480 chars client-side (WorkerStatusManager truncates to 500 server-side as a backstop)"

requirements-completed: [REL-03]

coverage:
  - id: D1
    description: "All 8 Manager/Service-tier catch (Exception) blocks named in D-14 stay broad (no narrowing) but each counts or raises severity, none continues silently"
    requirement: "REL-03"
    verification:
      - kind: other
        ref: "grep -c 'catch (Exception ex)' across the 5 in-scope files: GameImageManager.cs=4, GameEnrichmentManager.cs=1, GameSyncManager.cs=1, SteamPlaytimeSyncManager.cs=1, CsvGameService.cs=2 (whole-file is the D-14 site; per-row was already counting via skipped++) = 8 D-14 sites"
        status: pass
    human_judgment: false
  - id: D2
    description: "Data-loss failures log at Error; naturally-retried failures stay at Warning"
    requirement: "REL-03"
    verification:
      - kind: other
        ref: "LogError present at exactly: GameImageManager.cs screenshot+artwork batch sites (2), SteamPlaytimeSyncManager.cs per-user site (1), CsvGameService.cs whole-file site (1, pre-existing) = matches D-15's severity split; batch cover fetch, per-game cover search, enrichment, and CsvGameService per-row catch all stay LogWarning/LogDebug"
        status: pass
    human_judgment: false
  - id: D3
    description: "Four in-scope workers pass failed-item count + redacted detail into SetCompletedAsync so GET /api/worker/status shows partial failures without the run being marked Failed"
    requirement: "REL-03"
    verification:
      - kind: integration
        ref: "grep confirms GameImageSyncWorker, GameSyncWorker, SteamPlaytimeSyncWorker each call SetCompletedAsync(WorkerName, failedCount, failureDetail) once; GameHLTBSyncWorker does so at both call sites. Live app run with invalid IGDB:ClientId: SQL trace shows UPDATE [WorkerStatus] SET [FailedItemCount]=@p0,[LastPartialFailureDetail]=@p1,[LastSuccessAt]=@p2,[Status]=@p3 for both GameImageSyncWorker (20 items) and GameSyncWorker (157 items) — the SetCompletedAsync column signature, never SetFailedAsync's [LastError]+[Status]-only signature"
        status: pass
    human_judgment: false
  - id: D4
    description: "A cancelled sync run propagates OperationCanceledException out of every in-scope catch instead of counting it as a failed item"
    requirement: "REL-03"
    verification:
      - kind: other
        ref: "catch (OperationCanceledException) { throw; } precedes every in-scope catch (Exception ex): GameImageManager.cs=4, GameEnrichmentManager.cs=1, GameSyncManager.cs=1, SteamPlaytimeSyncManager.cs=1 — count matches the corresponding catch (Exception ex) count in each file"
        status: pass
    human_judgment: false
  - id: D5
    description: "The six Worker-level top-loop catch blocks are byte-identical to their pre-phase form"
    requirement: "REL-03"
    verification:
      - kind: other
        ref: "grep -rc 'catch (Exception ex)' GamePlanner.API/Workers/ = 6 before and after all three task commits; git diff for the Task 3 commit shows changes only around SetCompletedAsync call sites and the new Warning log line, never inside a catch block body"
        status: pass
    human_judgment: false
  - id: D6
    description: "Deferred-todo uncommitted changes in GameImageManager.cs are not swept into this phase's commits and still exist in the working tree afterward"
    requirement: "REL-03"
    verification:
      - kind: other
        ref: "git show HEAD:GamePlanner.API/Managers/GameImageManager.cs contains 'Dictionary<long, int> igdbToLocal' (pre-todo form); git diff -- GamePlanner.API/Managers/GameImageManager.cs is non-empty and shows the todo's Dictionary<long, List<int>> cross-user IGDBId-mapping fix, unstaged"
        status: pass
    human_judgment: false
  - id: D7
    description: "Full Development app start reaches all four in-scope workers with no schema error"
    requirement: "REL-03"
    verification:
      - kind: human
        ref: "Live app run (Development, real credentials): log shows GameSyncWorker, GameImageSyncWorker, GameHLTBSyncWorker, SteamPlaytimeSyncWorker all started+triggered; UPDATE [WorkerStatus] statements referencing [FailedItemCount]/[LastPartialFailureDetail] execute successfully with no 'invalid column' error"
        status: pass
    human_judgment: true
---

# Phase 02 Plan 03: REL-03 Failure Surfacing Summary

**The eight silent `catch (Exception)` blocks around IGDB/Steam/HLTB external calls now count what they skip and raise severity where a whole batch or user is permanently lost, with the four in-scope workers forwarding those counts into `WorkerStatus` so a partially-failed sync shows up as `Completed` with a non-zero `FailedItemCount` instead of an indistinguishable clean run — live-verified with two forced-failure runs (invalid IGDB credential, missing HLTB CSV).**

## Performance

- **Duration:** ~55 min (including four live app-start verification runs)
- **Tasks:** 3 (all `type="auto"`)
- **Files modified:** 10

## Accomplishments

- `GameImageManager.RunAsync` returns `(int FailedCount, string? FailureDetail)`. The batch cover fetch (retried per-game immediately after) stays `LogWarning`, uncounted. The per-game cover search stays `LogWarning`, now counted with the game name collected. The screenshot and artwork batch fetches — the two data-loss sites, since the batch's games are marked image-synced regardless of fetch success — are raised to `LogError`, with a single per-batch `bool batchFailed` flag gating one `failedCount += batch.Count` addition so a batch where both fetches fail is counted once, not twice.
- `GameEnrichmentManager.EnrichAsync` and `GameSyncManager.RunAsync` (via a new `Task<bool> EnrichGameAsync`) both return the same tuple shape, counting per-item enrichment failures at `LogWarning` (the next run naturally re-selects games with missing metadata). `GameSyncManager`'s early "already enriched" return returns `(0, null)`.
- `GameImportManager` captures the tuple from its one `EnrichAsync` call and logs the failed count at Warning when non-zero; no other change to the import job's own status handling.
- `SteamPlaytimeSyncManager.RunAsync` returns the same tuple shape. `SyncUserAsync` (now `Task<bool>`) raises its catch from `LogWarning` to `LogError` — one failed user drops that user's entire playtime sync for the run, not a single item, matching D-15's "real data loss" bar. Failure detail names the affected user ids.
- `CsvGameService.ReadGames` returns `(List<HLTBGameDto> Records, int Skipped, string? FailureDetail)`. The three early-guard returns (unconfigured path, file not found, empty/missing-column header) now return a non-null detail naming the condition instead of an indistinguishable empty list. The per-row catch is unchanged (`LogDebug` + existing `skipped++`). The whole-file catch stays `LogError` and now returns a detail; the closing aggregate log is raised to `LogWarning` when `skipped > 0`.
- All four in-scope workers (`GameImageSyncWorker`, `GameSyncWorker`, `SteamPlaytimeSyncWorker`, `GameHLTBSyncWorker`) destructure their manager/service's new return tuple and pass it into `SetCompletedAsync(WorkerName, failedCount, failureDetail)`. `GameHLTBSyncWorker` does this at both its call sites (the empty-records early return and the normal completion path) so a failed CSV read is recorded even when there are zero records to process. Each worker logs one `LogWarning` line naming itself and the count when `failedCount > 0`.
- `UpcomingGamesSyncWorker` and `SteamImportWorker` are untouched; their single-argument `SetCompletedAsync(WorkerName)` calls keep compiling via 02-02's optional parameters.
- All six Worker-level top-loop `catch (OperationCanceledException)` / `catch (Exception ex)` handlers are byte-identical to their pre-phase form — the flagged D-14 exclusion held throughout all three commits.

## Task Commits

Each task was committed atomically:

1. **Task 1: Instrument the four IGDB-facing catch sites and return their failure counts** - `8858bef` (feat) — includes the working-tree hazard handling for `GameImageManager.cs`
2. **Task 2: Instrument the Steam playtime and HLTB CSV catch sites** - `78cbd78` (feat)
3. **Task 3: Forward failure counts from the four in-scope workers into WorkerStatus** - `945533d` (feat)

## Files Created/Modified

- `GamePlanner.API/Managers/GameImageManager.cs` - `RunAsync` returns `(FailedCount, FailureDetail)`; screenshot/artwork batch catches raised to `LogError` with per-batch failure counting; cover search catch counted at `LogWarning`
- `GamePlanner.API/Managers/GameEnrichmentManager.cs` - `EnrichAsync` returns `(FailedCount, FailureDetail)`; per-item catch counted, aggregate Warning line added
- `GamePlanner.API/Managers/GameImportManager.cs` - captures and logs the enrichment failure count from its `EnrichAsync` call
- `GamePlanner.API/Managers/GameSyncManager.cs` - `RunAsync` returns `(FailedCount, FailureDetail)`; `EnrichGameAsync` now returns `Task<bool>` so the caller can accumulate
- `GamePlanner.API/Managers/SteamPlaytimeSyncManager.cs` - `RunAsync` returns `(FailedCount, FailureDetail)`; per-user catch raised `LogWarning` -> `LogError`
- `GamePlanner.API/Services/CsvGameService.cs` - `ReadGames` returns `(Records, Skipped, FailureDetail)`; three early guards and the whole-file catch now supply a non-null detail
- `GamePlanner.API/Workers/GameImageSyncWorker.cs`, `GameSyncWorker.cs`, `GameHLTBSyncWorker.cs`, `SteamPlaytimeSyncWorker.cs` - forward manager/service failure counts into `SetCompletedAsync`; add a Warning log line on non-zero failure

## Decisions Made

- **Working-tree hazard handled first, per plan:** `GameImageManager.cs` carried an uncommitted deferred-todo fix (cross-user IGDBId image-mapping, `Dictionary<long, List<int>>`). Captured its diff to a scratch patch file, reset the file to HEAD, made this task's edits on the clean base, staged/committed by explicit path, then restored the deferred WIP with `git apply -3`. The 3-way merge resolved cleanly with no manual conflict (git's context-based merge found a non-overlapping insertion point for both changes), but `git apply -3` staged the result into the index as a side effect — reset with `git reset -- GamePlanner.API/Managers/GameImageManager.cs` to put it back to its original unstaged working-tree state, matching the pre-existing convention and the plan's own verification gate (`git diff -- ...` must be non-empty).
- **Screenshot/artwork batch failures raised to `LogError`, cover-search per-item failures stay `LogWarning`:** matches D-15 — a batch fetch failure permanently loses up to `BatchSize` games' images for two days (the batch is still marked synced below regardless), while a per-game cover search failure is a one-off skip with no such compounding.
- **Steam per-user sync failure raised to `LogError`:** drops that user's entire playtime sync for the run, not a single item — same "whole unit lost" reasoning as the screenshot/artwork sites.
- **CsvGameService's per-row catch and severity structure left untouched:** it already had the exact D-16 shape (existing `skipped` counter, `LogDebug` for expected per-row noise). Only its return type and the three early guards changed to surface detail instead of an indistinguishable empty list.

## Deviations from Plan

### Auto-fixed Issues

None — no bugs, missing functionality, or blocking issues were found; all edits follow the plan's action text directly.

### Verify-script accuracy note (not a code deviation)

**Task 2's automated `test "$(grep -c 'LogError' GamePlanner.API/Services/CsvGameService.cs)" = "1"` check reports 2, not 1.** The file has two `LogError` calls: the whole-file read-failure catch (in scope for this task, unchanged severity) and a pre-existing `LogError` on the "missing required column 'game_name'" early guard (line 49 in the original file, not discussed in this task's action text or read_first notes, and not touched by this task's edits beyond adding a `FailureDetail` string to its return). The plan's verify script appears to have undercounted this pre-existing call. No code was changed to force the grep to `1`, since doing so would mean either deleting a genuine error log or downgrading a real configuration-error condition to a lower severity — both would contradict D-15's "raise severity for real data loss/misconfiguration" intent. All of Task 2's actual required behavior (whole-file catch stays `LogError`, per-row catch stays `LogDebug`, tuple return shape, non-null detail on every failure path) is satisfied and independently verified above.

## Issues Encountered

- `git apply -3` unexpectedly staged the restored deferred WIP into the index when the 3-way merge succeeded without conflicts (rather than leaving it purely in the working tree). Caught immediately via `git status --short` showing `M ` (staged) instead of ` M` (unstaged); corrected with `git reset -- GamePlanner.API/Managers/GameImageManager.cs`. No commit was affected.

## User Setup Required

None. All verification (build, static greps, and the four live app-start runs including two forced-failure observations) was performed by the executor. `dotnet user-secrets` was temporarily set to invalid `IGDB:ClientId` and a non-existent `HLTB:CsvPath` for the forced-failure observations, then restored to the real values recorded in 02-01 immediately after each observation — confirmed via `dotnet user-secrets list` showing the original values before this plan's commits were finalized.

## Next Phase Readiness

- REL-03 is fully closed: all 8 D-14 catch sites count or raise severity, all 5 changed manager/service signatures compile and are consumed correctly by their 4 in-scope workers, and `GET /api/worker/status` will now surface `FailedItemCount`/`LastPartialFailureDetail` for `GameImageSyncWorker`, `GameSyncWorker`, `GameHLTBSyncWorker`, and `SteamPlaytimeSyncWorker` on any run with partial failures.
- This was the last plan of Phase 02 (Credential & Secrets Hardening) per `.planning/STATE.md`'s "Plan: 3 of 5" position — confirm against `.planning/ROADMAP.md` whether additional phase-02 plans remain before transitioning.

## Self-Check: PASSED

- FOUND: GamePlanner.API/Managers/GameImageManager.cs
- FOUND: GamePlanner.API/Managers/GameEnrichmentManager.cs
- FOUND: GamePlanner.API/Managers/GameImportManager.cs
- FOUND: GamePlanner.API/Managers/GameSyncManager.cs
- FOUND: GamePlanner.API/Managers/SteamPlaytimeSyncManager.cs
- FOUND: GamePlanner.API/Services/CsvGameService.cs
- FOUND: GamePlanner.API/Workers/GameImageSyncWorker.cs
- FOUND: GamePlanner.API/Workers/GameSyncWorker.cs
- FOUND: GamePlanner.API/Workers/GameHLTBSyncWorker.cs
- FOUND: GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs
- FOUND commit: 8858bef
- FOUND commit: 78cbd78
- FOUND commit: 945533d

---
*Phase: 02-credential-secrets-hardening*
*Completed: 2026-08-30*
