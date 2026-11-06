---
created: 2026-10-24T22:00:00.000Z
title: Image worker skips cover fetch when IGDBUpdatedAt is already set and CoverImageUrl is NULL
area: api
severity: minor
resolves_phase: 6
files:
  - GamePlanner.API/Managers/GameImageManager.cs
  - GamePlanner.API/Managers/GameSyncManager.cs
---

## Problem

Found in Phase 5 plan 05-02, after script 38 re-enriched Half-Life (107683) and Half-Life 2 (107687).

`GameImageManager.SaveCoverAsync` skips a game with "Cover up-to-date" when
`igdbUpdatedAt <= currentUpdatedAt`, even if `CoverImageUrl` is NULL. `GameSyncWorker` stamps
`IGDBUpdatedAt` from IGDB `updated_at` before `GameImageSyncWorker` runs, so a row whose cover was
cleared (as script 38 did) is selected for a cover fetch (`CoverImageUrl IS NULL`) but then
skipped. After a non-force sync run followed by a non-force image run, both rows kept
`CoverImageUrl = NULL` (`LastImageSyncDate` was set; screenshots and artworks did sync).

Plan 05-06 triggers these workers again, so the covers will not self-heal there either.

## Solution / Next steps

1. Smallest data workaround (needs the owner's go-ahead, it writes live rows):
   `UPDATE Games SET IGDBUpdatedAt = NULL WHERE Id IN (107683,107687);` then a non-force
   `GameImageSyncWorker` run.
2. App fix, when feature work resumes: in `SaveCoverAsync`, only apply the freshness skip when the
   game already has a cover (the batch query should also select `CoverImageUrl`).

## Resolution

Resolved in Phase 6 (06-game-images-metadata), 2026-11-05.

- Fix commits (plan 06-01): `c64635c` fix(06-01): fetch every NULL cover once per run (D-01 freshness skip removed, D-02 id cursor), retry failed batches per row (D-28), sync images before covers; failing tests first in `215fdb5`.
- Tests T1a-T1f: the cover-sync tests on GamePlannerDB_Test (`215fdb5` red, `c64635c` green), harness in `879c839`.
- Live proof (D-22): `REPRO_OK` (06-05, recomputed live): UAT repro row 108276 reached the old-bug state (cover NULL, IGDBUpdatedAt set) after a non-force GameSyncWorker run, then received a `t_cover_big` cover and 12 screenshots from one non-force GameImageSyncWorker run. `REPRO_PAGE_OK` (06-06): the same row's game-detail page shows the `t_cover_big` cover and 12 carousel items (row since soft-deleted).
- Regression checks only: `HALFLIFE_OK` (107683 and 107687 keep their covers) and `COVERS_UNCHANGED`. NieR 107889 is not counted (IGDB game 391942 has no cover).
- Full UAT suite: 18 passed, `SUITE_EXIT=0` (06-06-full-suite.log).
- The proposed data workaround (`UPDATE Games SET IGDBUpdatedAt = NULL`) was not used (D-03, superseded by D-22 for the proof rows).
