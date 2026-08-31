---
phase: 04-data-layer-reliability-audit-trail
plan: 01
subsystem: database
tags: [ef-core, dapper, sql-server, transactions, audit-trail, xunit]

requires: []
provides:
  - "GameManager.CreateAsync/CreateFromIgdbAsync/UpdateAsync share one EF-owned transaction with their Dapper stored-procedure call"
  - "Games.ModifiedAt/DeletedAt and NowPlayingGames.ModifiedAt columns, stamped exclusively in T-SQL"
  - "sp_DeleteGame idempotency guard (IsActive = 1 in WHERE) and a fixed rows-affected return value"
  - "GamePlanner.Tests xUnit harness (real SQL Server, no mocks) that later phases can add files to"
affects: [04-02, 04-03, gsd-secure-phase]

tech-stack:
  added: ["xunit 2.5.3", "xunit.runner.visualstudio 2.5.3", "Microsoft.NET.Test.Sdk 17.8.0", "coverlet.collector 6.0.0"]
  patterns:
    - "Dapper receives the EF-owned transaction via db.Database.BeginTransactionAsync() + tx.GetDbTransaction(), passed through Dapper's transaction: parameter"
    - "SQL-side timestamp stamping (GETUTCDATE() inside the stored procedure), never assigned in C#"
    - "Stored procs that suppress row-count messages with SET NOCOUNT ON must SELECT @@ROWCOUNT explicitly if the caller needs the affected-row count via ExecuteScalarAsync"
    - "CREATE OR ALTER PROCEDURE statements pin SET ANSI_NULLS ON / SET QUOTED_IDENTIFIER ON immediately before them when the target table has a filtered index, because sqlcmd (unlike SSMS) defaults these OFF"

key-files:
  created:
    - Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql
    - GamePlanner.Tests/GamePlanner.Tests.csproj
    - GamePlanner.Tests/GameManagerAtomicityTests.cs
  modified:
    - GamePlanner.API/Entities/Game.cs
    - GamePlanner.API/Entities/NowPlayingGame.cs
    - GamePlanner.API/Managers/GameManager.cs
    - GamePlanner.sln

key-decisions:
  - "sp_DeleteGame's SET NOCOUNT ON silently made GameManager.DeleteAsync always return false (Dapper's ExecuteAsync returns -1 under NOCOUNT); fixed by SELECTing @@ROWCOUNT and switching to ExecuteScalarAsync<int>, not by dropping NOCOUNT"
  - "sqlcmd's default QUOTED_IDENTIFIER OFF got baked into sp_UpdateGame/sp_DeleteGame at CREATE time, breaking any UPDATE against Games (which carries two filtered indexes); fixed by pinning SET ANSI_NULLS ON / SET QUOTED_IDENTIFIER ON before each CREATE OR ALTER PROCEDURE in the migration"
  - "Task 1's own <verify> block asserted grep -c 'CreateConnection()' == 2 against GameManager.cs, but Task 1 only converts UpdateAsync (CreateAsync/CreateFromIgdbAsync stay on CreateConnection() until Task 2) -- the true post-Task-1 count is 4. Treated as a plan-authoring copy/paste of the post-Task-2 target rather than a code defect; the identical assertion in Task 2's own verify block is what actually gates it, and it passed there (count = 2) once both create paths were converted"

requirements-completed: [REL-01, AUDIT-01, AUDIT-02]

