# Phase 6: Game Images & Metadata - Pattern Map

**Mapped:** 2026-10-27
**Files analyzed:** 14 (7 modified, 7 new)
**Analogs found:** 14 / 14 (script 39 and the manager tests have exact or strong analogs; the `_Test` DB harness is the only partial one)

Line numbers refer to the working tree as read (including the uncommitted WIP). Do not rewrite the WIP; extend it.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `GamePlanner.API/Managers/GameImageManager.cs` (M: D-01, D-02) | manager | batch | itself (`SyncScreenshotsAndArtworksAsync` is the cursor-less sibling; `GameSyncManager.RunAsync` for paging) | exact |
| `GamePlanner.API/Managers/GameSyncManager.cs` (M: matcher) | manager | batch / request-response | itself (`EnrichGameAsync` lines 85-93) | exact |
| `GamePlanner.API/Services/IgdbService.cs` (M: `FindGameIdBySteamAppIdAsync`) | service (external API) | request-response | `FetchScreenshotsByGameIdsAsync` (lines 114-119, non-`games` endpoint) | exact |
| `GamePlanner.API/Domain/IgdbModels.cs` (M: `IgdbExternalGame`) | model (DTO) | transform | `IgdbImage` (lines 89-108) | exact |
| `GamePlanner.API/Controllers/GameImagesController.cs` (M: `gameId` filter, F1) | controller | request-response (read, EF LINQ) | itself (lines 14-35) | exact |
| `game-planner-ui/src/app/services/game.service.ts` (M: `gameId` param) | service (HTTP client) | request-response | itself (`getGameImages`, lines 117-121) | exact |
| `game-planner-ui/src/app/components/game-detail/game-detail.component.ts` (M: Escape, pass `game.id`) | component | event-driven | itself (WIP lines 61-123) | exact |
| `Database/39_FixPortalIgdbId.sql` (new) | migration (one-off data fix) | batch / transform | `Database/38_FixHalfLifeIgdbId.sql` | exact |
| `GamePlanner.Tests/GameImageManagerTests.cs` (new: T1a-c, T2) | test | batch (stubbed IGDB + real SQL) | `GameManagerAtomicityTests.cs` + `IgdbServiceContractTests.Build()` | role-match (combine both) |
| `GamePlanner.Tests/GameSyncManagerMatcherTests.cs` (new: T3a-d) | test | request-response | same two analogs | role-match |
| `GamePlanner.Tests/GameImagesControllerTests.cs` (new: T4) | test | request-response | `GameManagerAtomicityTests.cs` (`CreateContext`, cleanup) | partial (no existing controller-level test) |
| `GamePlanner.Tests/IgdbServiceContractTests.cs` (M, optional: external_games contract fact) | test | request-response | `FetchScreenshots_PostsToScreenshotsEndpoint` (lines 129-139) | exact |
| `e2e/tests/uat.spec.ts` (M: lightbox describe block) | test (e2e) | event-driven | `workers` describe (lines 200-244) + `workflows` `api()` helper (lines 58-65) | role-match |
| `GamePlanner.Tests` `_Test` DB guard / fixture base (new, optional) | test support | config | `GameManagerAtomicityTests.ConnectionString` / `CreateContext` (lines 21-31) | partial |

## Pattern Assignments

### `GamePlanner.API/Managers/GameImageManager.cs` (manager, batch) - D-01, D-02

**Analog:** same file, plus `GameSyncManager.RunAsync` for the loop shape. Edit in place; leave the WIP `igdbToLocal`/`SaveImagesAsync` hunks (lines 236-242, 263-297) untouched.

**Current cover loop to change** (lines 70-79, 100-105, 107-113, 125-128):
```csharp
while (!ct.IsCancellationRequested)
{
    var batch = await db.Games
        .Where(g => g.IsActive && g.CoverImageUrl == null)
        .OrderBy(g => g.Id)
        .Take(BatchSize)
        .Select(g => new { g.Id, g.Name, g.IGDBId, g.IGDBUpdatedAt })
        .ToListAsync(ct);
    if (batch.Count == 0) break;
    ...
    await SaveCoverAsync(conn, game.Id, game.Name, game.IGDBUpdatedAt, result);   // both withId and withoutId call sites
    ...
    processed += batch.Count;
    if (batch.Count < BatchSize) break;
}
```
**Target shape (D-02 cursor):** declare `int lastId = 0;` next to `int processed = 0;` (line 66), add `&& g.Id > lastId` to the `Where`, drop `g.IGDBUpdatedAt` from the `Select`, and set `lastId = batch[^1].Id;` right after `processed += batch.Count;`. Keep both `break`s.

