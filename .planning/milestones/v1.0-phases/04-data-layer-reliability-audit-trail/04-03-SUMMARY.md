---
phase: 04-data-layer-reliability-audit-trail
plan: 03
subsystem: database
tags: [sql-server, stored-procedures, ef-core, angular, dapper]

requires:
  - phase: 04-data-layer-reliability-audit-trail
    provides: "04-01's transactional GameManager write paths and 04-02's SteamTokenStore cleanup worker share the same GameManager.cs surface this plan closes the folded IGDBId bug against"
provides:
  - "Games.IGDBId is non-unique (global unique index UX_Games_IGDBId dropped, replaced by filtered IX_Games_IGDBId), so two users can each hold a row for the same IGDB title"
  - "sp_UpsertIGDBGame is a strictly scoped UPDATE-only procedure that can never create a Games row"
  - "A fixed, user-facing delete-failure message in the games list"
  - "A new pending todo documenting an unrelated data-quality finding (Half-Life/Half-Life 2 IGDBId duplicate) discovered incidentally during checkpoint fact-gathering"
affects: [phase-verification, gsd-secure-phase]

tech-stack:
  added: []
  patterns:
    - "sqlcmd -b for scripted migration application (non-zero exit on T-SQL error)"
    - "SET ANSI_NULLS ON / SET QUOTED_IDENTIFIER ON immediately before CREATE OR ALTER PROCEDURE, required whenever the procedure body touches a table with a filtered index (Games has two) -- established in 04-01, reused here for consistency and applies equally to ad-hoc sqlcmd -Q batches"

key-files:
  created:
    - Database/33_RemoveUniqueIndex_IGDBId.sql
    - .planning/todos/pending/2026-08-31-halflife-igdbid-duplicate-same-user.md
  modified:
    - Database/34_FixStoredProcedure_UpsertIGDBGame.sql
    - game-planner-ui/src/app/components/game-list/game-list.component.ts

key-decisions:
  - "Task 1 checkpoint decision: proceed with the UX_Games_IGDBId drop now (not backup-first, not defer), per user's explicit instruction"
  - "The incidentally-discovered Half-Life/Half-Life 2 same-user IGDBId duplicate was logged as a new pending todo, not investigated or fixed -- explicitly out of scope for this plan"
  - "The folded todo (2026-08-29-igdbid-upsert-deactivates-games-across-users.md) stays in .planning/todos/pending/ -- its resolution is contingent on human-check B (live IGDBId re-verification with sync workers), which is deferred to end-of-phase UAT per human_verify_mode: end-of-phase, not run in this session"

patterns-established:
  - "Ad-hoc sqlcmd -Q batches against tables with filtered indexes need their own SET QUOTED_IDENTIFIER ON prefix (a session-level default, distinct from the CREATE-time SET options baked into a stored procedure) -- hit during the plan's own probe-insert verify step"

requirements-completed: [REL-01]

