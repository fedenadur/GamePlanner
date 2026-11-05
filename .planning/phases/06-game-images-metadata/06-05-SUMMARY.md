---
phase: 06-game-images-metadata
plan: 05
subsystem: verification
tags: [restart, workers, igdb, matcher, live-proof]
status: complete
duration: ~65 min (across two executor sessions plus the orchestrator start)
completed: 2026-11-05
key-decisions:
  - "Restart approved by owner at D-24 (restart); workers ran once at API start plus one non-force run each for the repro row"
  - "SC1 proven by REPRO_OK on the UAT repro row 108276 (D-22); NieR not counted (IGDB has no cover); Half-Life only a regression check"
key-files:
  created:
    - .planning/phases/06-game-images-metadata/06-05-repro-precondition.txt
    - .planning/phases/06-game-images-metadata/06-05-nier-igdb.txt
requires: [06-01, 06-02, 06-03, 06-04]
affects: [06-06]
requirements: [IMG-01, IMG-02, IMG-03]
---

# Phase 6 Plan 05: Restart and live data proof Summary

Status: COMPLETE. The owner-approved restart ran the workers on all rows; the live proof for IMG-01 (repro row), IMG-02 (Dark Messiah pair), IMG-03 (Portal) and the matcher all hold, recomputed live with SELECT-only SQL. No retry round was needed. The stack (API https://localhost:7174, UI http://localhost:4200) is left running for plan 06-06.

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


## Restart record

UAT_API_STARTED_AT=2026-11-05T17:46:58Z

## Startup worker runs

After UAT_API_STARTED_AT, polled WorkerStatus every 10 s. Both startup runs finished before ~17:49:49Z:

```
GameSyncWorker      Completed LastRunAt 2026-11-05T17:47:16.3664366 LastError NULL
GameImageSyncWorker Completed LastRunAt 2026-11-05T17:47:16.3664275 LastError NULL
```

## Portal (IMG-03)

```
Id 107693 | IGDBId 71 | FirstReleaseDate 2007-10-10 | CoverImageUrl https://images.igdb.com/igdb/image/upload/t_cover_big/coay61.jpg | IGDBUpdatedAt 2026-11-05 13:03:42 | LastImageSyncDate 2026-11-05 17:47:18.810 | GameGenres 3 | screenshots 5 | GameTags {15}
PORTAL_OK
```

All values were re-derived by the workers from IGDB 71; nothing was hand-written.

## D-17 live verification

Repro row (UAT account, UserId 17, IGDB 231; the UAT account owned no active game with IGDBId 231 beforehand):

```
REPRO_ID=108276
REPRO_PRECONDITION=ok
```

States (dated live captures, SELECT only):

1. Created by POST /api/games/from-igdb (HTTP 201): `108276 | UserId 17 | IGDBId 231 | CoverImageUrl NULL | IGDBUpdatedAt NULL`.
2. After one non-force GameSyncWorker run (HTTP 202, LastRunAt 2026-11-05T17:50:45, Completed, LastError NULL), captured into `06-05-repro-precondition.txt`: `REPRO_PRE|108276|NULL|2026-11-05T07:50:25|2026-11-05T17:53:04.2138139`. Cover NULL and stamp set: exactly the state the old code skipped.
3. After one non-force GameImageSyncWorker run (HTTP 202, LastRunAt 2026-11-05T17:53:13, Completed, LastError NULL): see assertions below.

NIER=no-igdb-cover (not counted as proof, D-22)

Assertions (all recomputed live by the Task 3 verify, SELECT-only SQL against the recorded baselines; the verify ran against a CRLF-stripped copy of 06-02-SUMMARY.md because that file has CRLF line endings):

```
REPRO_OK
  108276 | UserId 17 | IGDBId 231 | CoverImageUrl https://images.igdb.com/igdb/image/upload/t_cover_big/coc2ea.jpg | IGDBUpdatedAt 2026-11-05 07:50:25 | LastImageSyncDate 2026-11-05 17:53:16.450 | screenshots 12 | artworks 2
  precondition file captured at 2026-11-05T17:53:04Z, before LastImageSyncDate (CAPTURED_BEFORE_IMAGE_RUN)
COVERS_UNCHANGED   now=465|1141247152  expected=465|1141247152
IGDBIDS_UNCHANGED  now=474|404388      expected=474|404388
MATCHER_OK         22 of 22 NULL_IGDB_IDS rows equal their PREVIEW line (14 assigned, 8 stay NULL)
  MATCH 107697 expected=2369 actual=2369
  MATCH 107698 expected=2369 actual=2369
  MATCH 107715 expected=73 actual=73
  MATCH 107717 expected=59 actual=59
  MATCH 107725 expected=392808 actual=392808
  MATCH 107787 expected=8965 actual=8965
  MATCH 107866 expected=NULL actual=NULL
  MATCH 107890 expected=NULL actual=NULL
  MATCH 107899 expected=91191 actual=91191
  MATCH 107930 expected=51525 actual=51525
  MATCH 107932 expected=54486 actual=54486
  MATCH 107952 expected=NULL actual=NULL
  MATCH 107988 expected=NULL actual=NULL
  MATCH 108052 expected=5317 actual=5317
  MATCH 108077 expected=134165 actual=134165
  MATCH 108079 expected=143619 actual=143619
  MATCH 108080 expected=7385 actual=7385
  MATCH 108081 expected=152211 actual=152211
  MATCH 108099 expected=NULL actual=NULL
  MATCH 108121 expected=NULL actual=NULL
  MATCH 108158 expected=NULL actual=NULL
  MATCH 108204 expected=NULL actual=NULL
HALFLIFE_OK        107683 and 107687 still hold their HALFLIFE_PRE covers (coc2ea, co1nmw)
  NIER=no-igdb-cover (not counted as proof, D-22)  [see line above; 06-05-nier-igdb.txt: IGDB 391942 answer has no cover; 107889 CoverImageUrl still NULL]
SIBLINGS_OK        107697 / 107698: IGDBId 2369 both, UserId 2 both, 5 identical screenshots each, 1 identical artwork each (SIBLING_IGDB_ARTWORKS=1), each image once per game
FAILED_CHECKS=[ ]
WorkerStatus: GameSyncWorker=Completed|NULL, GameImageSyncWorker=Completed|NULL (LastRunAt 17:50:45 and 17:53:13 after the repro-row triggers)
```

108266 (Servant of the Lake) and 108267 (The Blood of Dawnwalker) were not NULL-IGDBId rows at the pre-restart fingerprint (06-02 recorded IGDBIds 350179 and 282831 for them), so they are covered by IGDBIDS_UNCHANGED, not by MATCHER_OK. Live values: 350179 and 282831, covers present.

### SC1 note (D-22)

SC1 is proven by `REPRO_OK` on the UAT repro row 108276: it reached the old-bug state (cover NULL, IGDBUpdatedAt set, captured live) after one non-force GameSyncWorker run, then received a `t_cover_big` cover and 12 screenshots from one non-force GameImageSyncWorker run. NieR 107889 is not counted (IGDB game 391942 has no cover). `HALFLIFE_OK` is a non-discriminating regression check only (both rows already had covers before this phase), and `COVERS_UNCHANGED` shows no existing cover changed.

### D-17 / D-26 note

The owner's rows (Portal 107693, the Dark Messiah pair, matcher rows) were verified with SQL only. Claude did not log in as the owner (UserId 2); all API calls used uat-test@gameplanner.local (UserId 17, identity verified from the token claim before each call). Owner-page confirmation is left to 06-06's D-26 checkpoint.

## Deviations from Plan

None - plan executed exactly as written, with these procedural notes:

- The dev stack start was performed by the orchestrator (not the executor) so the processes outlive the agent for 06-06; the plan's "restart" semantic is unchanged (Task 1 recorded `restart`, one start, stack was down beforehand). No process was stopped by Claude.
- The IGDB scratch helper from 06-02 no longer existed; a new read-only helper (token from `dotnet user-secrets` in memory, nothing printed) re-queried `fields id,cover; where id = 391942;`.
- Bounded retry (`## Retry`) was not needed: MATCHER_OK and SIBLINGS_OK held after the first round.

## Auth gates

None. UAT login with the owner-designated UAT account; no credential or token was printed or stored in the repo.

## Known Stubs

None.

## Threat Flags

None. No new network endpoints, auth paths or schema changes were introduced; this plan only ran verification and process control.

## Self-Check

- Files: SUMMARY, 06-05-repro-precondition.txt and 06-05-nier-igdb.txt exist on disk.
- Verify block re-run: every token recomputed live with `FAILED_CHECKS=[ ]`.
- No worker was triggered with force; no owner row was written by Claude; no owner login.
- Repro row 108276 left active for 06-06 (REPRO_ID recorded). API and UI left running.

## Self-Check: PASSED