**SaveCoverAsync target (D-01)** (current lines 135-160; delete the `currentUpdatedAt` parameter and the lines 147-151 skip block, keep the rest):
```csharp
private async Task SaveCoverAsync(SqlConnection conn, int gameId, string name, IgdbGame? result)
{
    if (result?.Cover is null) { logger.LogDebug("No cover found for '{Name}'.", name); return; }
    var igdbUpdatedAt = DateTimeOffset.FromUnixTimeSeconds(result.UpdatedAt).UtcDateTime;
    var url = IgdbService.OptimizeCoverUrl(result.Cover.Url);
    await conn.ExecuteAsync("sp_UpdateGameCover",
        new { Id = gameId, CoverImageUrl = url, IGDBUpdatedAt = igdbUpdatedAt },
        commandType: System.Data.CommandType.StoredProcedure);
}
```
Pattern notes to preserve: writes only through `sp_*` via Dapper `conn.ExecuteAsync(..., commandType: StoredProcedure)`; reads via `db.Games` LINQ; `catch (OperationCanceledException) { throw; }` before generic catch; `Task.Delay(250, ct)` after each IGDB name search. Optional: line 126 passes `batch.Count` as `{Total}`; harmless pre-existing log bug.

---

### `GamePlanner.API/Services/IgdbService.cs` (service, request-response) - `FindGameIdBySteamAppIdAsync`

**Analog:** `FetchScreenshotsByGameIdsAsync` (lines 114-119): a non-`games` endpoint reached through the generic `PostQueryAsync<T>(url, query, ct)`.

**Constants block to extend** (lines 10-13):
```csharp
private const string TokenUrl       = "https://id.twitch.tv/oauth2/token";
private const string GamesUrl       = "https://api.igdb.com/v4/games";
private const string ScreenshotsUrl = "https://api.igdb.com/v4/screenshots";
private const string ArtworksUrl    = "https://api.igdb.com/v4/artworks";
// add: private const string ExternalGamesUrl = "https://api.igdb.com/v4/external_games";
```

**Method to copy** (lines 114-119):
```csharp
public async Task<List<IgdbImage>> FetchScreenshotsByGameIdsAsync(IEnumerable<long> igdbIds, CancellationToken ct)
{
    var ids   = string.Join(",", igdbIds);
    var query = $"fields id,image_id,url,width,height,game; where game = ({ids}); limit 500;";
    return await PostQueryAsync<IgdbImage>(ScreenshotsUrl, query, ct);
}
```
New method follows the same body: inline interpolated query, `int` interpolation (no injection), `PostQueryAsync<IgdbExternalGame>(ExternalGamesUrl, query, ct)`, return `long?`. Query per RESEARCH: `fields uid,game; where uid = "{steamAppId}" & external_game_source = 1 & game != null; limit 5;` (uid quoted; `external_game_source = 1` mandatory; never use `category`). Place it in the `// ── Queries ──` region next to the other fetchers.

**Do not touch** `SearchGameAsync` (lines 65-73): it already does exact case-insensitive name match then first hit (`results.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)) ?? results.FirstOrDefault()`), which covers D-09 steps 2-3.

### `GamePlanner.API/Domain/IgdbModels.cs` (model, transform) - `IgdbExternalGame`

**Analog:** `IgdbImage` (lines 89-108). Same attribute style, flat DTO, `[JsonPropertyName]` snake_case mapping:
```csharp
public class IgdbImage
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("game")]
    public long Game { get; set; }
}
```
New class: `Id` (`long`), `Uid` (`string`, `[JsonPropertyName("uid")]`), `Game` (`long?`, `[JsonPropertyName("game")]`). Uid is a JSON string, so the property must be `string`.

---

### `GamePlanner.API/Managers/GameSyncManager.cs` (manager, batch) - matcher in `EnrichGameAsync`

**Analog:** same file. Insertion points:

