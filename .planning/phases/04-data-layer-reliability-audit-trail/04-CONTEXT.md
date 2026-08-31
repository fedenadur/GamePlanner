# Phase 4: Data Layer Reliability & Audit Trail - Context

**Gathered:** 2026-08-31
**Status:** Ready for planning

<domain>
## Phase Boundary

Game data writes become atomic and auditable, and the Steam token store no longer grows unbounded. Confined to `GameManager.cs` (Create/Update/Delete atomicity), the `Game`/`NowPlayingGame` entities and their stored procs (new `ModifiedAt`/`DeletedAt` fields), `SteamTokenStore.cs` plus a new cleanup `BackgroundService`, and — folded in from a pending todo — the cross-user `IGDBId` upsert collision bug in `sp_UpsertIGDBGame`/`GameManager.cs`/`GameImageManager.cs`. Requirements: REL-01, REL-02, AUDIT-01, AUDIT-02.

One pending todo — the IGDBId cross-user upsert collision (`Database/33_...sql`, `34_...sql`, partial `GameManager.cs`/`GameImageManager.cs` WIP already in the working tree) — was folded into this phase's scope at the user's explicit request, since it's squarely a data-layer reliability bug and Phase 2's own context had already flagged it as "natural fit for Phase 4."

</domain>

<decisions>
## Implementation Decisions