coverage:
  - id: D1
    description: "GameManager.UpdateAsync runs sp_UpdateGame and SaveChangesAsync on one shared transaction; a mid-write failure (over-length genre truncation) rolls the stored-procedure write back too"
    requirement: "REL-01"
    verification:
      - kind: integration
        ref: "GamePlanner.Tests/GameManagerAtomicityTests.cs#UpdateAsync_RollsBackStoredProcWrite_WhenSaveChangesThrows"
        status: pass
    human_judgment: false
  - id: D2
    description: "GameManager.CreateAsync runs sp_InsertGame and SaveChangesAsync on one shared transaction; a mid-write failure leaves no orphan Games row"
    requirement: "REL-01"
    verification:
      - kind: integration
        ref: "GamePlanner.Tests/GameManagerAtomicityTests.cs#CreateAsync_RollsBackInsertedGame_WhenSaveChangesThrows"
        status: pass
    human_judgment: false
  - id: D3
    description: "CreateFromIgdbAsync's dedup short-circuit survives the transaction rewrite -- a repeat call with the same (IgdbId, UserId) returns the existing id and leaves exactly one row"
    requirement: "REL-01"
    verification:
      - kind: integration
        ref: "GamePlanner.Tests/GameManagerAtomicityTests.cs#CreateFromIgdbAsync_ReturnsExistingId_WhenIgdbIdAlreadyLinkedToUser"
        status: pass
    human_judgment: false
  - id: D4
    description: "Games.ModifiedAt is stamped by sp_UpdateGame via GETUTCDATE(), never assigned in C#, and lands within 60 seconds of a successful update"
    requirement: "AUDIT-01"
    verification:
      - kind: integration
        ref: "GamePlanner.Tests/GameManagerAtomicityTests.cs#UpdateAsync_StampsModifiedAt_OnSuccess"
        status: pass
    human_judgment: false
  - id: D5
    description: "sp_DeleteGame stamps DeletedAt/ModifiedAt in one UPDATE and gains an IsActive = 1 WHERE guard, making a repeat delete a zero-row no-op that leaves the original DeletedAt untouched"
    requirement: "AUDIT-02"
    verification:
      - kind: integration
        ref: "GamePlanner.Tests/GameManagerAtomicityTests.cs#DeleteAsync_IsIdempotent_SecondCallIsNoOp"
        status: pass
    human_judgment: false
  - id: D6
    description: "Migration 36 backfills nothing -- pre-existing Games/NowPlayingGames rows read NULL for the new audit columns"
    requirement: "AUDIT-01"
    verification:
      - kind: other
        ref: "sqlcmd: SELECT CASE WHEN EXISTS(SELECT 1 FROM Games WHERE ModifiedAt IS NULL) THEN 1 ELSE 0 END -> 1"
        status: pass
    human_judgment: false
  - id: D7
    description: "sp_UpsertNowPlayingGame stamps ModifiedAt on both its insert and update branches"
    requirement: "AUDIT-01"
    verification:
      - kind: other
        ref: "sqlcmd: OBJECT_DEFINITION('sp_UpsertNowPlayingGame') LIKE '%ModifiedAt%' -> 1"
        status: pass
    human_judgment: false

duration: 20min
completed: 2026-08-31
status: complete
---

# Phase 4 Plan 01: Atomic + Audited Game Writes Summary

**GameManager's three game write paths (Create/CreateFromIgdb/Update) now share one EF-owned SQL transaction with their Dapper stored-procedure call, and Games/NowPlayingGames gained SQL-side ModifiedAt/DeletedAt audit stamps -- both proven against a real SQL Server by a new xUnit harness, including a latent DeleteAsync bug found and fixed along the way.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-08-31T19:42:00Z (approx, per STATE.md)
- **Completed:** 2026-08-31T19:57:45Z
- **Tasks:** 2 completed
- **Files modified:** 7 (2 new SQL/test-project files created, 1 test file, 4 modified source/solution files)

## Accomplishments

- Migration `Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql` adds nullable `Games.ModifiedAt`/`Games.DeletedAt` and `NowPlayingGames.ModifiedAt`, with zero backfill of existing rows
- `sp_UpdateGame` stamps `ModifiedAt`, `sp_DeleteGame` stamps `DeletedAt`/`ModifiedAt` and gained an `IsActive = 1` idempotency guard, `sp_UpsertNowPlayingGame` stamps `ModifiedAt` on both branches -- all via `GETUTCDATE()`, never from C#
- All three `GameManager` game-write paths (`CreateAsync`, `CreateFromIgdbAsync`, `UpdateAsync`) now open one EF transaction, pass it to their Dapper call via `tx.GetDbTransaction()`, and commit only after `SaveChangesAsync` succeeds; `DeleteAsync` stays a single SQL-side write per D-06
- The pre-existing `CreateFromIgdbAsync` dedup short-circuit (D-01, already in the working tree before this plan ran) survives the rewrite, verified by a dedicated test
- New `GamePlanner.Tests` xUnit project (real SQL Server, no mocks) with 5 passing facts: update rollback, update stamp, delete idempotency, create rollback, dedup preservation
- Fixed two latent bugs discovered while proving these truths (see Deviations)

