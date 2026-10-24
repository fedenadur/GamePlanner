---
created: 2026-10-24T22:00:00.000Z
title: Image worker skips cover fetch when IGDBUpdatedAt is already set and CoverImageUrl is NULL
area: api
severity: minor
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