**Batch projection to extend** (line 49):
```csharp
.Select(g => new { g.Id, g.IGDBId, g.Name, g.IGDBUpdatedAt })   // add g.SteamAppId
```
**Call site** (line 60): `await EnrichGameAsync(item.Id, item.IGDBId, item.Name, item.IGDBUpdatedAt, ct, force);` add a `steamAppId` argument (new parameter before `ct` to keep the "CancellationToken last, optional bool after" convention already used here).

**Branch to replace** (lines 89-93):
```csharp
var result = igdbId.HasValue
    ? await igdb.FetchEnrichmentByIdAsync(igdbId.Value, ct)
    : await igdb.SearchGameAsync(name, ct);
if (result is null) return true;
```
Target logic (selection lives in the manager, per project rule):
```csharp
IgdbGame? result;
if (igdbId.HasValue)
    result = await igdb.FetchEnrichmentByIdAsync(igdbId.Value, ct);
else
{
    result = null;
    if (steamAppId.HasValue)
    {
        var mapped = await igdb.FindGameIdBySteamAppIdAsync(steamAppId.Value, ct);
        if (mapped.HasValue) result = await igdb.FetchEnrichmentByIdAsync(mapped.Value, ct);
    }
    result ??= await igdb.SearchGameAsync(name, ct);
}
```
Needs `using GamePlanner.API.Domain;` (not currently imported in this file; line 1-4 imports Context, Entities, Services, EF). Existing persistence stays: `game.IGDBId ??= result.Id;` (line 152). Error handling stays: outer `catch (OperationCanceledException) { throw; } catch (Exception ex) { logger.LogWarning(ex, "Failed to enrich '{Name}', skipping.", name); return false; }` (lines 167-172). Do not alter the `Skip(offset)` paging (RESEARCH Pitfall 3, out of scope).

---

### `GamePlanner.API/Controllers/GameImagesController.cs` (controller, request-response) - F1 `gameId` filter

**Analog:** same file, lines 14-35. Keep `[Authorize]`, `GetUserId()` check, and the user-scoped base query; add the filter additively:
```csharp
[HttpGet]
public async Task<IActionResult> Get(
    [FromQuery] string type    = "screenshot",
    [FromQuery] long?  igdbId  = null,
    // add: [FromQuery] int? gameId = null,
    CancellationToken  ct      = default)
{
    var userId = GetUserId();
    if (userId is null) return Unauthorized();

    var query = db.GameImages
        .Where(i => i.Game.UserId == userId && i.Game.IsActive && i.ImageType == type);

    // target: if (gameId.HasValue) query = query.Where(i => i.GameId == gameId.Value);
    //         else if (igdbId.HasValue) query = query.Where(i => i.Game.IGDBId == igdbId.Value);
```
Keep the `UserId` predicate (ASVS V4); another user's `gameId` returns `[]`.

### `game-planner-ui/src/app/services/game.service.ts` (HTTP service) - `gameId` param

**Analog:** same, lines 117-121:
```typescript
getGameImages(type = 'screenshot', igdbId?: number | null): Observable<GameImage[]> {
  const params: Record<string, string | number> = { type };
  if (igdbId != null) params['igdbId'] = igdbId;
  return this.http.get<GameImage[]>(`${this.api}/gameimages`, { params });
}
```
Add a third optional `gameId?: number | null` and `if (gameId != null) params['gameId'] = gameId;`. Single quotes, 2-space indent, explicit `Observable<T>`.

### `game-planner-ui/src/app/components/game-detail/game-detail.component.ts` (component, event-driven) - D-12 + F1

**Analog:** same file (WIP). **Escape** (lines 92-97):
```typescript
@HostListener('window:keydown', ['$event'])
handleLightboxKeydown(event: KeyboardEvent) {
  if (this.lightboxIndex === null) return;
  if (event.key === 'ArrowRight') this.nextImage();
  else if (event.key === 'ArrowLeft') this.prevImage();
  // add: else if (event.key === 'Escape') this.closeLightbox();
}
```
**F1 call site:** line 51 `this.loadScreenshots(g.igdbId)` and lines 61-67:
```typescript
private loadScreenshots(igdbId: number | null) {
  this.screenshots = [];
  if (!igdbId) return;
  this.gameService.getGameImages('screenshot', igdbId).subscribe({
    next: (imgs) => { this.screenshots = imgs; }
  });
}
```
Change to pass `g.id` as well and call `getGameImages('screenshot', igdbId, gameId)`; keep the `if (!igdbId) return;` guard. Do not alter `setLightboxIndex`/`triggerFade`/`preloadNeighbors`/wrap logic (lines 78-123).