coverage:
  - id: D1
    description: "Games.IGDBId carries a non-unique filtered index (IX_Games_IGDBId) and no unique index"
    requirement: "REL-01"
    verification:
      - kind: other
        ref: "sqlcmd sys.indexes check: UX_Games_IGDBId count=0, IX_Games_IGDBId count=1 is_unique=0"
        status: pass
    human_judgment: false
  - id: D2
    description: "sp_UpsertIGDBGame is UPDATE-only and scoped by (IGDBId, UserId); can never create a Games row"
    requirement: "REL-01"
    verification:
      - kind: other
        ref: "OBJECT_DEFINITION(sp_UpsertIGDBGame) contains 'UserId = @UserId', contains no INSERT; script source grep for 0x INSERT INTO Games, 0x ELSE"
        status: pass
    human_judgment: false
  - id: D3
    description: "A manually added IGDB game stays IsActive=1 after IGDB sync and image sync workers run (folded todo root-cause re-verification)"
    verification: []
    human_judgment: true
    rationale: "Requires the API and Angular app running live with real IGDB search calls and background worker triggers via the /workers page -- deferred to end-of-phase UAT per human_verify_mode: end-of-phase, matching 04-02's own deferred human-check handling"
  - id: D4
    description: "Deleting a game surfaces a fixed user-facing message when the request fails, with no server-derived text"
    requirement: "REL-01"
    verification:
      - kind: unit
        ref: "source grep: game-list.component.ts contains 'Could not delete game.', 0x error.message, 0x err.error, 0x JSON.stringify; deleteGame body contains both next: and error: callbacks"
        status: pass
    human_judgment: true
    rationale: "Automated checks prove the source shape (fixed string, no response-derived text reaches the callback), but rendering the message live with the API down is a visual/behavioral confirmation deferred to end-of-phase UAT"
  - id: D5
    description: "Games.UpdatedAt is neither renamed nor dropped by the corrected procedure"
    requirement: "REL-01"
    verification:
      - kind: other
        ref: "script source grep: contains 'UpdatedAt', 0x 'ModifiedAt'"
        status: pass
    human_judgment: false
  - id: D6
    description: "CalendarController and TrackedGamesManager filter by UserId and IsActive before keying an IGDBId dictionary"
    requirement: "REL-01"
    verification:
      - kind: other
        ref: "grep 'g.IsActive && g.UserId == userId && g.IGDBId != null' in both files"
        status: pass
    human_judgment: false

duration: 35min
completed: 2026-08-31
status: complete
---

# Phase 4 Plan 3: IGDBId Cross-User Collision Fix + Delete Error Surface Summary

**Dropped the global unique index on `Games.IGDBId`, rewrote `sp_UpsertIGDBGame` as a strictly `(IGDBId, UserId)`-scoped UPDATE-only procedure with no creation path, and added a user-facing delete-failure message to the games list.**

## Performance

- **Duration:** ~35 min (continuation from a prior checkpoint dispatch)
- **Completed:** 2026-08-31
- **Tasks:** 3/3 (Task 1 checkpoint resolved, Task 2 and Task 3 executed)
- **Files modified:** 3 (2 SQL scripts, 1 TypeScript component) + 1 new pending-todo doc

## Accomplishments
- Closed the folded IGDBId cross-user collision bug at the database layer: `UX_Games_IGDBId` (global unique index) is gone, replaced by the non-unique filtered `IX_Games_IGDBId`; two users can now each hold a row for the same IGDB title without the database rejecting the second insert
- `sp_UpsertIGDBGame` reduced to a single unconditional `UPDATE ... WHERE IGDBId = @IGDBId AND UserId = @UserId` — the drafted row-creating `ELSE`/`INSERT` branch (which would have created games with empty genres/tags and a hardcoded 60-minute estimate) is removed entirely; the procedure is now a no-op when no matching row exists
- Confirmed both remaining `IGDBId`-keyed dictionary consumers (`CalendarController.cs`, `TrackedGamesManager.cs`) already filter by `UserId` and `IsActive` first — no code change needed there, only verification
- `deleteGame()` in the games list now surfaces "Could not delete game. Please try again." on failure instead of leaving an unhandled `HttpErrorResponse` visible only in the console; the callback reads nothing off the failure object
- Logged an unrelated data-quality finding (two distinct games sharing one `IGDBId` under the same user) as a new pending todo rather than investigating or fixing it, per explicit user instruction to keep this plan's scope closed

## Task Commits

1. **Task 1: One-way door checkpoint** — resolved via user decision ("proceed"), no code commit of its own; facts re-verified independently below
2. **Task 2: Correct and apply migration scripts, verify IGDBId consumers** — `f41f622` (fix)
3. **Task 3: Surface delete failures in the games list** — `a85d95d` (fix)

**Pending-todo commit (out-of-plan-scope discovery, docs):** `663bbd4`

**Plan metadata:** (this commit, following STATE/ROADMAP update)