## Task Commits

Each task was committed atomically:

1. **Task 1: End-to-end atomic + audited game update** - `16fe503` (feat)
2. **Task 2: Extend the shared transaction to both create paths** - `1b584c5` (feat)

_No separate plan-metadata commit was requested for this run; STATE.md/ROADMAP.md updates are captured in the final housekeeping commit._

## Files Created/Modified

- `Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql` - New migration: audit columns + `sp_UpdateGame`/`sp_DeleteGame`/`sp_UpsertNowPlayingGame` stamping, `ANSI_NULLS`/`QUOTED_IDENTIFIER` fix, `sp_DeleteGame` `@@ROWCOUNT` fix
- `GamePlanner.API/Entities/Game.cs` - Added `ModifiedAt`/`DeletedAt` nullable properties
- `GamePlanner.API/Entities/NowPlayingGame.cs` - Added `ModifiedAt` nullable property
- `GamePlanner.API/Managers/GameManager.cs` - `CreateAsync`/`CreateFromIgdbAsync`/`UpdateAsync` rewritten onto a shared EF transaction; `DeleteAsync` switched to `ExecuteScalarAsync<int>` to read the real affected-row count
- `GamePlanner.Tests/GamePlanner.Tests.csproj` - New xUnit project (net8.0), references `GamePlanner.API.csproj`
- `GamePlanner.Tests/GameManagerAtomicityTests.cs` - 5 integration facts against a real dev database, every probe row prefixed `gsd-probe-` and cleaned up in `finally`
- `GamePlanner.sln` - Added `GamePlanner.Tests` project reference

## Decisions Made

- Kept `SET NOCOUNT ON` in `sp_DeleteGame` (codebase-wide convention) and added an explicit `SELECT @@ROWCOUNT AS RowsAffected;` instead, switching the caller to `ExecuteScalarAsync<int>` -- preserves the convention while fixing the always-false return value
- Pinned `SET ANSI_NULLS ON` / `SET QUOTED_IDENTIFIER ON` immediately before each `CREATE OR ALTER PROCEDURE` in the new migration, rather than relying on the caller's session settings, since SQL Server checks the settings baked in at proc-creation time for any DML against a table with a filtered index
- Read Task 1's own `CreateConnection()` count == 2 assertion as scoped to the state after Task 2 completes (Task 1 by itself only converts `UpdateAsync`, leaving `CreateAsync`/`CreateFromIgdbAsync` on `CreateConnection()`, for a true count of 4) -- did not pull Task 2's scope into Task 1 to force an early match

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] `GameManager.DeleteAsync` always returned `false` regardless of success**
- **Found during:** Task 1 (writing the delete-idempotency test)
- **Issue:** `sp_DeleteGame` has `SET NOCOUNT ON;`, and Dapper's `ExecuteAsync` (backed by `SqlCommand.ExecuteNonQueryAsync`) returns `-1` for any stored procedure that suppresses row-count messages. `DeleteAsync`'s `return rows > 0;` therefore always evaluated to `false`, silently breaking every caller that checked the return value.
- **Fix:** Added `SELECT @@ROWCOUNT AS RowsAffected;` to the end of `sp_DeleteGame` and switched `GameManager.DeleteAsync` to `conn.ExecuteScalarAsync<int>(...)`.
- **Files modified:** `Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql`, `GamePlanner.API/Managers/GameManager.cs`
- **Verification:** `GameManagerAtomicityTests.DeleteAsync_IsIdempotent_SecondCallIsNoOp` passes, asserting the first call returns 1 affected row and the second returns 0
- **Committed in:** `16fe503` (Task 1 commit)