---

### `Database/39_FixPortalIgdbId.sql` (migration, one-off data fix)

**Analog:** `Database/38_FixHalfLifeIgdbId.sql` (copy structure verbatim, single-row variant).

**Header skeleton to mirror** (lines 1-35): title line, "Phase 06 / D-05/D-06", authored date, "Applied MANUALLY in SSMS by the user (project convention)", lookup-evidence block, CONVENTION EXEMPTION paragraph (lines 21-25), effect paragraph (lines 27-33), then REVERSAL block (lines 35-61). Evidence to cite (from RESEARCH): Steam AppId 400 -> `external_games` -> game 71; 71 = Portal, `first_release_date` 2007-10-10, developer Valve; wrong 14546 = Portal 1986-11-01 Nexa/Activision; app search `limit 3` cannot surface id 71. Note in header that GameTags (TagId 15) is left alone so after resync Tags stays "Action" (D-06 consequence). Unlike 38, Portal HAS GameImages rows (4464, 4467), so REVERSAL should re-INSERT them or say they are rebuilt by the image worker. Re-capture pre-fix values live immediately before authoring and compare with the RESEARCH table.

**Body to copy** (lines 62-117), adapted to `Id = 107693`, `IGDBId = 14546 -> 71`:
```sql
USE GamePlannerDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM Games WHERE Id = 107693 AND UserId = 2 AND IGDBId = 14546)
BEGIN
    DELETE FROM GameGenres             WHERE GameId = 107693;
    DELETE FROM GameThemes             WHERE GameId = 107693;
    DELETE FROM GamePlayerPerspectives WHERE GameId = 107693;
    DELETE FROM GameModesRelation      WHERE GameId = 107693;
    DELETE FROM GameImages             WHERE GameId = 107693;

    UPDATE Games
    SET IGDBId = 71,
        FirstReleaseDate = NULL,
        AggregatedRating = NULL,
        AggregatedRatingCount = NULL,
        CoverImageUrl = NULL,
        IGDBUpdatedAt = NULL,
        LastIGDBSyncAt = NULL,
        LastImageSyncDate = NULL,
        ModifiedAt = GETUTCDATE()
    WHERE Id = 107693 AND UserId = 2 AND IGDBId = 14546;
END

COMMIT TRANSACTION;
GO
```
REVERSAL format (lines 39-60): commented `DELETE` of re-derived junction rows first, then `UPDATE Games SET IGDBId = 14546, FirstReleaseDate = '1986-11-01T00:00:00', CoverImageUrl = '.../t_cover_big/coaanx.jpg', AggregatedRating = 80.0, AggregatedRatingCount = 0, IGDBUpdatedAt = '2026-08-28T02:26:32', LastIGDBSyncAt = '2026-08-30T00:58:28.0118486', LastImageSyncDate = '2026-10-27T20:08:52.467', ModifiedAt = NULL WHERE Id = 107693 AND UserId = 2;` then `INSERT` genres (1,3,7), theme 1, perspective 1, mode 3, and GameImages. Owner checkpoint (D-05) before apply; Claude does not run it.

---

### `GamePlanner.Tests/GameImageManagerTests.cs` and `GameSyncManagerMatcherTests.cs` (tests, stubbed IGDB + real SQL)

**Analogs:** compose two files. Do NOT use `ApiFactory` for manager tests (RESEARCH: it is for HTTP-level tests); the precedent for managers is `GameManagerAtomicityTests`.

**IGDB stub builder to copy** from `IgdbServiceContractTests.cs` (lines 18-42):
```csharp
private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
    new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

private static bool IsTokenHost(HttpRequestMessage r) => r.RequestUri!.Host == "id.twitch.tv";

var handler = new RecordingHandler { Responder = r => IsTokenHost(r)
    ? Json($$"""{"access_token":"tok","expires_in":3600}""") : Json("[]") };
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    { ["IGDB:ClientId"] = ApiFactory.TestIgdbClientId, ["IGDB:ClientSecret"] = ApiFactory.TestIgdbSecret })
    .Build();
var logger = LoggerFactory.Create(b => b.AddProvider(logs)).CreateLogger<IgdbService>();
new IgdbService(new StubHttpClientFactory(handler), config, logger)
```
Route stub responses by `r.RequestUri!.AbsolutePath` (`/v4/games`, `/v4/screenshots`, `/v4/artworks`, `/v4/external_games`) and, for `games`, by request body (`search "` vs `where id = (` vs `where id = 71`). Read the body in the responder from `handler.Requests` is not possible (body is captured, not passed); instead capture via `r.Content` is disposed, so key the responder on `AbsolutePath` and use a counter/queue or make the stub JSON depend on a closure variable set per test. Assertions use `handler.Requests` (`Uri`, `Body`), as in lines 51-68 and 136-138.