## Files Created/Modified
- `Database/33_RemoveUniqueIndex_IGDBId.sql` — committed as-drafted (untouched); applied via `sqlcmd -b` as an idempotent no-op re-apply since the index was already gone live
- `Database/34_FixStoredProcedure_UpsertIGDBGame.sql` — rewritten to a single UPDATE-only, `(IGDBId, UserId)`-scoped statement; added `SET ANSI_NULLS ON` / `SET QUOTED_IDENTIFIER ON` ahead of `CREATE OR ALTER PROCEDURE` (see Deviations)
- `game-planner-ui/src/app/components/game-list/game-list.component.ts` — `deleteGame()` clears stale `error` on entry and assigns a fixed failure message via a new `error:` subscribe callback
- `.planning/todos/pending/2026-08-31-halflife-igdbid-duplicate-same-user.md` — new pending todo, unrelated finding

## Decisions Made

**Task 1 checkpoint decision (re-verified independently before acting on it):**
Before answering the checkpoint, the four facts reported by the prior dispatch were
independently re-confirmed live against the database in this session:
1. `UX_Games_IGDBId` does **not** exist on `Games`; `IX_Games_IGDBId` (non-unique, filtered
   `WHERE IGDBId IS NOT NULL`) already exists — script 33 had already been applied to the
   live database outside git tracking.
2. `Games` has 482 total rows, 461 with non-NULL `IGDBId`.
3. A duplicate `(IGDBId, UserId)` pair exists among active rows: `IGDBId = 267639, UserId = 2`
   appears twice — `Id 107683` ("Half-Life") and `Id 107687` ("Half-Life 2"), both `IsActive = 1`.
4. The live `sp_UpsertIGDBGame` was already the buggy draft from script 34 (with the
   row-creating `ELSE`/`INSERT` branch) — also applied outside git tracking.

User's answer: **proceed** (plain, not backup-first, not defer). Script 33 was committed
as-is (idempotent no-op re-apply against the live database, since the index was already
gone) and script 34 was corrected per D-02 and applied.

**Unplanned-finding decision:** the Half-Life/Half-Life 2 same-`IGDBId` duplicate is logged
as a new pending todo, not investigated or fixed in this plan, per explicit user instruction.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Pinned `SET ANSI_NULLS ON` / `SET QUOTED_IDENTIFIER ON` before `CREATE OR ALTER PROCEDURE sp_UpsertIGDBGame`**
- **Found during:** Task 2, while rewriting `Database/34_FixStoredProcedure_UpsertIGDBGame.sql`
- **Issue:** `sp_UpsertIGDBGame`'s corrected body issues an `UPDATE Games` against a table that
  now carries two filtered indexes (`IX_Games_IGDBId`, `UX_Games_SteamAppId_PerUser`). This
  session's own Phase 4 history (04-01) already hit exactly this bug: `sqlcmd`'s default
  `QUOTED_IDENTIFIER OFF` at `CREATE` time gets baked into the procedure and breaks any
  `UPDATE`/`INSERT` against a table with a filtered index, unlike SSMS's session default.
  The plan's drafted script 34 did not include this guard.
- **Fix:** Added `SET ANSI_NULLS ON; GO SET QUOTED_IDENTIFIER ON; GO` immediately before
  `CREATE OR ALTER PROCEDURE`, matching the exact pattern already established in
  `Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql` from the same phase.
- **Files modified:** `Database/34_FixStoredProcedure_UpsertIGDBGame.sql`
- **Verification:** `dotnet build GamePlanner.sln` exits 0; the plan's own `<verify>` block's
  `OBJECT_DEFINITION` checks pass; the cross-user duplicate-insert probe succeeds after
  applying the script
- **Committed in:** `f41f622` (Task 2 commit)

