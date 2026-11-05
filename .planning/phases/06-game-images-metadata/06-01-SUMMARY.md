---
phase: 06-game-images-metadata
plan: 01
subsystem: image-sync
tags: [igdb, covers, screenshots, xunit, sql-server, test-harness]
status: complete
requires: []
provides:
  - GamePlannerDB_Test throwaway database rebuilt by scripts/build-test-db.ps1
  - TestDatabase guard and helpers (ConnectionString, ApiHostConnectionString, CreateContext, CreateConfig, EnsureUserAsync, InsertProbeGameAsync, PurgeProbeRowsAsync)
  - IgdbStub (IgdbService on a RecordingHandler, route by path and body)
  - GameImageManager cover loop with Id cursor, no freshness skip, per-row retry, images before covers
affects: [06-02, 06-03, 06-04, 06-05, 06-06]
tech-stack:
  added: []
  patterns:
    - "DB tests run only on a database whose name ends in _Test (no default connection string)"
    - "In-process API host pinned to the guarded database (configuration and EF)"
key-files:
  created:
    - scripts/build-test-db.ps1
    - GamePlanner.Tests/Support/TestDatabase.cs
    - GamePlanner.Tests/Support/IgdbStub.cs
    - GamePlanner.Tests/GameImageManagerTests.cs
  modified:
    - GamePlanner.API/Managers/GameImageManager.cs
    - game-planner-ui/src/app/components/game-detail/game-detail.component.ts
    - game-planner-ui/src/app/components/game-detail/game-detail.component.html
    - game-planner-ui/src/app/components/game-detail/game-detail.component.css
    - GamePlanner.Tests/Support/ApiFactory.cs
    - GamePlanner.Tests/GameManagerAtomicityTests.cs
    - GamePlanner.Tests/SteamAuthServiceTests.cs
key-decisions:
  - "D-01: SaveCoverAsync writes every cover IGDB returns for a NULL-cover row, with no timestamp comparison (both the by-id and the name-search paths)"
  - "D-02: Id > lastId cursor in the cover loop, so each NULL-cover row is requested once per run"
  - "D-28: rows of a failed cover batch are fetched one by one in the same run; a row whose retry fails stays NULL and is counted in FailedCount and named in FailureDetail"
  - "RunAsync runs the screenshot/artwork phase before the cover phase, because sp_UpdateGameCover stamps LastImageSyncDate"
  - "D-18, D-23, D-25: all DB tests and the ApiFactory host are pinned to GamePlannerDB_Test; test-arrange raw writes run under the D-23 exemption"
requirements-completed: [IMG-01, IMG-02]
duration: 10 min
completed: 2026-11-05
---

# Phase 6 Plan 01: Image sync fix and GamePlannerDB_Test harness Summary

Cover sync now fetches every NULL cover once per run (Id cursor, no timestamp skip, per-row retry after a failed batch, images before covers), proven on a guarded throwaway SQL Server database that every DB test and the in-process API host now use.

## Tasks and commits

| Task | Commit | Result |
|------|--------|--------|
| 1 (tracer): land the WIP | `d978732` feat IMG-02 WIP, `c2e3d48` feat UI-01 WIP | Two exact-path commits. `dotnet build GamePlanner.sln`, `npm run test:ci` (8/8) and `ng build --configuration development` passed on the committed WIP |
| 2: test harness | `879c839` | `build-test-db.ps1`, `TestDatabase`, pinned `ApiFactory`, migrated `GameManagerAtomicityTests` and `SteamAuthServiceTests` |
| 3a: WIP proof | `a6fad37` | `IgdbStub` and `SharedIgdbId_SavesImagesForEveryLocalGame` (passed against the committed WIP, so no WIP defect) |
| 3b: RED | `215fdb5` | Six cover-sync facts; five failed for the expected reasons (T1b passes by design, it guards a regression) |
| 3c: GREEN | `c64635c` | `GameImageManager` changes; 69/69 tests pass on `GamePlannerDB_Test` |

## WIP review notes

The backend hunk was exactly the two planned parts (`GroupBy(...).ToDictionary` to `Dictionary<long, List<int>>`, and `sp_UpsertGameImage` once per local id). The lightbox ts/html/css has `lightboxIndex`/`openLightbox(i)`, modulo-wrapped `nextImage`/`prevImage` guarded for length <= 1, the `window:keydown` HostListener, `preloadNeighbors()` and the 20 ms `imageFading` toggle. Left unchanged. Nothing was stashed, checked out or reset; the unrelated untracked paths stay untracked.