**DB wiring to copy** from `GameManagerAtomicityTests.cs` (lines 19-43):
```csharp
private const string NamePrefix = "gsd-probe-";

private static string ConnectionString =>
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Server=localhost;Database=GamePlannerDB;Trusted_Connection=True;TrustServerCertificate=True;";

private static GamePlannerDbContext CreateContext()
{
    var options = new DbContextOptionsBuilder<GamePlannerDbContext>().UseSqlServer(ConnectionString).Options;
    return new GamePlannerDbContext(options);
}
// config for managers needing GetConnectionString("DefaultConnection"):
new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["ConnectionStrings:DefaultConnection"] = ConnectionString }).Build();
```
Construct managers by hand: `new GameImageManager(db, igdb, config, logger)` (needs both IGDB and ConnectionStrings config keys in one `IConfiguration`) and `new GameSyncManager(db, igdb, logger)`.

**Probe row + cleanup pattern** (lines 46-57, 62-103): unique name `$"{NamePrefix}{Guid.NewGuid():N}"`, user via `await db.Users.Select(u => u.Id).FirstAsync()` (prefer a non-owner user, never UserId 2 data; RESEARCH Pitfall 1), `try { ... } finally { await CleanupByNamePrefixAsync(name); }`. Cleanup must also delete `GameImages` and `GameThemes`/`GamePlayerPerspectives`/`GameModesRelation` for probe ids (extend the `DELETE` list at lines 52-54; `GameImages` has FK to Games). Use `Random.Shared.NextInt64(1_000_000_000, 2_000_000_000)` for fake IGDB ids (line 263). Probe row creation for these tests needs `IGDBUpdatedAt`/`CoverImageUrl NULL`/`LastImageSyncDate NULL`/`SteamAppId`: insert via Dapper/EF directly in test arrange (tests may; production writes stay SP-only).

**Safety (mandatory):** pass `new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token` to every `RunAsync`, so a regressed cursor fails instead of hanging (RESEARCH). Recommended `GamePlannerDB_Test` DB with a name-suffix guard; see "No Analog" note.

---

### `GamePlanner.Tests/GameImagesControllerTests.cs` (test, request-response) - T4

