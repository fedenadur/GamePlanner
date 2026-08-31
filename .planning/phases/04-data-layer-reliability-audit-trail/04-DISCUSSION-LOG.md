# Phase 4: Data Layer Reliability & Audit Trail - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-31
**Phase:** 4-Data Layer Reliability & Audit Trail
**Areas discussed:** Fold in IGDBId bug fix, Atomicity mechanism, Audit field scope & placement, Cleanup worker visibility

---

## Fold in IGDBId bug fix

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, fold it in | It's a data-layer reliability bug with a fix already ~90% drafted, exactly matching this phase's domain | ✓ |
| No, keep as separate pending todo | Leave it out of Phase 4's plan; handle it as its own standalone fix later | |

**User's choice:** Yes, fold it in.
**Notes:** Fix already partially drafted (`Database/33_...sql`, `34_...sql`, uncommitted `GameManager.cs`/`GameImageManager.cs` WIP). Phase 2's own context had flagged this as a "natural fit for Phase 4."

| Option | Description | Selected |
|--------|-------------|----------|
| Remove the INSERT branch | Per CONCERNS.md's own fix approach — games should only ever be created via `sp_InsertGame`; `sp_UpsertIGDBGame` becomes UPDATE-only | ✓ |
| Fix the INSERT values instead | Keep the INSERT branch but pass real Genre/Tags/EstimatedMinutes values instead of hardcoded empty string/60 | |

**User's choice:** Remove the INSERT branch.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, include it | Small, self-contained frontend consequence of the same bug being fixed here | ✓ |
| No, backend-only phase | Leave the frontend error-handling gap for a separate/future pass | |

**User's choice:** Yes, include it.

| Option | Description | Selected |
|--------|-------------|----------|
| Leave it for the audit discussion | Keep this area scoped to just the cross-user collision fix | ✓ |
| Map/reuse it here | Since already touching `sp_UpsertIGDBGame`, map `Games.UpdatedAt` into the entity now | |

**User's choice:** Leave it for the audit discussion.
**Notes:** Discovered mid-discussion: `Games.UpdatedAt` is a dormant, unmapped DB column left over from the now-orphaned `sp_UpsertIGDBGame` (which no current C# code calls at all).

---

## Atomicity mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Share a transaction across both | EF opens the connection/transaction first, passes the same connection+transaction into the Dapper call | ✓ |
| Move genre/tag sync into the stored proc too | Rewrite sp_InsertGame/sp_UpdateGame to also handle junction-table writes in T-SQL | |
| You decide | Let the researcher/planner pick the mechanism | |

**User's choice:** Share a transaction across both.

| Option | Description | Selected |
|--------|-------------|----------|
| Set DeletedAt inside sp_DeleteGame | SQL-side; keeps Delete a single atomic write, no transaction needed | ✓ |
| Set DeletedAt via EF, wrapped in the same shared transaction | Consistent mechanism across Create/Update/Delete, but adds transaction overhead to a path that doesn't need it | |

**User's choice:** Set it inside sp_DeleteGame.

| Option | Description | Selected |
|--------|-------------|----------|
| Automated test with an injected failure | Targeted xUnit/integration test forcing a failure between the two writes, asserting rollback | ✓ |
| Manual verification only | Claude manually triggers a failure during phase verification, no permanent test | |
| Both | Automated test AND manual live-proof | |

**User's choice:** Automated test with an injected failure.

---

## Audit field scope & placement

| Option | Description | Selected |
|--------|-------------|----------|
| Game + NowPlayingGame; leave GameJournalEntry as-is | GameJournalEntry's existing UpdatedAt already satisfies the intent under a different name | ✓ |
| Rename GameJournalEntry.UpdatedAt to ModifiedAt too | Standardize naming across all three entities literally | |

**User's choice:** Game + NowPlayingGame; leave GameJournalEntry as-is.

| Option | Description | Selected |
|--------|-------------|----------|
| Add a fresh ModifiedAt column | Cleaner — the dormant UpdatedAt column's original purpose (IGDB sync timestamp) differs from "last user-modified" semantics | ✓ |
| Repurpose the existing UpdatedAt column | Avoids a schema change, but conflates two different meanings | |

**User's choice:** Add a fresh ModifiedAt column.

| Option | Description | Selected |
|--------|-------------|----------|
| SQL-side, inside sp_UpdateGame | One consistent rule: timestamps stamped in T-SQL, matches sp_UpsertGameJournalEntry / sp_DeleteGame(DeletedAt) precedent | ✓ |
| C#-side, via EF right before SaveChangesAsync | Keeps timestamp logic in code next to genre/tag sync | |

**User's choice:** SQL-side, inside sp_UpdateGame.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, SQL-side inside both procs | Consistent with the Game/DeletedAt decision, no transaction machinery needed | ✓ |
| Something else | — | |

**User's choice:** Yes, SQL-side inside both procs (sp_UpsertNowPlayingGame/sp_DeleteNowPlayingGame).

---

## Cleanup worker visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, report to WorkerStatus | Consistency — every BackgroundService follows the same convention | ✓ |
| No, lightweight-only | Skip WorkerStatus entirely, just a BackgroundService with an internal loop and log lines | |

**User's choice:** Yes, report to WorkerStatus.

| Option | Description | Selected |
|--------|-------------|----------|
| Every 10 minutes, log a count | Matches CONCERNS.md's own suggested interval; log only when N > 0 | ✓ |
| Something else | — | |

**User's choice:** Every 10 minutes, log a count.

| Option | Description | Selected |
|--------|-------------|----------|
| Automated test + manual observation | Test seeding expired entries + a manual live-check during phase verification | ✓ |
| Automated test only | Skip the manual live-check | |

**User's choice:** Automated test + manual observation.

---

## Claude's Discretion

- Exact transaction/connection-sharing implementation mechanism for the atomicity fix (`db.Database.GetDbConnection()`/`GetDbTransaction()` vs. an explicit `SqlTransaction` passed through).
- Whether `sp_DeleteGame` sets `ModifiedAt` in addition to `DeletedAt` — not explicitly asked; reasonable default is yes.
- `SteamTokenStore`'s actual sweep implementation (`ConcurrentDictionary` has no built-in `RemoveWhere`; needs manual enumeration + `TryRemove`).
- Naming of the new cleanup worker class and its `WorkerStatus` entry — follow the existing `{Noun}Worker` convention.
- Exact shape/wiring of minimal test infrastructure needed for the two new automated tests (full test-project setup is formally Phase 5's scope).

## Deferred Ideas

None — discussion stayed within phase scope. The one cross-scope item (IGDBId cross-user upsert bug) was explicitly folded in rather than deferred.
