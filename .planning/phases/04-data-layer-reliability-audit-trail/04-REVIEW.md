---
phase: 04-data-layer-reliability-audit-trail
reviewed: 2026-08-31T00:00:00Z
depth: standard
files_reviewed: 15
files_reviewed_list:
  - Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql
  - GamePlanner.API/Entities/Game.cs
  - GamePlanner.API/Entities/NowPlayingGame.cs
  - GamePlanner.API/Managers/GameManager.cs
  - GamePlanner.Tests/GamePlanner.Tests.csproj
  - GamePlanner.Tests/GameManagerAtomicityTests.cs
  - GamePlanner.sln
  - GamePlanner.API/Services/SteamTokenStore.cs
  - GamePlanner.API/Workers/SteamTokenCleanupWorker.cs
  - GamePlanner.API/Program.cs
  - Database/37_SeedWorkerStatus_SteamTokenCleanup.sql
  - GamePlanner.Tests/SteamTokenStoreSweepTests.cs
  - Database/33_RemoveUniqueIndex_IGDBId.sql
  - Database/34_FixStoredProcedure_UpsertIGDBGame.sql
  - game-planner-ui/src/app/components/game-list/game-list.component.ts
findings:
  critical: 1
  warning: 5
  info: 2
  total: 8
status: issues_found
---

# Phase 04: Code Review Report

**Reviewed:** 2026-08-31T00:00:00Z
**Depth:** standard
**Files Reviewed:** 15
**Status:** issues_found

## Summary

Reviewed all three plans (04-01 atomicity/audit stamps, 04-02 Steam token sweep, 04-03 IGDBId
cross-user fix) at the code level, cross-referencing each `git diff` hunk against the pre-phase
base commit to isolate exactly what this phase changed vs. what was pre-existing.