### Folded todo — IGDBId cross-user upsert collision
- **D-01:** Fold the drafted fix into this phase: apply `Database/33_RemoveUniqueIndex_IGDBId.sql` (drops the unique index on `IGDBId`, adds a non-unique one) and a corrected version of `Database/34_FixStoredProcedure_UpsertIGDBGame.sql`, plus the already-partial `GameManager.cs`/`GameImageManager.cs` WIP (see Code Context below for what's already there).
- **D-02:** Script 34's drafted `sp_UpsertIGDBGame` INSERT branch must be removed entirely — the proc becomes **UPDATE-only**, scoped by `(IGDBId, UserId)`. CONCERNS.md independently found the drafted INSERT hardcodes empty-string `Genre`/`Tags` and duplicates `sp_InsertGame`'s job; games must only ever be created through `sp_InsertGame`. — **Reversibility:** reversible — a stored-proc rewrite before it's ever applied.
- **D-03:** Include the small companion frontend fix: the delete action's error handler currently only logs an unhandled `HttpErrorResponse` to the console; it should surface a user-facing message instead.
- **D-04:** The dormant, unmapped `Games.UpdatedAt` DB column (leftover from the orphaned `sp_UpsertIGDBGame`, which no current C# code calls) is explicitly **not** touched by this fix — its fate is decided separately under Audit Fields below (D-08/D-09: a fresh `ModifiedAt` column is added instead, `UpdatedAt` stays dormant).

### REL-01 — Atomic Create/Update/Delete
- **D-05:** `GameManager`'s Create/Update paths (Dapper stored-proc call + separate EF `SaveChangesAsync` for genre/tag sync, currently on two independent connections) become one atomic unit by **sharing a transaction**: EF opens the connection/transaction first (`db.Database.BeginTransaction()`), and the same underlying `SqlConnection`+`SqlTransaction` is passed into the Dapper call (Dapper accepts an existing `IDbConnection`/`IDbTransaction`). Rejected alternative: moving genre/tag junction-table writes into the stored procs themselves (bigger surface, not chosen).
- **D-06:** `DeleteAsync` stays a **single SQL-side write** — `AUDIT-02`'s `DeletedAt` gets stamped inside `sp_DeleteGame` itself (`UPDATE Games SET IsActive = 0, DeletedAt = GETUTCDATE() WHERE Id = @Id`), not via a second EF write. No transaction-sharing needed for Delete since it never becomes two writes.
- **D-07:** Verify atomicity via an **automated test with an injected failure** — a targeted xUnit/integration test that forces a failure between the stored-proc write and the EF write (e.g., a constraint violation or thrown exception mid-`SaveChangesAsync`) and asserts the stored-proc row was rolled back too, not just left partially applied.

### AUDIT-01/AUDIT-02 — Audit fields
- **D-08:** In scope for `ModifiedAt`: **`Game` and `NowPlayingGame`** get a fresh `ModifiedAt` column each. `GameJournalEntry` is explicitly **left alone** — its existing `UpdatedAt` field (already stamped inside `sp_UpsertGameJournalEntry` via `GETUTCDATE()`) already satisfies AUDIT-01's intent under a different name; no rename.
- **D-09:** `Games.ModifiedAt` is a **fresh column**, not a reuse of the dormant `UpdatedAt` column — that column's original semantic (IGDB sync timestamp, per its use in the now-orphaned `sp_UpsertIGDBGame`) is different from "last user-modified," and conflating the two risks confusing meanings later. `UpdatedAt` stays dormant/unused (not dropped as part of this phase — out of scope, that's a separate cleanup).
- **D-10:** `Game.ModifiedAt` is stamped **SQL-side, inside `sp_UpdateGame`** — not via EF in `GameManager` — establishing one consistent rule: timestamps get stamped in T-SQL across this codebase (matches the `sp_UpsertGameJournalEntry` / `sp_DeleteGame`(`DeletedAt`, D-06) precedent), not scattered across C#.
- **D-11:** `NowPlayingGame.ModifiedAt` is stamped the same way — **SQL-side, inside both `sp_UpsertNowPlayingGame` and `sp_DeleteNowPlayingGame`** — consistent with D-10, and no transaction machinery needed since each of those is already a single write.

### REL-02 — SteamTokenStore cleanup
- **D-12:** The cleanup mechanism is a **`BackgroundService` matching the existing 6-worker pattern**, reporting to `WorkerStatus`/visible on the `/workers` dashboard — even though it's a pure in-memory sweep with no external calls or meaningful failure mode, consistency won over minimalism: every background task in this app should show up in one place.
- **D-13:** Sweep interval: **every 10 minutes** (matches `CONCERNS.md`'s own suggested interval). Log a line only when it actually removes expired entries (`"{Worker}: removed {N} expired token(s)."`) — avoid log noise on quiet sweeps, matching the existing worker logging convention (`LogInformation` on completion with something to report, `LogWarning` on partial failure).
- **D-14:** Verify via **both** an automated test (seed expired entries, assert the store shrinks after a sweep) **and** a manual live-check during phase verification (add a few Steam-login codes, wait past their 5-minute expiry, trigger the worker, confirm count drops) — matches the "Both" verification approach chosen for REL-01's atomicity proof.

### Claude's Discretion
- Exact transaction/connection-sharing implementation mechanism for D-05 (`db.Database.GetDbConnection()`/`GetDbTransaction()` vs. an explicit `SqlTransaction` passed through — whichever is cleanest given `GameManager`'s current `CreateConnection()` helper).
- Whether `sp_DeleteGame` sets `ModifiedAt` in addition to `DeletedAt` (D-06) — reasonable default is yes (a delete is technically a modification), but this wasn't explicitly asked; low-risk either way.
- `SteamTokenStore`'s actual sweep implementation — `ConcurrentDictionary` has no built-in `RemoveWhere`; the worker/store needs to enumerate keys and remove expired ones manually (thread-safe via `TryRemove`).
- Naming of the new cleanup worker class (e.g., `SteamTokenCleanupWorker`) and its `WorkerStatus` entry — follow the existing `{Noun}Worker` convention.
- Exact shape/wiring of the shared xUnit/integration test project if one doesn't already exist (TEST-01/02 are Phase 5's formal scope, but D-07/D-14 need *some* test infrastructure now — reuse whatever minimal harness gets set up, don't over-build ahead of Phase 5).

### Folded Todos
- **IGDBId upsert silently deactivates manually-added games** (`.planning/todos/pending/2026-08-29-igdbid-upsert-deactivates-games-across-users.md`, `Database/33_...sql`, `34_...sql`, `GameManager.cs:170`, major severity) — folded into Phase 4 scope at explicit user request (see D-01 through D-04). Already had a fix ~90% drafted in the working tree; Phase 2's context had flagged this as a "natural fit for Phase 4." Downstream planning should treat this as a self-contained data-integrity fix, sequenced early since REL-01's atomicity work also touches `GameManager.cs`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & requirements
- `.planning/ROADMAP.md` — Phase 4 section: goal, success criteria, REL-01/REL-02/AUDIT-01/AUDIT-02 requirements
- `.planning/REQUIREMENTS.md` — REL-01, REL-02, AUDIT-01, AUDIT-02 definitions

### Codebase state
- `.planning/codebase/CONCERNS.md` §"Resource Leaks" (SqlConnection disposal, SteamTokenStore memory accumulation), §"Data Integrity Issues" (sp_UpsertIGDBGame invalid-data INSERT), §"Missing Critical Features" (No Audit Logging, No Soft Delete Audit Trail) — original findings this phase fixes
- `.planning/codebase/ARCHITECTURE.md` — background worker (`IHostedService`) pattern, `WorkerStatus` monitoring convention
- `GamePlanner.API/Managers/GameManager.cs` — `CreateFromIgdbAsync` (line 92), `CreateAsync` (line 123), `UpdateAsync` (line 146), `DeleteAsync` (line 170) — the four methods REL-01/AUDIT-01/AUDIT-02 touch; already has uncommitted WIP for the IGDBId dedup check (lines 94-98)
- `GamePlanner.API/Managers/GameImageManager.cs` — already has uncommitted WIP handling one-IGDBId-to-many-local-games fan-out (`SaveImagesAsync`, lines 233-297), part of the folded fix
- `GamePlanner.API/Services/SteamTokenStore.cs` — the `ConcurrentDictionary`-backed store REL-02 adds cleanup for
- `GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs` — canonical `BackgroundService` + `WorkerStatus` pattern to follow for the new cleanup worker (D-12)
- `GamePlanner.API/Entities/Game.cs`, `NowPlayingGame.cs`, `GameJournalEntry.cs` — current entity shapes; `GameJournalEntry.UpdatedAt` (line 17) is the existing precedent for D-08
- `Database/07_StoredProcedures_IGDBSync.sql` — original (currently-applied) `sp_UpsertIGDBGame`, the orphaned proc with the dormant `Games.UpdatedAt` write (no C# caller exists)
- `Database/14_UpdateStoredProcedures_NormalizedSchema.sql` — current `sp_InsertGame`/`sp_UpdateGame` definitions (D-06/D-10 target)
- `Database/28_CreateGameJournalEntries.sql` — `sp_UpsertGameJournalEntry`'s `UpdatedAt = GETUTCDATE()` pattern, the precedent D-10/D-11 extend
- `Database/33_RemoveUniqueIndex_IGDBId.sql`, `Database/34_FixStoredProcedure_UpsertIGDBGame.sql` — drafted-but-unapplied fix scripts (D-01/D-02 apply and correct these)
- `.planning/todos/pending/2026-08-29-igdbid-upsert-deactivates-games-across-users.md` — folded todo, see Folded Todos above
- `game-planner-ui/src/app/components/...` (delete action's error handler — exact component TBD by researcher) — D-03's frontend fix location

### Project state
- `.planning/STATE.md` — Phase 4 dependency note: sequenced after Phase 3 per research risk-isolation ordering, no hard technical dependency
- `.planning/phases/02-credential-secrets-hardening/02-CONTEXT.md` `<deferred>` — original source of the "natural fit for Phase 4" flag for the IGDBId todo

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SteamPlaytimeSyncWorker.cs`'s `BackgroundService` + `WorkerStatusManager.SetRunningAsync/SetCompletedAsync/SetFailedAsync` pattern — directly reusable for the new cleanup worker (D-12).
- `sp_UpsertGameJournalEntry`'s `UpdatedAt = GETUTCDATE()` in the `UPDATE` branch — the exact SQL-side-stamping pattern D-10/D-11 replicate for `Game`/`NowPlayingGame`.
- Dapper's support for an externally-supplied `IDbConnection`/`IDbTransaction` — the mechanism D-05's shared-transaction decision relies on; no new library needed.

### Established Patterns
- Every stored-proc-driven timestamp in this codebase is currently set in T-SQL (`GETUTCDATE()`), never passed in from C# — D-10/D-11 follow this, not an exception to it.
- `GameManager` already mixes Dapper (writes) and EF (reads + junction-table sync) per this project's `CLAUDE.md` convention — REL-01 doesn't change that split, only makes the two writes transactionally consistent.
- Uncommitted WIP already exists for part of the folded IGDBId fix: `GameManager.CreateFromIgdbAsync` has a dedup check (`existing.Id` return-early), and `GameImageManager.SaveImagesAsync` already handles one `IGDBId` mapping to multiple local `GameId`s. This WIP should be built on, not redone.

### Integration Points
- `GameManager.cs` is the shared touch point for both REL-01 (atomicity) and the folded IGDBId fix (D-01) — sequence the IGDBId fix first since it changes `CreateFromIgdbAsync`'s shape before the transaction wrapping goes in.
- `Program.cs` needs the new cleanup `BackgroundService` registered (`builder.Services.AddHostedService<...>()`), following the same registration pattern as the existing 6 workers.
- `WorkerStatus` table/`WorkerStatusManager` gets a new seeded row for the cleanup worker (numbered-migration convention, like `30_SeedWorkerStatus_SteamPlaytimeSync.sql`).

</code_context>

<specifics>
## Specific Ideas

No specific external reference implementations mentioned. Across all four areas the user's guidance consistently favored the option that reused an existing, already-proven pattern in this exact codebase (SQL-side timestamp stamping, the `BackgroundService`+`WorkerStatus` convention, Dapper's transaction-sharing support) over introducing a new mechanism — matching the personal/single-developer, hardening-not-features framing of this milestone.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope; the one cross-scope item (IGDBId cross-user upsert bug) was explicitly folded in rather than deferred, per user decision.

### Reviewed Todos (not folded)
None this phase — the only pending-todo match (IGDBId upsert bug) was folded in, not deferred.

</deferred>

---

*Phase: 4-Data Layer Reliability & Audit Trail*
*Context gathered: 2026-08-31*
