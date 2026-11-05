---
phase: 06-game-images-metadata
plan: 05
subsystem: verification
tags: [restart, workers, igdb, matcher, live-proof]
status: in-progress
requires: [06-01, 06-02, 06-03, 06-04]
affects: [06-06]
requirements: [IMG-01, IMG-02, IMG-03]
---

# Phase 6 Plan 05: Restart and live data proof Summary

Status: PARTIAL. Task 1 (D-24 checkpoint) is resolved as `restart`. The Task 2 pre-start part (confirm apply, idempotency dry run, fingerprints) is done and recorded below. The dev stack has NOT been started yet; the orchestrator performs the single start, then a continuation agent finishes Task 2 (post-start) and Task 3.

## Restart confirmation (D-24)

- Option: `restart`
- Date: 2026-11-05
- Owner reply: "restart" (the owner confirmed knowing the stack is currently down; this is one controlled start of the API and the Angular UI)
- Accepted effects: the startup worker runs process their rows as described at the checkpoint (14 of 22 NULL-IGDBId rows matched, 8 stay NULL; 24 NULL-cover rows get image/cover attempts; no force; no re-match of rows that already have an IGDBId).
- Note: the orchestrator (not the executor) performs the start so the processes outlive this agent for plan 06-06. The executor started and stopped nothing and triggered no worker.
- 06-02 precondition re-read: `apply-user`, owner "applied" reply, `SIBLING_PAIR=107697,107698 igdb=2369`, `SIBLING_IGDB_ARTWORKS=1`, `NIER_IGDB_COVER=no` are all recorded in 06-02-SUMMARY.md. Code precondition: main checkout on `main` contains `FetchExternalGamesBySteamAppIdAsync`/`ImagePageSize` (IgdbService), `gameId` (GameImagesController), `g.Id > lastId`/`FetchAllImagePagesAsync` (GameImageManager).

## Apply confirmation (read-only, 2026-11-05 before start)

```
Games 107693: IGDBId 71 | FirstReleaseDate NULL | CoverImageUrl NULL | IGDBUpdatedAt NULL | LastImageSyncDate NULL | ModifiedAt 2026-11-05 17:39:09.19
GameTags 107693: TagId 15 only
```

No pre-existing API process ran its timer since the apply (cleared columns still NULL, no process listening on 4200, 7174, 5127).

## Idempotency dry run (rolled back, never committed)

COMMIT line stripped, abort-before-sqlcmd guard ran (no `commit` in the SQL text), body inside `BEGIN TRANSACTION ... ROLLBACK TRANSACTION`:

```
IDEMPOTENT_CHANGED=0
```

## Pre-restart fingerprints

Captured read-only on GamePlannerDB 2026-11-05 ~17:46Z, before any start. The PREVIEW coverage gate passed: all 22 Ids in `NULL_IGDB_IDS` have exactly one `PREVIEW` line in 06-02-SUMMARY.md (note: that file has CRLF line endings, so `grep` with `$` anchors needs `tr -d '\015'` first).

```
MAXID=108269
COVER_FP=465|1141247152
IGDB_FP=474|404388
NULL_COVER_IDS=107693,107697,107698,107715,107717,107725,107787,107866,107889,107890,107899,107930,107932,107952,107988,108052,108077,108079,108080,108081,108099,108121,108158,108204
NULL_IGDB_IDS=107697,107698,107715,107717,107725,107787,107866,107890,107899,107930,107932,107952,107988,108052,108077,108079,108080,108081,108099,108121,108158,108204
HALFLIFE_PRE 107683 https://images.igdb.com/igdb/image/upload/t_cover_big/coc2ea.jpg
HALFLIFE_PRE 107687 https://images.igdb.com/igdb/image/upload/t_cover_big/co1nmw.jpg
```

Plain evidence:

```
NieR 107889: UserId 2 | IGDBId 391942 | CoverImageUrl NULL | IGDBUpdatedAt 2026-06-11 01:10:25 | LastImageSyncDate 2026-11-05 16:09:49.183
Sibling pair: 107697 and 107698 (UserId 2): IGDBId NULL | CoverImageUrl NULL | IGDBUpdatedAt NULL | LastImageSyncDate NULL
UAT account (UserId 17): active games with IGDBId 231 = 0 (repro row may use 231)
WorkerStatus (pre-start): GameSyncWorker Completed 2026-11-05 16:16:20 | GameImageSyncWorker Completed 2026-11-05 16:16:20 | GameHLTBSyncWorker Completed 16:16:20 | UpcomingGamesSyncWorker Completed 16:16:20 | SteamPlaytimeSyncWorker Completed 16:16:20 | SteamTokenCleanupWorker Completed 16:46:20
```

Both startup-run waits must see `LastRunAt` strictly after the recorded `UAT_API_STARTED_AT`.

## Remaining work (continuation agent)

After the orchestrator starts the stack: record `UAT_API_STARTED_AT`, wait for both startup worker runs (Completed, NULL LastError), Portal assertions (`## Portal (IMG-03)`), then Task 3 (`## D-17 live verification`, `REPRO_ID`, `06-05-repro-precondition.txt`, NieR record, retry if needed), then state/roadmap updates.