The atomicity work in `GameManager` (shared EF transaction wrapping `sp_InsertGame`/`sp_UpdateGame`
+ `SaveChangesAsync`) is sound and is proven by real-DB tests. However, 04-03's fix for the
IGDBId cross-user collision bug traded a too-strict DB constraint (global unique index) for an
**app-only** duplicate check with no replacement DB-level guard — this reopens a race window that
can produce duplicate active `Games` rows for the same user, which directly undercuts the phase's
own "data layer reliability" goal. `UpdateAsync` also stops one step short of the fix `DeleteAsync`
received in the same commit (ignoring the stored procedure's affected-row count), and the new
`SteamTokenCleanupWorker` has a status-reporting double-fault path that can permanently kill its
own loop. Test files are functionally sound but are real-SQL-Server integration tests with no
skip/guard logic, which is a CI fragility risk worth flagging even though it's out of primary scope.

## Critical Issues

### CR-01: TOCTOU race lets duplicate active Games rows be created per (IGDBId, UserId)

**File:** `GamePlanner.API/Managers/GameManager.cs:95-99` (see also `Database/33_RemoveUniqueIndex_IGDBId.sql:9-11`)

**Issue:** Prior to this phase, `Games.IGDBId` had a **global** unique index (`UX_Games_IGDBId`),
which caused a real bug: two different users could never each own the same IGDB title (D-01,
fixed by 04-03). The fix in `Database/33_RemoveUniqueIndex_IGDBId.sql` drops that index entirely
and replaces it with a **non-unique** filtered index — no DB-level uniqueness constraint of any
kind remains on `(IGDBId, UserId)`. The only remaining duplicate guard is the application-level
check added in `CreateFromIgdbAsync`:

```csharp
var existing = await db.Games
    .FirstOrDefaultAsync(g => g.IGDBId == request.IgdbId && g.UserId == userId && g.IsActive);

if (existing is not null)
    return existing.Id;

await using var tx = await db.Database.BeginTransactionAsync();
// ... sp_InsertGame runs unconditionally past this point
```

This is a classic check-then-act race: two concurrent calls to `CreateFromIgdbAsync` for the same
`(IgdbId, userId)` (double-submit on a slow connection, a retried request, or overlapping sync
paths) can both observe `existing is null` before either transaction commits, and both will insert
a row via `sp_InsertGame`. The result is two active `Games` rows for the same user pointing at the
same IGDB title — silent data duplication with no error, no rollback, and nothing in the schema to
prevent it. The 04-01 commit message even documents this as a conscious tradeoff ("dedup
short-circuit runs before the transaction opens, unchanged (D-01)") without addressing the
concurrency gap it reopens once 04-03 removed the DB-level constraint.

**Fix:** Restore a DB-level guard scoped correctly this time — a filtered unique index on
`(UserId, IGDBId)` for active rows — so the database, not just app logic, prevents the duplicate:

```sql
CREATE UNIQUE NONCLUSTERED INDEX UX_Games_UserId_IGDBId_Active
    ON Games (UserId, IGDBId)
    WHERE IGDBId IS NOT NULL AND IsActive = 1;
```

And handle the resulting unique-violation in `CreateFromIgdbAsync` as the tie-breaker for the race
(catch the constraint violation, re-query for the existing row, return its Id) instead of relying
solely on the pre-transaction check.

## Warnings

### WR-01: UpdateAsync ignores sp_UpdateGame's affected-row count, unlike the DeleteAsync fix in the same commit

**File:** `GamePlanner.API/Managers/GameManager.cs:153-178`

**Issue:** This same phase (04-01) explicitly fixed a bug where `DeleteAsync`'s `rows > 0` check
always evaluated false because `ExecuteAsync` returns `-1` under `SET NOCOUNT ON` — the fix was to
have `sp_DeleteGame` `SELECT @@ROWCOUNT` and switch to `ExecuteScalarAsync<int>`. `UpdateAsync`
was not given the same treatment:

```csharp
await conn.ExecuteAsync("sp_UpdateGame",
    new { Id = id, request.Name, request.EstimatedMinutes },
    transaction: tx.GetDbTransaction(),
    commandType: System.Data.CommandType.StoredProcedure);

await SyncGenresAsync(game, request.Genres);
await SyncTagsAsync(game, request.Tags);
await db.SaveChangesAsync();
await tx.CommitAsync();

return true;   // unconditional
```

If the target row is soft-deleted between the initial `FirstOrDefaultAsync` ownership/active check
and the `sp_UpdateGame` call (e.g., a concurrent `DeleteAsync` on the same game), `sp_UpdateGame`'s
`WHERE Id = @Id AND IsActive = 1` guard makes the `UPDATE` a silent no-op, yet `UpdateAsync` still
commits the transaction (updating only the genre/tag join tables, which have no `IsActive` filter)
and returns `true` to the caller as if the edit succeeded. This is exactly the failure mode the
phase's own atomicity tests target, but it isn't covered.

**Fix:** Mirror the `DeleteAsync` pattern — have `sp_UpdateGame` `SELECT @@ROWCOUNT`, switch to
`ExecuteScalarAsync<int>`, and short-circuit (roll back, return `false`) when it's `0`:

```csharp
var rows = await conn.ExecuteScalarAsync<int>("sp_UpdateGame",
    new { Id = id, request.Name, request.EstimatedMinutes },
    transaction: tx.GetDbTransaction(),
    commandType: System.Data.CommandType.StoredProcedure);

if (rows == 0) return false;
```

### WR-02: sp_UpdateGame / sp_DeleteGame rely solely on the app-layer ownership check, not DB-level UserId scoping

**File:** `Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql:38-51,64-77`, `GamePlanner.API/Managers/GameManager.cs:153-191`

**Issue:** Project convention (per CLAUDE.md architecture notes) is "All queries filter by UserId
(user-scoped data)." Both procedures touched by this migration filter only by `Id`
(`WHERE Id = @Id AND IsActive = 1`), not `UserId`. Safety currently depends entirely on
`GameManager` performing an `IsActive && UserId == userId` check in EF immediately beforehand.
That's correct today, but it means any future direct caller of `sp_UpdateGame`/`sp_DeleteGame`
(another manager, an admin script, a bug that reorders the two calls) silently loses the
per-user isolation guarantee — there's no defense-in-depth at the data layer these procedures live in.

**Fix:** Add `@UserId INT` to both procedures and include `AND UserId = @UserId` in the `WHERE`
clause, passing `userId` from `GameManager` alongside `Id`.

### WR-03: SteamTokenCleanupWorker can permanently die on a status-reporting double-fault

**File:** `GamePlanner.API/Workers/SteamTokenCleanupWorker.cs:37-55`

**Issue:**

```csharp
catch (OperationCanceledException)
{
    await statusManager.SetIdleAsync(WorkerName);
    logger.LogWarning("{Worker} was cancelled.", WorkerName);
}
catch (Exception ex)
{
    await statusManager.SetFailedAsync(WorkerName, ex.Message);
    logger.LogError(ex, "{Worker} failed.", WorkerName);
}
```

If the original failure that lands in `catch (Exception ex)` is itself a DB connectivity problem
(e.g. `SetRunningAsync` at the top of the try block failed because SQL Server was briefly
unreachable), then `statusManager.SetFailedAsync(...)` — which also needs the DB — is very likely
to throw too. That second exception is not caught anywhere inside `RunAsync`, so it propagates out
of the method entirely. The outer loop in `ExecuteAsync` only catches `OperationCanceledException`:

```csharp
try
{
    while (!ct.IsCancellationRequested)
    {
        await RunAsync(ct);
        await Task.Delay(Interval, ct);
    }
}
catch (OperationCanceledException) { ... }
```

An uncaught non-cancellation exception here terminates `ExecuteAsync`'s task for the remaining
lifetime of the process — the 10-minute sweep never runs again until the app is restarted, with
only the unobserved-task-exception log (if any) as a trace.

**Fix:** Wrap the status-manager calls inside each catch block in their own try/catch (log-and-swallow),
or add a top-level `catch (Exception ex)` around the `while` loop in `ExecuteAsync` that logs and lets
the loop continue after the next `Interval` rather than letting the task fault permanently.

### WR-04: GameManagerAtomicityTests hard-fail without a live, pre-seeded SQL Server — no skip/guard

**File:** `GamePlanner.Tests/GameManagerAtomicityTests.cs:21-43,65,111,151,192,230`

**Issue:** Every test opens a real `SqlConnection`/`DbContext` against
`ConnectionStrings__DefaultConnection` or a hardcoded `localhost` fallback, and assumes at least one
row already exists in `Users` (`await db.Users.Select(u => u.Id).FirstAsync()` — throws
`InvalidOperationException` on an empty table). There is no `[Trait]`/category to exclude these from
a default `dotnet test` run, no DB-availability check, and no seed step to guarantee a `Users` row.
Any CI pipeline or fresh clone that runs `dotnet test` without a already-migrated, already-seeded
SQL Server instance will get hard failures indistinguishable from a real regression, and the
failure mode ("Sequence contains no elements") does not point at the actual cause.

**Fix:** Tag these with `[Trait("Category", "Integration")]` and document/wire up an opt-out filter
(`dotnet test --filter Category!=Integration`) for environments without a DB, and/or add a
`CollectionFixture` that seeds a throwaway user row if `Users` is empty instead of assuming one exists.

### WR-05: CreateFromIgdbAsync's dedup check and the create path share one DbContext transaction, but the check itself runs outside any lock/transaction

**File:** `GamePlanner.API/Managers/GameManager.cs:93-125`

**Issue:** This is the concurrency root cause behind CR-01, called out separately here because it's
independently fixable at the application layer even before/without the schema change in CR-01: the
existence check (`db.Games.FirstOrDefaultAsync(...)`) executes on its own implicit transaction/read,
fully outside the `BeginTransactionAsync()` block that follows. Even with a DB-level unique index in
place (per CR-01's fix), the current code does not catch the resulting unique-constraint violation —
it would surface as an unhandled `DbUpdateException` from `SaveChangesAsync`/the SP call instead of
gracefully returning the winning row's Id, so callers (e.g. a sync worker) would see an unexpected
500 on the losing side of the race rather than the intended dedup behavior.

**Fix:** See CR-01's fix — add a catch for the unique-constraint violation around the transaction
block, re-query for `existing`, and return its Id instead of letting the exception surface.

## Info

### IN-01: New ModifiedAt/DeletedAt audit columns are not surfaced anywhere outside the database

**File:** `GamePlanner.API/Entities/Game.cs:10-11`, `GamePlanner.API/Entities/NowPlayingGame.cs:10`

**Issue:** Neither `GameMapping` nor any DTO exposes `ModifiedAt`/`DeletedAt` to the API/frontend.
That may be intentional for this phase (schema + stored-proc groundwork only), but as delivered,
the "audit trail" this phase is named for is currently invisible to anyone but a DBA running ad hoc
queries — worth confirming this matches phase intent before considering the audit-trail goal complete.

**Fix:** If a later phase is expected to surface this (admin view, "last edited" UI, etc.), note the
gap in follow-up planning; otherwise no action needed.

### IN-02: sp_UpsertIGDBGame has zero application callers

**File:** `Database/34_FixStoredProcedure_UpsertIGDBGame.sql`

**Issue:** A repo-wide search found no caller of `sp_UpsertIGDBGame` in `GamePlanner.API`. The
migration comment acknowledges this ("never through this orphaned sync-only procedure"), so this is
a deliberate call, but shipping a "fix" to dead code without a caller or a test means the corrected
`(IGDBId, UserId)` scoping is unverified in practice — if/when a caller is eventually wired up, it
will get its first real exercise with no regression coverage.

**Fix:** Either remove the orphaned procedure or add a caller + a Dapper-level test proving the
`(IGDBId, UserId)`-scoped `UPDATE` behaves as documented, so it isn't shipped as untested dead code.

---

_Reviewed: 2026-08-31T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
