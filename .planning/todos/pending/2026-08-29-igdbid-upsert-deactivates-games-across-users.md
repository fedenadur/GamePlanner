---
created: 2026-08-30T01:18:26.903Z
title: IGDBId upsert silently deactivates manually-added games
area: database
severity: major
files:
  - Database/33_RemoveUniqueIndex_IGDBId.sql
  - Database/34_FixStoredProcedure_UpsertIGDBGame.sql
  - GamePlanner.API/Managers/GameManager.cs:170
---

## Problem

Found during the Phase 1 (Angular EOL upgrade) manual UAT walkthrough — pre-existing, NOT
introduced by that phase, and you already have an unapplied fix in progress.

Reproduced live: added two games manually via the "Add Game" IGDB search flow on a fresh test
account. Within minutes (before I could test the delete action), both games' `IsActive` flipped
to `0` in the `Games` table on their own — confirmed via direct SQL (`SELECT Id, Name, UserId,
IsActive FROM Games WHERE Id IN (...)`). This made the delete button 404 (the delete endpoint's
own logic is correct — it only found the rows already inactive), and the frontend didn't surface
any user-facing message when that happened (just an unhandled `HttpErrorResponse` in the console).

Root cause: the currently-*applied* `sp_UpsertIGDBGame` and a unique index on `Games.IGDBId` don't
scope by `UserId`, so the background `GameSyncWorker`'s periodic catalog sync (used for the
Calendar / "Upcoming Releases" feature) collides with per-user rows that share the same IGDB
title and silently deactivates/overwrites them.

You already have the fix drafted but not yet run:
- `Database/33_RemoveUniqueIndex_IGDBId.sql` — drops the unique index on `IGDBId`, replaces with
  a non-unique index for query performance.
- `Database/34_FixStoredProcedure_UpsertIGDBGame.sql` — rewrites `sp_UpsertIGDBGame` to key off
  `(IGDBId, UserId)` instead of `IGDBId` alone.

## Solution

Apply scripts 33 and 34 in SSMS (per this project's "numbered scripts run manually" convention),
then re-verify: add a game manually, trigger `GameImageSyncWorker`/`GameSyncWorker` via the
Workers page, confirm the manually-added game's `IsActive` stays `1`. Separately worth a small
frontend fix: the delete action's error handler should surface a user-facing message instead of
only logging an unhandled `HttpErrorResponse` to the console.
