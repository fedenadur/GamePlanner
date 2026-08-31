# Phase 4: Data Layer Reliability & Audit Trail - Pattern Map

**Mapped:** 2026-08-31
**Files analyzed:** 10
**Analogs found:** 10 / 10

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `GamePlanner.API/Managers/GameManager.cs` (Create/Update transaction sharing) | manager | CRUD | itself (existing file, in-place edit) | exact |
| `GamePlanner.API/Managers/GameImageManager.cs` (fan-out WIP, no phase 4 change expected beyond what's already there) | manager | batch | itself | exact |
| `Database/33_RemoveUniqueIndex_IGDBId.sql` | migration | schema-change | `Database/27_CreateNowPlaying.sql` (numbered migration convention) | role-match |
| `Database/34_FixStoredProcedure_UpsertIGDBGame.sql` (corrected, INSERT branch removed) | migration/stored-proc | CRUD | `Database/28_CreateGameJournalEntries.sql`'s `sp_UpsertGameJournalEntry` (UPDATE branch + GETUTCDATE stamping) | exact |
| New migration: add `Games.ModifiedAt`, `NowPlayingGames.ModifiedAt` columns + update `sp_UpdateGame`/`sp_DeleteGame`/`sp_UpsertNowPlayingGame`/`sp_DeleteNowPlayingGame` | migration/stored-proc | CRUD | `Database/28_CreateGameJournalEntries.sql` (`UpdatedAt = GETUTCDATE()` precedent) + `Database/14_UpdateStoredProcedures_NormalizedSchema.sql` (current `sp_UpdateGame`) + `Database/27_CreateNowPlaying.sql` (current `sp_UpsertNowPlayingGame`/`sp_DeleteNowPlayingGame`) | exact |
| `GamePlanner.API/Entities/Game.cs` (add `ModifiedAt` property) | model | CRUD | itself | exact |
| `GamePlanner.API/Entities/NowPlayingGame.cs` (add `ModifiedAt` property) | model | CRUD | `GamePlanner.API/Entities/GameJournalEntry.cs` (`UpdatedAt` property precedent) | exact |
| `GamePlanner.API/Services/SteamTokenStore.cs` (add sweep/removal method) | service (in-memory store) | event-driven | itself | exact |
| `GamePlanner.API/Workers/SteamTokenCleanupWorker.cs` (new) | worker | event-driven / batch | `GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs` | exact |
| `GamePlanner.API/Program.cs` (register new worker) | config | request-response (DI wiring) | existing `AddHostedService<...>()` block (lines 47-52) | exact |
| New migration: seed `WorkerStatus` row for cleanup worker | migration | CRUD | `30_SeedWorkerStatus_SteamPlaytimeSync.sql` referenced in CONTEXT.md (not found on disk — see "No Analog Found"; use `WorkerStatusManager.GetOrCreateAsync` self-seeding as fallback, or the pattern of other numbered seed scripts) | role-match |
| `game-planner-ui/src/app/components/game-list/game-list.component.ts` (`deleteGame` error handler) | component | request-response | itself (`loadGames()`'s `error` handler, same file, lines 32-38) | exact |
| Integration/xUnit test project (new, for D-07/D-14) | test | request-response | none exists yet — see "No Analog Found" | none |

## Pattern Assignments

### `GamePlanner.API/Managers/GameManager.cs` (manager, CRUD) — REL-01 transaction sharing

**Analog:** itself — `CreateAsync`/`UpdateAsync` (lines 123-168), `CreateConnection()` helper (lines 13-14)

**Current connection pattern** (lines 11-14):
```csharp
public class GameManager(GamePlannerDbContext db, IConfiguration config)
{
    private SqlConnection CreateConnection() =>
        new(config.GetConnectionString("DefaultConnection"));
```

**Current two-write (non-atomic) Update pattern** (lines 146-168):
```csharp
public async Task<bool> UpdateAsync(int id, UpdateGameRequest request, int userId)
{
    var game = await db.Games
        .Include(g => g.Genres) /* ...other Includes... */
        .FirstOrDefaultAsync(g => g.Id == id && g.IsActive && g.UserId == userId);

    if (game is null) return false;

    using var conn = CreateConnection();
    await conn.ExecuteAsync("sp_UpdateGame",
        new { Id = id, request.Name, request.EstimatedMinutes },
        commandType: System.Data.CommandType.StoredProcedure);

    await SyncGenresAsync(game, request.Genres);
    await SyncTagsAsync(game, request.Tags);
    await db.SaveChangesAsync();

    return true;
}
```

**Pattern to apply (D-05):** Wrap the Dapper call + `SaveChangesAsync` in a shared transaction. Use `db.Database.BeginTransactionAsync()` to get an EF-owned `IDbContextTransaction`, then pass its underlying connection/transaction into the Dapper call via `commandType`/`transaction:` parameter overload:
```csharp
await using var tx = await db.Database.BeginTransactionAsync();
var conn = (SqlConnection)db.Database.GetDbConnection();
var sqlTx = (SqlTransaction)tx.GetDbTransaction();

await conn.ExecuteAsync("sp_UpdateGame",
    new { Id = id, request.Name, request.EstimatedMinutes },
    transaction: sqlTx,
    commandType: System.Data.CommandType.StoredProcedure);

await SyncGenresAsync(game, request.Genres);
await SyncTagsAsync(game, request.Tags);
await db.SaveChangesAsync();
await tx.CommitAsync();
```
This replaces the separate `CreateConnection()`/`using var conn` pattern for `CreateAsync`/`CreateFromIgdbAsync`/`UpdateAsync` only — `DeleteAsync` (lines 170-181) stays untouched per D-06 (single SQL-side write, no transaction needed).

**Existing WIP already in tree (D-01 base to build on)** — `CreateFromIgdbAsync` (lines 92-121) already has the dedup check:
```csharp
var existing = await db.Games
    .FirstOrDefaultAsync(g => g.IGDBId == request.IgdbId && g.UserId == userId && g.IsActive);

if (existing is not null)
    return existing.Id;
```

**Error handling pattern:** No explicit try/catch in `GameManager` — relies on ASP.NET Core's default exception middleware and EF's automatic rollback-on-exception when a transaction isn't committed. Match this convention (no new try/catch wrapper unless a test requires asserting on a specific exception type).

---

### `GamePlanner.API/Managers/GameImageManager.cs` (manager, batch) — folded IGDBId fix context

**Analog:** itself — `SaveImagesAsync` (lines 263-297) already handles the one-IGDBId-to-many-local-games fan-out via `Dictionary<long, List<int>>`:
```csharp
foreach (var localGameId in localGameIds)
{
    await conn.ExecuteAsync("sp_UpsertGameImage",
        new { GameId = localGameId, IgdbImageId = image.ImageId, ImageType = imageType, Url = url, Width = image.Width, Height = image.Height },
        commandType: System.Data.CommandType.StoredProcedure);
}
```
No further change expected here per D-01 — this WIP should be built on, not redone. Confirm during planning that no other method in this file assumes IGDBId uniqueness across users.

---

### `Database/34_FixStoredProcedure_UpsertIGDBGame.sql` (stored-proc, CRUD) — D-02 correction

**Analog:** `Database/28_CreateGameJournalEntries.sql` — `sp_UpsertGameJournalEntry` UPDATE branch (lines 37-48), the exact "UPDATE-only, scoped by two keys" shape to replicate:
```sql
IF EXISTS (SELECT 1 FROM GameJournalEntries WHERE UserId = @UserId AND GameId = @GameId)
    UPDATE GameJournalEntries
       SET Status = @Status, ... , UpdatedAt = GETUTCDATE()
     WHERE UserId = @UserId AND GameId = @GameId;
```

**Current drafted (WRONG per D-02) version** — `Database/34_FixStoredProcedure_UpsertIGDBGame.sql` lines 10-36 has an `ELSE BEGIN INSERT INTO Games (...) VALUES ('', 60, '', ...) END` branch that must be removed entirely. The corrected proc becomes:
```sql
CREATE OR ALTER PROCEDURE sp_UpsertIGDBGame
    @IGDBId BIGINT, @Name NVARCHAR(100), @IGDBGenres NVARCHAR(300),
    @FirstReleaseDate DATETIME2, @UpdatedAt DATETIME2, @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
       SET Name = @Name, IGDBGenres = @IGDBGenres,
           FirstReleaseDate = @FirstReleaseDate, UpdatedAt = @UpdatedAt
     WHERE IGDBId = @IGDBId AND UserId = @UserId;
END
GO
```
(No `IF EXISTS`/INSERT branch — UPDATE-only, no-op if no matching row, per D-02.)

---

### New migration: `Games.ModifiedAt` / `NowPlayingGames.ModifiedAt` columns + SQL-side stamping — D-08 through D-11

**Analog:** `Database/28_CreateGameJournalEntries.sql` (UpdatedAt precedent) + `Database/14_UpdateStoredProcedures_NormalizedSchema.sql` (current `sp_UpdateGame`) + `Database/27_CreateNowPlaying.sql` (current `sp_UpsertNowPlayingGame`/`sp_DeleteNowPlayingGame`).

**Current `sp_UpdateGame`** (`Database/14_UpdateStoredProcedures_NormalizedSchema.sql` lines 22-35):
```sql
CREATE OR ALTER PROCEDURE sp_UpdateGame
    @Id INT, @Name NVARCHAR(100), @EstimatedMinutes INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET Name = @Name, EstimatedMinutes = @EstimatedMinutes
    WHERE Id = @Id AND IsActive = 1;
END
```
**Pattern to apply (D-10):** add `ModifiedAt = GETUTCDATE()` to the SET clause (new numbered migration, e.g. `35_AddModifiedAt_AuditTrail.sql`, using `ALTER TABLE ... ADD ModifiedAt DATETIME2 NULL` + `CREATE OR ALTER PROCEDURE sp_UpdateGame ... SET ..., ModifiedAt = GETUTCDATE() ...`).

**Current `sp_DeleteGame`** — not shown in files read this pass; locate via Grep (`sp_DeleteGame` referenced in `GameManager.DeleteAsync`, line 176) in whichever numbered script defines it, and add `DeletedAt = GETUTCDATE()` (D-06) and optionally `ModifiedAt = GETUTCDATE()` (Claude's Discretion, default yes) to its `UPDATE Games SET IsActive = 0, ...` statement.

**Current `sp_UpsertNowPlayingGame`/`sp_DeleteNowPlayingGame`** (`Database/27_CreateNowPlaying.sql` lines 14-40):
```sql
CREATE OR ALTER PROCEDURE sp_UpsertNowPlayingGame
    @UserId INT, @GameId INT, @Slot INT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM NowPlayingGames WHERE UserId = @UserId AND Slot = @Slot)
        UPDATE NowPlayingGames
           SET GameId = @GameId, AddedAt = GETUTCDATE()
         WHERE UserId = @UserId AND Slot = @Slot;
    ELSE
        INSERT INTO NowPlayingGames (UserId, GameId, Slot)
        VALUES (@UserId, @GameId, @Slot);
END
GO

CREATE OR ALTER PROCEDURE sp_DeleteNowPlayingGame
    @UserId INT, @Slot INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM NowPlayingGames WHERE UserId = @UserId AND Slot = @Slot;
END
```
**Pattern to apply (D-11):** add `ModifiedAt DATETIME2 NULL` column via `ALTER TABLE NowPlayingGames ADD ModifiedAt DATETIME2 NULL`, then stamp `ModifiedAt = GETUTCDATE()` in the `sp_UpsertNowPlayingGame` UPDATE branch (INSERT branch can leave it NULL or set on insert too — planner's call) — no equivalent stamp needed in `sp_DeleteNowPlayingGame` since it's a hard DELETE, not a soft-delete/update (unlike `Games`).

---

### `GamePlanner.API/Entities/Game.cs` / `NowPlayingGame.cs` (model, CRUD) — D-08

**Analog:** `GamePlanner.API/Entities/GameJournalEntry.cs` line 17 — the `UpdatedAt` property precedent:
```csharp
public DateTime  UpdatedAt      { get; set; }
```
**Pattern to apply:** add nullable `DateTime? ModifiedAt { get; set; }` to `Game.cs` (after `CreatedAt`, line 9) and to `NowPlayingGame.cs` (after `AddedAt`, line 9), matching existing nullable `DateTime?` conventions already used elsewhere in `Game.cs` (e.g. `LastImageSyncDate`, `IGDBUpdatedAt`, line 21/27).

---

### `GamePlanner.API/Services/SteamTokenStore.cs` (service, event-driven) — REL-02

**Analog:** itself — current implementation, full file (24 lines):
```csharp
public class SteamTokenStore
{
    private readonly ConcurrentDictionary<string, (AuthResponse Response, DateTime Expiry)> _store = new();

    public string Store(AuthResponse response)
    {
        var code = Guid.NewGuid().ToString("N");
        _store[code] = (response, DateTime.UtcNow.AddMinutes(5));
        return code;
    }

    public AuthResponse? Redeem(string code)
    {
        if (!_store.TryRemove(code, out var entry)) return null;
        if (entry.Expiry < DateTime.UtcNow) return null;
        return entry.Response;
    }
}
```
**Pattern to apply (D-14 discretion):** add a `SweepExpired()` (or `int RemoveExpired()`) method enumerating `_store` keys and calling `TryRemove` for entries where `Expiry < DateTime.UtcNow`, returning the removed count for the worker's logging:
```csharp
public int RemoveExpired()
{
    var removed = 0;
    foreach (var kvp in _store)
    {
        if (kvp.Value.Expiry < DateTime.UtcNow && _store.TryRemove(kvp.Key, out _))
            removed++;
    }
    return removed;
}
```

---

### `GamePlanner.API/Workers/SteamTokenCleanupWorker.cs` (worker, new) — D-12/D-13

**Analog:** `GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs` — full file, canonical `BackgroundService` + `WorkerStatus` pattern (57 lines, read in full).

**Imports pattern** (lines 1-3):
```csharp
using GamePlanner.API.Managers;

namespace GamePlanner.API.Workers;
```

**Core BackgroundService + interval loop pattern** (lines 5-27):
```csharp
public class SteamPlaytimeSyncWorker(IServiceScopeFactory scopeFactory, ILogger<SteamPlaytimeSyncWorker> logger)
    : BackgroundService
{
    public const string WorkerName = "SteamPlaytimeSyncWorker";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("{Worker} started.", WorkerName);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await RunAsync(ct);
                await Task.Delay(Interval, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("{Worker} stopped.", WorkerName);
        }
    }
```
**Pattern to apply (D-13):** identical shape, but `Interval = TimeSpan.FromMinutes(10)`, `WorkerName = "SteamTokenCleanupWorker"`, and since this worker has no manager/DB-dependent business logic (it's an in-memory sweep), inject `SteamTokenStore` directly instead of resolving a `Manager` via scope — still resolve `WorkerStatusManager` via scope for status reporting:

**WorkerStatus + try/catch/log pattern** (lines 29-56):
```csharp
public async Task RunAsync(CancellationToken ct)
{
    logger.LogInformation("{Worker} triggered at {Time}.", WorkerName, DateTime.UtcNow);

    await using var scope = scopeFactory.CreateAsyncScope();
    var statusManager = scope.ServiceProvider.GetRequiredService<WorkerStatusManager>();

    try
    {
        await statusManager.SetRunningAsync(WorkerName, ct);
        var removed = tokenStore.RemoveExpired();
        await statusManager.SetCompletedAsync(WorkerName);

        if (removed > 0)
            logger.LogInformation("{Worker}: removed {N} expired token(s).", WorkerName, removed);
    }
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
}
```
Note D-13's "log a line only when it actually removes expired entries" — matches the `if (removed > 0)` guard above, mirroring `SteamPlaytimeSyncWorker`'s `if (failedCount > 0)` convention (line 43).

**Registration pattern** — `GamePlanner.API/Program.cs` lines 47-52:
```csharp
builder.Services.AddHostedService<GameSyncWorker>();
builder.Services.AddHostedService<GameImageSyncWorker>();
builder.Services.AddHostedService<SteamImportWorker>();
builder.Services.AddHostedService<GameHLTBSyncWorker>();
builder.Services.AddHostedService<UpcomingGamesSyncWorker>();
builder.Services.AddHostedService<SteamPlaytimeSyncWorker>();
```
Add `builder.Services.AddHostedService<SteamTokenCleanupWorker>();` as a new line following this exact pattern.

**WorkerStatus self-seeding fallback:** `WorkerStatusManager.GetOrCreateAsync` (`GamePlanner.API/Managers/WorkerStatusManager.cs` lines 47-55) auto-creates a `WorkerStatus` row on first `SetRunningAsync` call if none exists — a dedicated seed migration is optional (matches existing worker convention referenced in CONTEXT.md as `30_SeedWorkerStatus_SteamPlaytimeSync.sql`, but that file was not found on disk during this scan; confirm its existence/necessity during planning, or rely on self-seeding).

---

### `game-planner-ui/src/app/components/game-list/game-list.component.ts` (component, request-response) — D-03

**Analog:** itself — `loadGames()`'s `error` handler, same file (lines 32-38), the established error-surfacing convention:
```typescript
loading = false;
error = '';
...
loadGames() {
  this.loading = true;
  this.gameService.getGames().subscribe({
    next: (data) => { this.games = data; this.loading = false; },
    error: () => { this.error = 'Could not load games. Is the API running?'; this.loading = false; }
  });
}
```

**Current unhandled delete pattern** (lines 131-132):
```typescript
deleteGame(id: number) {
  this.gameService.deleteGame(id).subscribe({ next: () => this.loadGames() });
}
```
**Pattern to apply (D-03):** add an `error` callback reusing the existing `error: string` component property, matching the `loadGames()` convention:
```typescript
deleteGame(id: number) {
  this.gameService.deleteGame(id).subscribe({
    next: () => this.loadGames(),
    error: () => { this.error = 'Could not delete game. Please try again.'; }
  });
}
```
The template (`game-list.component.html`) already has a binding for `error` (used by `loadGames()`) — reuse it, no new template markup needed unless the existing `error` binding is scoped only to the initial-load state (verify in `.html` during planning).

**Alternate/secondary analog** — `game-planner-ui/src/app/components/journal/journal.component.ts` `deleteEdit()` (lines 133-144) shows a *sibling* component with the same gap (delete subscribe has an `error` callback but it only resets a loading flag, doesn't surface a message) — not a positive pattern to copy, but confirms this is a systemic gap, not unique to `game-list`. Scope per CONTEXT.md D-03 is `game-list.component.ts` only.

---

## Shared Patterns

### SQL-side timestamp stamping
**Source:** `Database/28_CreateGameJournalEntries.sql` (`sp_UpsertGameJournalEntry`, `UpdatedAt = GETUTCDATE()`)
**Apply to:** All new/modified stored procs touching `Games` and `NowPlayingGames` for `ModifiedAt`/`DeletedAt` (D-06, D-10, D-11). Never pass timestamps in from C# — this is a hard codebase-wide convention, not just a suggestion.

### BackgroundService + WorkerStatus worker pattern
**Source:** `GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs` (full file)
**Apply to:** `SteamTokenCleanupWorker.cs` — reuse `WorkerName` const, `Interval` static field, `ExecuteAsync` while-loop shape, `RunAsync` try/catch/status-update shape exactly.

### Dapper transaction-sharing with EF
**Source:** No existing example in codebase (new pattern for REL-01) — use `db.Database.BeginTransactionAsync()` + `db.Database.GetDbConnection()`/`tx.GetDbTransaction()` cast to `SqlConnection`/`SqlTransaction`, pass `transaction:` into `Dapper.ExecuteAsync`/`ExecuteScalarAsync` calls.
**Apply to:** `GameManager.CreateAsync`, `CreateFromIgdbAsync`, `UpdateAsync` only (not `DeleteAsync`, per D-06).

### Numbered SQL migration convention
**Source:** `Database/27_CreateNowPlaying.sql`, `Database/28_CreateGameJournalEntries.sql`, `Database/33_...sql`, `Database/34_...sql`
**Apply to:** New `ModifiedAt` column migration — next available number after 34 (confirm current max via `Database/` directory listing at planning time, since this scan found up to 34).

### Frontend error-surfacing via component `error` property
**Source:** `game-planner-ui/src/app/components/game-list/game-list.component.ts` `loadGames()` (lines 32-38)
**Apply to:** `deleteGame()` in the same file (D-03).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| Integration/xUnit test project for D-07 (atomicity rollback test) and D-14 (token sweep test) | test | request-response | No test project currently exists in the repo (only `Database/`, `GamePlanner.API/`, `game-planner-ui/` found at root — no `*.Tests.csproj`). Planner must scaffold minimal xUnit harness per CONTEXT.md's Claude's Discretion note ("reuse whatever minimal harness gets set up, don't over-build ahead of Phase 5"). |
| `30_SeedWorkerStatus_SteamPlaytimeSync.sql` (referenced in CONTEXT.md as the seed-migration precedent) | migration | CRUD | File not found on disk during this scan (`Database/` numbered files jump from 28 to 33/34 in what was checked) — verify its existence at planning time; if absent, rely on `WorkerStatusManager`'s self-seeding (`GetOrCreateAsync`) instead of writing a seed script. |
| `sp_DeleteGame` current definition | stored-proc | CRUD | Not located in the files read this pass (referenced by `GameManager.DeleteAsync` but its defining script wasn't opened) — planner/implementer should `Grep "sp_DeleteGame"` across `Database/*.sql` to find the exact current definition before editing for D-06. |

## Metadata

**Analog search scope:** `GamePlanner.API/Managers/`, `GamePlanner.API/Services/`, `GamePlanner.API/Workers/`, `GamePlanner.API/Entities/`, `GamePlanner.API/Managers/WorkerStatusManager.cs`, `Database/*.sql` (07, 14, 27, 28, 33, 34), `GamePlanner.API/Program.cs`, `game-planner-ui/src/app/components/game-list/`, `game-planner-ui/src/app/components/journal/`
**Files scanned:** 15
**Pattern extraction date:** 2026-08-31
