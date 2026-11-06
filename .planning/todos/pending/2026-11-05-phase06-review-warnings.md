---
created: 2026-11-05T19:30:00.000Z
title: Address Phase 06 code review warnings WR-01..WR-05
area: api
severity: minor
files:
  - GamePlanner.API/Managers/GameImageManager.cs
  - GamePlanner.API/Managers/GameSyncManager.cs
  - Database/11_AlterGames_AddImageColumns.sql
  - e2e/tests/uat.spec.ts
  - scripts/build-test-db.ps1
---

## Problem

Advisory findings from `.planning/phases/06-game-images-metadata/06-REVIEW.md` (none breaks a Phase 6 criterion):

- WR-01: failed screenshot/artwork fetch still stamps `LastImageSyncDate` for the batch (2-day delay); needs an Id cursor if failed batches stop being stamped.
- WR-02: `sp_UpdateGameCover` writes `IGDBUpdatedAt`, so enrichment may skip rows that have a cover but lack metadata.
- WR-03: Steam-mapping hit + null IGDB fetch falls through to first-hit name search (can persist a wrong IGDBId); a mapping lookup failure aborts the row. Related: 2026-10-27-igdbid-suspect-rematch.
- WR-04: e2e `uat-lightbox-fixture` game is never cleaned up.
- WR-05: `build-test-db.ps1` `_Test` guard is weak and the name is interpolated unescaped.