The tracer's `<verify>` is fully automated and passed. Because `workflow.human_verify_mode` is `end-of-phase` and auto mode is off, no mid-flight human checkpoint was raised; the lightbox is a visual item for the end-of-phase UAT.

## build-test-db.ps1

Ran clean on an empty database: all numbered scripts applied in ordinal order (17_ before 17b_), including `38_FixHalfLifeIgdbId.sql`, so no script needed an exception list. One fix to my own script during the run: the final `UPDATE Games SET IsActive = 0` also needs `sqlcmd -I`, because the filtered indexes require QUOTED_IDENTIFIER. After the build, `SELECT COUNT(*) FROM Games WHERE IsActive = 1` returns 0 and `TEST_DB_READY=GamePlannerDB_Test` prints.

## Schema diff

`INFORMATION_SCHEMA.COLUMNS` plus `sys.procedures` on `GamePlannerDB` (read-only) versus `GamePlannerDB_Test`: 136 lines each, no difference at all. No drift on the tested objects.

## D-28 per-row retry (Task 3 step 6)

A `batchFailed` flag is set in the batch catch. When set, each `withId` row is fetched alone through `FetchCoversByIdsAsync([id])` in its own try/catch, with a 250 ms delay. A row whose retry fails stays NULL, increments `failedCount` and its name goes into `failedNames`. T1e result: the cover for P is saved, Q stays NULL, exactly 3 cover requests reference P or Q (batch, P, Q), and `RunAsync` returns FailedCount 1 with Q's name in FailureDetail.

## Tests (all on GamePlannerDB_Test)

- `GameImageManagerTests` (7 facts, pass): `SharedIgdbId_SavesImagesForEveryLocalGame`, `NullCover_WithIgdbUpdatedAtSet_GetsCover_EvenWhenIgdbTimestampIsNotNewer` (older and equal timestamps), `RowWithCover_IsNeverRequestedForACover`, `CoverLoop_RequestsEachRowOnce_WhenIgdbHasNoCovers` (3 requests for 120 rows, ascending-Id slices of 50), `NewlyCoveredRow_GetsScreenshotsInTheSameRun`, `CoverBatchFailure_RetriesEachRowOnItsOwn`, `NullCoverWithoutIgdbId_UsesNameSearch_AndGetsCover`.
- `GameManagerAtomicityTests` (migrated) and `SteamAuthServiceTests` (migrated, plus `ApiHost_NeverReachesTheOwnerDatabase`) pass; `AuthTokenTests` pass. Full solution run: 69 passed, 0 failed.
- Guard proof with `Database=GuardProbe_NotTest`: the atomicity filter fails with "must name a database ending in _Test" before any connection, and `ApiHost_NeverReachesTheOwnerDatabase` passes with the API host on `TestDbGuard_Unconfigured`. No `dotnet test` run named the owner's database, and no file under `GamePlanner.Tests` contains `Database=GamePlannerDB;`.

## Deviations from Plan

**1. [Rule 3 - Blocking] `sqlcmd -I` on the deactivation UPDATE.** Found while running `build-test-db.ps1`: Msg 1934 (QUOTED_IDENTIFIER) on the final `UPDATE Games SET IsActive = 0`. Fix: added `-I`. File: `scripts/build-test-db.ps1`, committed in `879c839`.

No other deviations. The configuration pin via `UseSetting` was sufficient (the guard fact passes), so the `ConfigureAppConfiguration` fallback was not needed.

## Known Stubs

None.

## Threat Flags

None. The new surface is the test harness (guarded) and the build script (guarded on `_Test$`).

## Notes for later plans

- The dev API was not running, and was not started. No worker was triggered, and `GamePlannerDB` was only read (schema parity query).
- Environment variables do not persist between Bash calls in this harness: export `ConnectionStrings__DefaultConnection` in the same command as `dotnet test`.

## Self-Check: PASSED

Files found: `scripts/build-test-db.ps1`, `GamePlanner.Tests/Support/TestDatabase.cs`, `GamePlanner.Tests/Support/IgdbStub.cs`, `GamePlanner.Tests/GameImageManagerTests.cs`. Commits found in `git log`: `d978732`, `c2e3d48`, `879c839`, `a6fad37`, `215fdb5`, `c64635c`.
