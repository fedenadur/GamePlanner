---
phase: 05-regression-testing-full-stack-verification
plan: 02
subsystem: database
tags: [sql-server, igdb, data-correction, idempotent-script, workers]

requires:
  - phase: 04
    provides: "UX_Games_IGDBId dropped (script 33), sp_UpsertIGDBGame keyed on (IGDBId, UserId) (script 34)"
provides:
  - "Database/38_FixHalfLifeIgdbId.sql: guarded, transactional, reversible IGDBId correction, applied by the user in SSMS"
  - "Half-Life (107683) -> IGDBId 231 and Half-Life 2 (107687) -> IGDBId 233; UserId 2 duplicate-title scan returns 0 groups"
  - "Re-enriched release date, rating, genres, screenshots and artworks for both rows from the correct IGDB entries"
  - "Half-Life todo closed; Portal wrong-match and cover-skip todos logged"
affects: [05-06 full-stack UAT]

tech-stack:
  added: []
  patterns:
    - "One-off numbered data scripts may use raw IF EXISTS-guarded UPDATE/DELETE (user-approved exemption from SP-only-for-writes)"
    - "Claude-side runs of a data script are dry runs: COMMIT line stripped, outer transaction, ROLLBACK, abort-before-sqlcmd guard"

key-files:
  created:
    - Database/38_FixHalfLifeIgdbId.sql
    - .planning/todos/pending/2026-10-24-portal-igdbid-wrong-match.md
    - .planning/todos/pending/2026-10-24-cover-skipped-when-igdbupdatedat-set.md
  modified:
    - .planning/todos/completed/2026-08-31-halflife-igdbid-duplicate-same-user.md

key-decisions:
  - "Both rows were wrong (each held 267639, 'Half-Life 2: MMod', 2018), so both were corrected rather than one of 107683/107687"
  - "User approved IGDBId 231 (Half-Life) and 233 (Half-Life 2) and the SP-only convention exemption at the 05-02 decision checkpoint (apply-user)"
  - "GameTags left alone: also written by user edits (GameManager.SyncTagsAsync) and GameEnrichmentManager; both rows held zero tags"
  - "Cover NULL after re-enrichment was NOT hand-patched; logged as a todo with a one-line workaround awaiting the owner's go-ahead"

requirements-completed: []
# TEST-02 is deliberately NOT marked complete here: plan 05-06 is the main TEST-02 deliverable;
# this plan only satisfies its precondition (workers run against corrected IDs) and the adjacency probe.