**2. [Rule 3 - Blocking] `SET QUOTED_IDENTIFIER ON` prefix needed on the plan's own probe-insert verify query**
- **Found during:** Task 2, running the plan's automated `<verify>` block
- **Issue:** The plan's own cross-user duplicate-insert probe query (`sqlcmd -Q "BEGIN TRAN;
  INSERT INTO Games (...) VALUES (...); SELECT 1; ROLLBACK;"`) failed with `Msg 1934:
  INSERT failed because the following SET options have incorrect settings:
  'QUOTED_IDENTIFIER'` — an ad-hoc `sqlcmd -Q` batch has its own session-level
  `QUOTED_IDENTIFIER OFF` default (distinct from the CREATE-time setting baked into a stored
  procedure), and `Games` has filtered indexes.
- **Fix:** Prefixed the probe query with `SET QUOTED_IDENTIFIER ON;` when running it. This
  is a verify-time workaround only — no plan file or script content changed, since the plan's
  scripts (33/34) themselves already carry their own correct CREATE-time SET options.
- **Verification:** The probe query succeeded (`(2 rows affected)` then `1`) after the prefix
  was added
- **Committed in:** N/A (verification-only workaround, no file changed)

---

**Total deviations:** 2 auto-fixed (both Rule 1/3, both driven by the same underlying
sqlcmd/filtered-index interaction already documented from 04-01)
**Impact on plan:** Both fixes were necessary for the migration to actually apply and
verify correctly against this database's schema. No scope creep — no plan behavior changed,
only the mechanics of getting the same specified SQL to execute correctly under sqlcmd.

## Issues Encountered

None beyond the two auto-fixed deviations above.

## Human-Check Deferrals (per `human_verify_mode: end-of-phase`)

Both of Task 3's `<human-check>` items were **not** run in this session and are explicitly
deferred to end-of-phase UAT, consistent with how 04-02's SUMMARY handled its own deferred
human check:

- **Human-check A (delete-failure UI message):** requires the API and Angular dev servers
  running live, stopping the API mid-session, and clicking delete to visually confirm the
  rendered message and absence of server-derived text.
- **Human-check B (IGDBId cross-user re-verification):** requires adding two new games
  through the live "Add Game" IGDB search flow, triggering `GameSyncWorker` and
  `GameImageSyncWorker` via the `/workers` page, and re-querying `Games.IsActive` for those
  two new rows.

**The folded pending todo (`2026-08-29-igdbid-upsert-deactivates-games-across-users.md`) is
NOT moved to `.planning/todos/completed/` in this plan.** Per the plan's own success
criteria, that move is conditional on human-check B passing, which has not yet been
observed. It remains in `.planning/todos/pending/` until end-of-phase UAT confirms the fix
holds against a live sync-worker run.

## Known Stubs

None.

## Threat Flags

None — all four threat register entries assigned to this plan (T-04-13, T-04-14, T-04-15,
T-04-16, T-04-17) were mitigated exactly as specified; no new unregistered security-relevant
surface was introduced.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

This is the **last plan in Phase 4 (data-layer-reliability-audit-trail)**. All three plans
(04-01 transactional writes + audit fields, 04-02 SteamTokenStore cleanup worker, 04-03
IGDBId collision fix + delete error surface) are now committed. **Phase-level verification
comes next** — this includes:
- The two deferred human-checks from this plan (A: delete-failure UI, B: live IGDBId
  cross-user re-verification with sync workers)
- 04-02's own deferred human-check (SteamTokenStore live sweep observation)
- Whatever consolidated end-of-phase UAT the phase's own `<verification>`/`<success_criteria>`
  blocks call for across all three plans

Two pending todos remain open and unresolved:
- `2026-08-29-igdbid-upsert-deactivates-games-across-users.md` — resolution contingent on
  human-check B above
- `2026-08-31-halflife-igdbid-duplicate-same-user.md` — new, unrelated, minor data-quality
  finding, not blocking phase completion

---
*Phase: 04-data-layer-reliability-audit-trail*
*Completed: 2026-08-31*

## Self-Check: PASSED

All created/modified files confirmed present on disk; all three task/docs commit hashes
(`f41f622`, `a85d95d`, `663bbd4`) confirmed present in `git log`.