**2. [Rule 1 - Bug] `sqlcmd`-applied `CREATE OR ALTER PROCEDURE` broke every UPDATE against `Games`**
- **Found during:** Task 1 (first test run after applying the migration)
- **Issue:** `sqlcmd` defaults `QUOTED_IDENTIFIER` to `OFF` (unlike SSMS, which defaults it `ON`). Since `sp_UpdateGame` and `sp_DeleteGame` were recreated via `sqlcmd` without this setting, SQL Server rejected every subsequent `UPDATE Games` with error 1934 ("SET options have incorrect settings: 'QUOTED_IDENTIFIER'"), because `Games` carries two filtered indexes (`IX_Games_IGDBId`, `UX_Games_SteamAppId_PerUser`) and the check is against the settings baked in at proc-creation time, not the caller's session.
- **Fix:** Added `SET ANSI_NULLS ON; GO SET QUOTED_IDENTIFIER ON; GO` immediately before each `CREATE OR ALTER PROCEDURE` statement in the migration (`sp_UpdateGame`, `sp_DeleteGame`, `sp_UpsertNowPlayingGame`), then re-applied the migration.
- **Files modified:** `Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql`
- **Verification:** `OBJECTPROPERTY(OBJECT_ID('sp_UpdateGame'), 'ExecIsQuotedIdentOn')` etc. all return `1`; all 5 tests pass
- **Committed in:** `16fe503` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 - bug fixes discovered while proving the plan's own required truths)
**Impact on plan:** Both fixes were necessary for the plan's own acceptance criteria (delete idempotency, successful UPDATE against Games) to be provable at all. No scope creep -- both live inside files the plan already modifies (`GameManager.cs`, migration 36).

## Issues Encountered

- A debug xUnit fact (`DebugNoCountProbe`) and one ad-hoc `sqlcmd` INSERT were used transiently to diagnose the `SET NOCOUNT ON` / `@@ROWCOUNT` behavior; both the debug test class and its leftover `Games` row (`Id 108166`, name `debug-nocount-probe`) were removed before the final test run and commit. Confirmed no residue: `SELECT COUNT(*) FROM Games WHERE Name LIKE 'gsd-probe-%'` returns 0 after the full suite, and `debug-nocount-probe` no longer exists.

## User Setup Required

None - no external service configuration required. Migration 36 was applied directly to the local dev database (`sqlcmd -S localhost -d GamePlannerDB -E -C -b -I -i Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql`) as part of this execution, per the plan's precondition and action steps.

## Next Phase Readiness

- `GamePlanner.Tests` harness is in place for plan 04-02 (`SteamTokenStoreSweepTests.cs`) to add a file to without touching the `.csproj` or `.sln`
- `GameManager.cs`'s transaction pattern (`BeginTransactionAsync` / `GetDbTransaction()` / `CommitAsync`) is now the established precedent for any future Dapper+EF write path in this codebase
- No blockers for 04-02 (SteamTokenStore cleanup) or 04-03 (`sp_UpsertIGDBGame` fix / `IX_Games_IGDBId` index) -- both are independent of this plan's changes
- Flag for `/gsd-secure-phase` or a future audit: the `ANSI_NULLS`/`QUOTED_IDENTIFIER` gotcha discovered here likely affects any *other* stored procedure ever recreated via `sqlcmd` (as opposed to SSMS) against `Games` or any table gaining a filtered index in the future -- worth a repo-wide sweep if more `sqlcmd`-applied proc changes are planned

---
*Phase: 04-data-layer-reliability-audit-trail*
*Completed: 2026-08-31*

## Self-Check: PASSED

- FOUND: `Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql`
- FOUND: `GamePlanner.Tests/GamePlanner.Tests.csproj`
- FOUND: `GamePlanner.Tests/GameManagerAtomicityTests.cs`
- FOUND: `.planning/phases/04-data-layer-reliability-audit-trail/04-01-SUMMARY.md`
- FOUND commit: `16fe503`
- FOUND commit: `1b584c5`