**Analog (partial):** `GameManagerAtomicityTests` for DbContext/cleanup. No existing controller-level test; construct directly:
`new GameImagesController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")) } } }`, then `var ok = Assert.IsType<OkObjectResult>(await controller.Get("screenshot", null, gameId, ct))`. Claim type must be `ClaimTypes.NameIdentifier` (controller lines 37-41). Include the cross-user assertion (other user's `gameId` returns empty).

---

### `e2e/tests/uat.spec.ts` (e2e test, event-driven) - lightbox block (D-16)

**Analog:** the `workers` describe (lines 200-244) for polling/worker-run, and the `workflows` `api()` helper (lines 58-65) for authenticated requests.

**Helpers to reuse** (lines 4-16, 58-65, 203-209):
```typescript
const API = process.env.UAT_API_URL ?? 'https://localhost:7174';
function bearer(): Record<string, string> { /* reads .auth/uat.json gp_token */ }

function api(request: APIRequestContext) {
  const headers = bearer();
  return { get: (path: string) => request.get(`${API}${path}`, { headers }), ... };
}
```
Note: `api()` is scoped inside `describe('workflows')` (line 58) and `worker()` inside `describe('workers')` (line 203); a new `describe('lightbox')` must either define its own copies or hoist them to file scope. A POST helper does not exist yet (only get/put/del); fixture creation needs `request.post(`${API}/api/games/from-igdb`, { headers: bearer(), data })`.

**Polling pattern to copy** (lines 227-237): `await expect.poll(async () => ..., { timeout, intervals: [2_000] }).toBe(...)`, `test.setTimeout(...)` for long waits. Worker trigger via UI card: `page.locator('.worker-card').filter({ has: page.locator('.worker-name', { hasText: ... }) })` then `getByRole('button', { name: /Run now/ })`.

**Navigation convention** (line 18-22): `page.goto(...)` then `expect(page.getByRole('heading', ...)).toBeVisible()`. Place the new block inside the outer `test.describe.serial('GamePlanner UAT', ...)` (line 24). Avoid names starting `uat-` or equal to `Celeste` for fixture games (workflows cleanup deletes those). Selectors per RESEARCH: `.carousel-item`, `.lightbox-overlay`, `.lightbox-img`, `.lightbox-arrow-prev`/`-next`, `.lightbox-counter`, `.lightbox-close`. Preload assertion: `page.on('request', r => requested.push(r.url()))` as at lines 217-218, filter `t_1080p`.

## Shared Patterns

### Writes via stored procedure, reads via EF LINQ
**Source:** `GameImageManager.cs` lines 155-157, 248-250, 284-294
**Apply to:** every manager change; test arrange may insert rows directly, production code may not
```csharp
await conn.ExecuteAsync("sp_UpdateGameCover",
    new { Id = gameId, CoverImageUrl = url, IGDBUpdatedAt = igdbUpdatedAt },
    commandType: System.Data.CommandType.StoredProcedure);
```
Exception: `Database/39` uses raw guarded UPDATE/DELETE with the documented CONVENTION EXEMPTION header (copy from script 38 lines 21-25).

### Cancellation and per-item failure handling in workers
**Source:** `GameImageManager.cs` lines 107-123; `GameSyncManager.cs` lines 167-172
**Apply to:** cover loop, matcher
```csharp
catch (OperationCanceledException) { throw; }
catch (Exception ex)
{
    logger.LogWarning(ex, "Cover search failed for '{Name}', skipping.", game.Name);
    failedCount++;
    failedNames.Add(game.Name);
}
```
Workers return `(int FailedCount, string? FailureDetail)`; keep `Task.Delay` between IGDB calls (250 ms).

### IGDB HTTP access
**Source:** `IgdbService.PostQueryAsync<T>` (lines 146-166)
**Apply to:** the new external-games query. Token caching, `Bearer` + `Client-ID` headers, `text/plain` body, error logging (status/body only, no secrets) are already handled; add no new HTTP code.

### Controller auth and user scoping
**Source:** `GameImagesController.cs` lines 9-12, 20-24, 37-41
**Apply to:** the `gameId` filter. `[Authorize]`, `GetUserId()` from `ClaimTypes.NameIdentifier`, `i.Game.UserId == userId` always present.

### Test hygiene
**Source:** `GameManagerAtomicityTests.cs` lines 19, 46-57, 99-102
**Apply to:** all new DB-touching tests. Probe-prefix names, `try/finally` cleanup that also removes new child tables (`GameImages`), bounded CancellationToken, never touch UserId 2 rows.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `GamePlannerDB_Test` provisioning + `_Test` name-suffix guard (test base class) | test support | config | No existing test base class or scratch DB; existing tests hit the dev DB with probe rows only. Use `GameManagerAtomicityTests.ConnectionString` (env `ConnectionStrings__DefaultConnection`) as the seam and add a guard that throws unless the DB name ends in `_Test`. Building the DB from `Database/*.sql` is unverified (RESEARCH A1); fallback is documented in RESEARCH "Pitfall 1". |
| Controller-level test harness (`ControllerContext` with claims) | test | request-response | No controller is unit-tested today (only HTTP-level via `ApiFactory`). Construct manually as described above. |
| e2e POST helper / lightbox fixture creation | test | request-response | `uat.spec.ts` helpers only expose get/put/del; add a POST to `/api/games/from-igdb` (body per RESEARCH: `{ igdbId, name, estimatedMinutes, genres: [], coverUrl: null }`). |

## Metadata

**Analog search scope:** `GamePlanner.API/{Managers,Services,Domain,Controllers}`, `Database/38`, `GamePlanner.Tests/**`, `game-planner-ui/src/app/{services,components/game-detail}`, `e2e/tests/uat.spec.ts`
**Files scanned:** 15 read, plus targeted greps
**Pattern extraction date:** 2026-10-27
**Read-only:** no source files modified; WIP in `GameImageManager.cs` and `game-detail.component.*` untouched.