duration: ~2h across two executor sessions (includes the user's decision checkpoint)
completed: 2026-10-24
status: complete
---

# Phase 5 Plan 02: Half-Life / Half-Life 2 IGDBId correction Summary

**Script 38 separated the owner's two Half-Life rows (IGDB 267639 -> 231 / 233) with a guarded, reversible, user-applied correction; both rows re-enriched to 1998 / 2004 release dates, but covers stayed NULL because of a pre-existing image-worker skip.**

## Accomplishments

- Re-ran the D-08 duplicate scan for UserId 2 and for all users: exactly one group, `UserId 2, IGDBId 267639 -> 107683:Half-Life ; 107687:Half-Life 2`. No other users have groups; no same-name shared-IGDBId rows exist.
- Looked up IDs with the app's own `GET /api/games/igdb/search` as the UAT user (exact title + original year); user approved them at the checkpoint.
- Authored `Database/38_FixHalfLifeIgdbId.sql` (two `GO` lines, `XACT_ABORT`, per-row `IF EXISTS` guard on pre-fix `(Id, UserId, IGDBId)`, `-- CONVENTION EXEMPTION:` line, literal `-- REVERSAL:` block); proved it with a rolled-back dry run (Task 1, commit 2c28671).
- The user applied script 38 manually in SSMS and replied `applied`; Claude only ran rolled-back dry runs.
- Read-back, idempotency pass, non-force worker re-enrichment and todo handling (Task 3, below).

## Task Commits

| Task | Name | Commit |
|------|------|--------|
| 1 | Re-scan, look up IDs, author script 38, rolled-back dry run (tracer) | 2c28671 |
| 2 | Decision checkpoint (user: apply-user; IDs 231/233 and the SP-only exemption approved) | n/a |
| 3 | Read-back verify, idempotency pass, non-force workers, todos | 1067637 |

## Evidence

### Duplicate scans (Task 1, before authoring)

UserId 2 and all users each returned the single group above.

### IGDB lookup lists (app search as UAT user; capped at 5 results)

| Query | Results (igdbId, name, year) |
|-------|------------------------------|
| `Half-Life` | 267639, 271414, 271415 (MMod 2018), 401474 Half-Life 2001, 228891 Half-Life no year |
| `Half-Life 2` | 267639, 271414, 271415, **233 Half-Life 2 2004**, 138366 |
| `half life` | 383405 GOTY 1999, 28029 HL3, 320851 HL2 Survivor 2013, 267639, 271415 |
| `Half-Life: Opposing Force` | 232 (1999) |

Chosen: Half-Life 2 -> 233 (exact title, 2004-11-16). Half-Life -> 231 (see Deviations).

### Rolled-back dry run (Task 1)

COMMIT guard passed before `sqlcmd` ran; `DRYRUN_DUP_GROUPS=0`; in-transaction rows read 231/233 with cover and FirstReleaseDate NULL and genres 0; live rows unchanged afterwards (`TRANCOUNT_BEFORE_ROLLBACK=2`, outer plus the script's own transaction, both rolled back).

### Post-apply read-back (Task 3)

- UserId 2 duplicate-title scan: **0** groups. No row among 107683/107687 holds 267639.
- Corrected rows' `ModifiedAt` (set by the user's apply): both `2026-10-24 21:39:14.0866667`.
- Idempotency (guarded, rolled-back second pass; COMMIT guard passed): `IDEMPOTENT_CHANGED=0`; `ModifiedAt` unchanged for both rows after the pass.

### Non-force worker runs (triggered by this task, as UAT user, no `force` parameter)

| Worker | LastRunAt before | LastRunAt after | Status | LastError |
|--------|------------------|-----------------|--------|-----------|
| GameSyncWorker | 2026-10-24T21:33:02.6351856 | 2026-10-24T21:40:09.8617098 | Completed | NULL |
| GameImageSyncWorker | 2026-10-24T21:33:02.6351562 | 2026-10-24T21:42:25.2608335 | Completed | NULL |

### Re-derivation per corrected row

| Id | Name | IGDBId | FirstReleaseDate | Genres | Rating | Screenshots / artworks | CoverImageUrl |
|----|------|--------|------------------|--------|--------|------------------------|---------------|
| 107683 | Half-Life | 231 | 1998-11-19 (year 1998, match) | 3 | 80.0 | 12 / 2 | **NULL** |
| 107687 | Half-Life 2 | 233 | 2004-11-16 (year 2004, match) | 1 | 79.125 | 5 / 4 | **NULL** |

## Deviations from Plan

### Auto-fixed Issues

None.

### Deviations

**1. [Lookup source] Half-Life 231 confirmed out-of-band, not from the app search**
- **Found during:** Task 1
- **Issue:** The app search is capped at 5 results and never returned id 231 for "Half-Life" (only 401474 / 2001 and 228891 / no year matched the exact title).
- **Resolution:** 231 was corroborated by a direct IGDB id query (name "Half-Life", first release 1998-11-19). The user explicitly approved 231 at the checkpoint, which satisfies the "never guessed" intent of D-08 / T-05-07.

**2. [Acceptance not met] CoverImageUrl did not re-derive**
- **Found during:** Task 3 step 4
- **Issue:** The plan's truth requires a non-NULL `CoverImageUrl` on each corrected row. Both stayed NULL after the non-force sync then image runs. `GameSyncWorker` re-stamps `IGDBUpdatedAt` first; `GameImageManager.SaveCoverAsync` (unchanged in HEAD) then skips with "Cover up-to-date" because `igdbUpdatedAt <= currentUpdatedAt`, even though the cover is NULL. Screenshots, artworks, genres, dates and ratings did re-derive; `LastImageSyncDate` was set 2026-10-24 21:42:45.
- **Why not fixed here:** Per the plan, IGDB values are not hand-written, and clearing `IGDBUpdatedAt` would be an extra live write the user did not approve; an app fix is feature/behavior work outside the hardening milestone. Plan 05-06 re-runs the workers and will not self-heal this.
- **Remedy (owner decision):** `UPDATE Games SET IGDBUpdatedAt = NULL WHERE Id IN (107683,107687);` then one non-force `GameImageSyncWorker` run. Logged in `.planning/todos/pending/2026-10-24-cover-skipped-when-igdbupdatedat-set.md`.

**3. [Process] Wrong-ordering of git add in the Task 3 commit**
- The first commit attempt staged only the rename; the content was amended into the same local commit (1067637) before anything else happened. No other effect.

## Convention exemption

The user selected `apply-user`, approving both the looked-up IDs and the exemption of script 38 from the SP-only-for-writes convention (raw guarded UPDATE/DELETE, no stored procedure, single-use SP being an unnecessary abstraction; no existing SP performs this change). The exemption is recorded in the script header.

## Authentication gates

None. The UAT user logged in with credentials read from the user's local memory file; the password and JWT were not printed or written to any file.

## Known Stubs

None.

## Threat Flags

None (script 38 uses literals only, no dynamic SQL, no new endpoints).

## Issues Encountered

- sqlcmd on Windows emits CRLF; verification pipes were run through `tr -d '\r'` before `grep -qx` checks.
- Cover re-derivation gap (Deviation 2).

## Self-Check: PASSED

- Database/38_FixHalfLifeIgdbId.sql: FOUND (commit 2c28671)
- .planning/todos/completed/2026-08-31-halflife-igdbid-duplicate-same-user.md with Resolution: FOUND (commit 1067637)
- .planning/todos/pending/2026-10-24-portal-igdbid-wrong-match.md (mentions 14546): FOUND
- .planning/todos/pending/2026-10-24-cover-skipped-when-igdbupdatedat-set.md: FOUND
- Commits 2c28671 and 1067637: FOUND in git log
- Plan automated verify block: all checks pass except the cover re-derivation truth (documented above); WIP files in GameImageManager.cs and game-detail/* untouched and unstaged.
