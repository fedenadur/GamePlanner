# Phase 6: Game Images & Metadata - Research

**Researched:** 2026-10-27
**Domain:** ASP.NET Core 8 managers + Dapper SPs (IGDB image/cover sync), IGDB v4 `external_games`, Angular 22 lightbox, xUnit + Playwright verification
**Confidence:** HIGH (IGDB shape and DB state verified live, read-only; code read directly). MEDIUM on the test-isolation recommendation (scratch DB not tried, because creating a DB is not read-only).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Cover fetch (IMG-01)**
- **D-01:** Remove the `IGDBUpdatedAt` freshness skip from `GameImageManager.SaveCoverAsync` entirely. `SyncCoversAsync` only selects rows with `CoverImageUrl IS NULL`, so the skip can never apply correctly there. `sp_UpdateGameCover` still stamps `IGDBUpdatedAt`. The same change applies to the `withoutId` (name-search) path, which calls the same method.
- **D-02:** Fix the cover loop's re-fetch bug in the same change. Today it re-queries "first 50 rows with NULL cover" on every iteration, so 50 or more rows that IGDB has no cover for are fetched again and again until cancellation. Page with an Id cursor (`Id > lastId`) so each row is tried once per run.
- **D-03:** No data script for Half-Life (107683) and Half-Life 2 (107687). After the code fix, one non-force `GameImageSyncWorker` run must fill their covers, and that run is the proof for success criterion 1.

**Shared IGDBId images (IMG-02)**
- **D-04:** Keep the WIP as written: `igdbToLocal` becomes `Dictionary<long, List<int>>` and `sp_UpsertGameImage` runs once per local game. Review it, commit it, and verify it with a test (D-15) and a live check (D-17).

**Portal fix (IMG-03)**
- **D-05:** Claude looks up Valve's Portal on IGDB (exact title "Portal", release year 2007) through the app's search and/or a direct IGDB id query, and writes the evidence into the script header the way script 38 does. The owner confirms the ID at a checkpoint before applying the script. - **Reversibility:** one-way (practically) - it writes a live row in the owner's DB. The script's REVERSAL block makes it recoverable.
- **D-06:** Script `Database/39_FixPortalIgdbId.sql` mirrors `38_FixHalfLifeIgdbId.sql`:
  - It is guarded on the pre-fix `(Id=107693, UserId=2, IGDBId=14546)`.
  - It runs in a transaction with `XACT_ABORT`.
  - It sets the correct IGDBId.
  - It clears the IGDB-derived scalars: `FirstReleaseDate`, `AggregatedRating*`, `CoverImageUrl`, `IGDBUpdatedAt`, `LastIGDBSyncAt`, `LastImageSyncDate`.
  - It deletes the `GameGenres`, `GameThemes`, `GamePlayerPerspectives`, `GameModesRelation` and `GameImages` rows. `GameTags` is left alone.
  - It sets `ModifiedAt = GETUTCDATE()`.
  - It includes the convention-exemption note and a REVERSAL block holding the pre-fix values, captured live just before authoring.

  The owner applies it manually in SSMS. Afterwards a non-force `GameSyncWorker` run and then a `GameImageSyncWorker` run rebuild the row.
- **D-07:** Run a read-only SQL scan for other likely wrong matches (for example, an IGDB `FirstReleaseDate` implausible for a Steam game, or a name mismatch against the IGDB title) and list the suspects in the phase summary. Only Portal is fixed in this phase. The scan changes no data.

**IGDB matcher for new matches (IMG-03 prevention)**
- **D-08:** The matcher runs only in `GameSyncManager.EnrichGameAsync`, on the branch where the row has no IGDBId and the code calls `igdb.SearchGameAsync(name)`.
  - The legacy `GameEnrichmentManager` stays as it is.
  - `GameImageManager`'s `SearchCoverByNameAsync` stays as it is (it stores no IGDBId).
  - The Add-game IGDB search UI stays as it is.
- **D-09:** Order:
  1. If the row has a `SteamAppId`, resolve it through IGDB's external-games mapping (Steam uid -> IGDB game). This is exact.
  2. Otherwise, or when there is no mapping, use the name search and prefer an exact case-insensitive name match among the results.
  3. If no name matches exactly, fall back to the first hit, as today.

  The new IGDB query lives in `IgdbService` (Services are for external APIs). The selection logic lives in the manager. The researcher must confirm the current IGDB external-games query shape (endpoint, Steam source/category field and uid field), since IGDB has changed that model.
- **D-10:** Only new matches use the new matcher. Already-matched rows are not re-matched. Suspects from D-07 are reported, not changed.

**Lightbox (UI-01)**
- **D-11:** Keep the WIP: prev/next buttons, Left/Right keys, wrap at both ends, an "N / M" counter, neighbor preload and the 180ms fade.
- **D-12:** Add Escape to close in the existing `handleLightboxKeydown` handler.
- **D-13:** Only screenshots can be browsed. Artworks keep syncing but are not added to the carousel or lightbox.
- **D-14:** Keep the 20ms class-toggle fade. Neighbor preload covers "no visible load delay". Verification confirms that.

**Verification**
- **D-15:** Each change gets a targeted xUnit test in `GamePlanner.Tests`, using the existing `ApiFactory`, `StubHttpClientFactory` and `RecordingHandler` (stubbed IGDB plus real SQL Server):
  - **Shared IGDBId:** two local games with one IGDBId both get the `GameImages` rows.
  - **NULL cover:** a row with `IGDBUpdatedAt` set and no cover gets a cover, and the cover loop terminates when IGDB returns no cover for a full batch.
  - **Matcher:** SteamAppId resolves through external games; an exact name beats the first hit; with no exact name it falls back to the first hit.
- **D-16:** Lightbox gets a new re-runnable test in `e2e/tests/uat.spec.ts`, run as `uat-test@gameplanner.local`. It opens a game with 2+ screenshots and checks the buttons, arrow keys, wrap at both ends, the counter and Escape. The test needs a fixture game with 2+ screenshots in the UAT user's library, so the planner must provide it (seed or add via IGDB in the test).
- **D-17:** Data outcomes are verified live:
  - Half-Life covers
  - Portal showing 2007 data and the matching cover after script 39 and the re-sync
  - shared-IGDBId games showing the same screenshots

  After the owner applies script 39, Claude triggers non-force worker runs through the API and checks the rows with SQL and the game-detail pages (Playwright). The owner's only manual step is applying the script.

### Claude's Discretion
- Exact SQL heuristics for the D-07 suspect scan.
- How the IGDB external-games lookup is shaped in `IgdbService` (single vs batch), following existing method conventions.

### Deferred Ideas (OUT OF SCOPE)
- Using the improved matcher in `GameEnrichmentManager` and the cover name search: not chosen, sync path only (D-08).
- Re-matching suspects found by the D-07 scan: needs a per-row decision, so a future todo if the scan finds any.
- Showing artworks in the carousel or lightbox: new display capability.
- Touch/swipe navigation in the lightbox: new capability, not discussed.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| IMG-01 | NULL-cover game gets a cover even when `IGDBUpdatedAt` is set | "Code State" (SaveCoverAsync skip + cursor), "Cover loop design", Test T1. Live proof row is NieR:AutomataT (107889), not Half-Life (see Finding F2). |
| IMG-02 | Screenshots/artworks saved for every local game sharing an IGDBId | WIP review (correct), Finding F1 (carousel duplicates through `GET /api/gameimages?igdbId=`), live pair exists after matcher run (Dark Messiah 107697/107698). |
| IMG-03 | Portal gets Valve's 2007 IGDBId via guarded reversible script 39 + matcher | IGDB id **71** with evidence, live captured pre-fix state for REVERSAL, external_games query shape verified, 49-row suspect list. |
| UI-01 | Lightbox prev/next, arrow keys, wrap, counter, preload | WIP review, Escape gap, e2e fixture + preload assertion strategy. |
</phase_requirements>

## Summary

The phase is four small, well-bounded changes on top of a WIP that is almost done. The WIP backend change (`igdbToLocal` as `Dictionary<long, List<int>>`) is correct as written. The WIP lightbox already has prev/next, ArrowLeft/ArrowRight with wrap, "N / M" counter, neighbor preload and the 20 ms fade toggle. What is missing versus the decisions: Escape (D-12), the `SaveCoverAsync` skip removal and the Id-cursor (D-01/D-02), the Steam external-games matcher (D-09 step 1), script 39, and all tests. One real defect is not covered by any decision: with IMG-02 working, the carousel will show every screenshot twice (see F1).

IGDB's external-games model was verified live with the app's credentials (read-only). Steam is `external_game_source = 1`, the id lives in `uid` (a **string**), the endpoint is `https://api.igdb.com/v4/external_games`, and the old `category` field is gone: `fields category` returns nothing and `where category = 1` returns `[]`. Steam AppId 400 resolves to IGDB game **71** "Portal", first release 2007-10-10, developer Valve. The currently stored 14546 is the 1986 Nexa/Activision text adventure. Importantly, the existing name search (`limit 3`) ranks Valve's Portal (id 71) 8th, and the existing `SearchGameAsync` already prefers an exact case-insensitive name match and falls back to the first hit. So D-09 steps 2 and 3 already exist in code. The only new behaviour is step 1 (SteamAppId lookup), and an exact-name rule alone would still pick 14546.

**Primary recommendation:** Land the WIP as two commits first (backend, then frontend), then implement in this order: IGDB `FindGameIdBySteamAppIdAsync` + `EnrichGameAsync` change, cover fix (D-01/D-02), Escape + `gameId` filter on `GameImagesController` (F1), script 39 (IGDB id 71, needs owner checkpoint), tests, live verification. Use a throwaway SQL database for the manager tests (the managers scan all users' rows, see Pitfall 1).

## Findings that change the plan (read first)

| # | Finding | Impact |
|---|---------|--------|
| F1 | `GameImagesController.Get` filters `Game.IGDBId == igdbId` across all of the user's active games and the detail page calls it with the game's `igdbId`. Once IMG-02 writes images for every sibling, the same-user sibling pair returns each screenshot twice. [VERIFIED: code read, GameImagesController.cs lines 23-31; game-detail.component.ts `loadScreenshots`] | Carousel shows duplicates and the counter reads "N / 2M". Success criterion 2 and UI-01 would be degraded. Fix: add optional `gameId` query param, frontend passes `game.id`. See Pattern 4. |
| F2 | Half-Life (107683) and Half-Life 2 (107687) **already have covers** in the dev DB (`coc2ea`, `co1nmw`), set at 2026-10-27 20:08. [VERIFIED: sqlcmd SELECT] | D-03's "one non-force run fills their covers" cannot be demonstrated on those rows. The one natural live row that exercises IMG-01 is **NieR:AutomataT, Games.Id 107889, IGDBId 391942, IGDBUpdatedAt 2026-06-11, CoverImageUrl NULL** (the only active row with NULL cover and a non-NULL `IGDBUpdatedAt`). Use it as the live proof; the xUnit test (T1) is the deterministic proof. |
| F3 | There is currently **no same-user pair sharing an IGDBId** (only Hades 113112 shared across users 2 and 17). [VERIFIED: sqlcmd] | D-17 needs a fixture. Natural one: the new matcher will map Dark Messiah SP (107697, Steam 2100) and MP (107698, Steam 2130) both to IGDB **2369**, creating a real same-user pair on the first non-force `GameSyncWorker` run. |
| F4 | `GameImageSyncWorker` Run-now ignores `force` (WorkerController passes no force to it); only `GameSyncWorker` takes it. [VERIFIED: WorkerController.cs] | "Non-force" is the only mode for the image worker; fine. |
| F5 | Portal's stored cover `coaanx` is actually the cover of "Portal Maze 2" (IGDB 193776), not of either Portal. [VERIFIED: sqlcmd + IGDB covers] | Script 39 clearing `CoverImageUrl` is required (as designed); do not skip it. Valve Portal's cover will be `coay61`. |
| F6 | 49 of 480 owner Steam rows (UserId 2) have an `IGDBId` that disagrees with IGDB's Steam external-games mapping (Portal is one). 22 Steam rows have no IGDBId; 16 of those resolve exactly. [VERIFIED: IGDB + sqlcmd scan] | Feeds D-07 (see Appendix A). The first non-force `GameSyncWorker` run after the matcher lands will assign IGDBIds to those 16 rows. Tell the owner before running it. |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| IGDB external-games lookup (Steam uid -> game id) | API external service (`IgdbService`) | - | Project rule: Services are only for external APIs. |
| Choosing between Steam match / exact name / first hit | API manager (`GameSyncManager`) | - | Logic belongs in Managers. |
| Cover and image persistence | Database via SPs (`sp_UpdateGameCover`, `sp_UpsertGameImage`) | API manager | Writes only through SPs (Dapper). |
| Reading screenshots for a game | API controller (`GameImagesController`, EF LINQ) | Database | Reads are EF LINQ. |
| Lightbox navigation, keys, preload | Browser / Client (`game-detail` component) | - | Pure view state. |
| Wrong-IGDBId data fix | Database (numbered script 39, applied manually) | - | Live-row correction, project convention. |

## Standard Stack

No new packages. Everything below is already in the repo. [VERIFIED: csproj / package.json read]

| Library | Version | Purpose |
|---------|---------|---------|
| .NET / ASP.NET Core | net8.0 target (SDK 10.0.401 installed, runtime 8.0.26 present) | API, managers |
| Dapper + Microsoft.Data.SqlClient | existing | SP calls |
| EF Core SqlServer 8 | existing | reads |
| xUnit 2.9.3, Microsoft.AspNetCore.Mvc.Testing 8.0.32 | existing in `GamePlanner.Tests` | manager tests (builds clean today) |
| Angular 22.2.3, TypeScript 6.0.3, zone.js 0.15 | existing | lightbox (note: CLAUDE.md still says Angular 13; the code is 22, `ChangeDetectionStrategy.Eager`, `standalone: false`) |
| @playwright/test 1.64.1 | existing in `e2e/` | lightbox e2e, runs on system Chrome (`channel: 'chrome'`, `ignoreHTTPSErrors: true`) |

**Package Legitimacy Audit:** Not applicable, this phase installs no external packages. `slopcheck`/`package-legitimacy` not run.

**Installation:** none.

## Code State: What the WIP already does vs. what is still needed

### GameImageManager.cs (backend WIP, +35/-24 lines, one hunk)
Already done and correct (D-04): `SyncScreenshotsAndArtworksAsync` builds `igdbToLocal = batch.GroupBy(g => g.IGDBId!.Value).ToDictionary(gr => gr.Key, gr => gr.Select(g => g.Id).ToList())`; `SaveImagesAsync` takes `Dictionary<long, List<int>>` and calls `sp_UpsertGameImage` once per local id. `sp_UpsertGameImage` is a MERGE on `(GameId, IgdbImageId)` and `UQ_GameImages_GameId_IgdbImageId` is per game, so two games can hold the same IGDB image id. [VERIFIED: Database/32]
Edge check: siblings that fall into different 50-row batches, or where one sibling was synced within the last 2 days, still work because each batch fetches by its own IGDB ids. No change needed.
Still needed (not in WIP): D-01 skip removal, D-02 cursor. Both are in `SyncCoversAsync` / `SaveCoverAsync`, untouched by the WIP.

### game-detail component (frontend WIP, html/ts/css)
Already done (D-11): `lightboxIndex`, `openLightbox(i)`, `nextImage`/`prevImage` with modulo wrap (guarded for length <= 1), `@HostListener('window:keydown')` for ArrowRight/ArrowLeft, counter `{{ lightboxIndex! + 1 }} / {{ screenshots.length }}`, `preloadNeighbors()` via `new Image()` for index +/- 1 using `lightboxUrl()`, 20 ms `imageFading` toggle with `opacity` 180 ms CSS transition, arrows and counter only rendered when `screenshots.length > 1`.
Still needed:
- **Escape (D-12):** add `else if (event.key === 'Escape') this.closeLightbox();` in `handleLightboxKeydown`.
- **F1 duplicates:** pass `game.id`, not just `igdbId`.
- Optional hardening, low risk: the counter is positioned `bottom: -2.25rem` outside `.lightbox-content` (max-height 90vh); on short viewports it can clip. Verify visually at a small viewport (e.g. 1280x600) in the e2e/screenshot pass. `preload` Image objects are not retained; browsers normally still finish and cache the request, and the e2e preload check (below) proves it in practice.
- Angular note: `*ngIf`/`*ngFor` still work in Angular 22 (the file already relies on them). `setTimeout` inside a zone.js app triggers change detection, so `imageFading` resets correctly with default (Eager) change detection. [VERIFIED: component declares `ChangeDetectionStrategy.Eager`, zone.js in package.json]

### Commit plan for the WIP
Two commits, staging exact paths only (the tree also holds untracked `.claude/commands`, `.claude/skills`, `.playwright-mcp`, `openspec/` which are not part of this phase): (1) `GamePlanner.API/Managers/GameImageManager.cs` as the IMG-02 change; (2) the three `game-detail.component.*` files as UI-01. Git warns about LF->CRLF on the frontend files; that is a warning, not a diff. Do not stash/checkout/reset.

## IGDB external-games: verified query shape (D-09)

Verified live against `https://api.igdb.com/v4` with the app's configured credentials via a read-only Node script that read `dotnet user-secrets` and never printed them. [VERIFIED: live IGDB API, 2026-10-27]

| Question | Answer |
|----------|--------|
| Endpoint | `POST https://api.igdb.com/v4/external_games` (same headers/body style as the other endpoints: `Authorization: Bearer`, `Client-ID`, `text/plain` body) |
| Steam source field | `external_game_source` (a reference to `external_game_sources`). **Steam = 1** (GOG = 5, Epic = 26, Microsoft = 11, Itch = 30, GiantBomb = 3, Twitch = 14, Youtube = 10). Read from `GET external_game_sources`. |
| Uid field | `uid`, a **string**. Query with `uid = "400"` (quoted). |
| `category` | Gone. `fields category` returns objects with no `category`; `where uid = "400" & category = 1` returns `[]`. The same `uid` also appears under other sources (`uid = "400"` returned 3 rows), so the source filter is mandatory. `games.category` was likewise replaced by `game_type` (`{id:0,type:"Main Game"}`). |
| Result shape | `{ "id": 15156, "game": 71, "uid": "400", "external_game_source": 1, "name": "Portal", "url": "https://store.steampowered.com/app/400", ... }`. `game` is the IGDB game id (long). |
| Batch form | `fields uid,game; where uid = ("400","220","70") & external_game_source = 1; limit 50;` returned 3 correct rows. A 50-uid batch with `limit 500` worked for all 480 owner Steam apps (10 requests). |
| Unknown uid | Returns `[]` (HTTP 200). |
| Cardinality | Across the 474 mapped owner apps there were zero uids mapping to more than one game. Still handle the multi case deterministically (take the first `game != null` ordered by `game`). |

The official docs site (api-docs.igdb.com) returned HTTP 403 to the fetch tool, so the doc text could not be quoted. The shape above is confirmed by behaviour, not by docs; tag it `[VERIFIED: live API]`, not `[CITED]`.

### Recommended `IgdbService` addition (follows existing conventions)
Add `private const string ExternalGamesUrl = "https://api.igdb.com/v4/external_games";`, a model `IgdbExternalGame { Id, Uid (string), Game (long?) }` with `[JsonPropertyName]` in `Domain/IgdbModels.cs`, and:

```csharp
// Source: live IGDB v4 verification 2026-10-27
public async Task<long?> FindGameIdBySteamAppIdAsync(int steamAppId, CancellationToken ct)
{
    var query = $"fields uid,game; where uid = \"{steamAppId}\" & external_game_source = 1 & game != null; limit 5;";
    var results = await PostQueryAsync<IgdbExternalGame>(ExternalGamesUrl, query, ct);
    return results.OrderBy(r => r.Game).Select(r => r.Game).FirstOrDefault();
}
```

Single (not batch) is the right shape: `EnrichGameAsync` handles one row at a time with a 250 ms delay, and `int` interpolation cannot inject. In `GameSyncManager.EnrichGameAsync`, change the no-IGDBId branch to: if `steamAppId.HasValue`, call `FindGameIdBySteamAppIdAsync`; if it returns an id, `result = await igdb.FetchEnrichmentByIdAsync(id, ct)`; if that is null or there was no mapping, fall back to the existing `SearchGameAsync(name)` (which already does exact-name-then-first-hit). Project `g.SteamAppId` in the batch `.Select(...)` (line ~49) and add the parameter to `EnrichGameAsync`. `game.IGDBId ??= result.Id` already persists the id. Extra request per unmatched row is within IGDB's ~4 req/s because of the existing 250 ms inter-row delay; no new delay needed.

D-09 steps 2/3: nothing new to write. `SearchGameAsync` already does `results.FirstOrDefault(exact, OrdinalIgnoreCase) ?? results.FirstOrDefault()` over `limit 3`. The planner should not rewrite it. Optional (discretion): raise `limit 3` to `limit 10` so an exact match ranked 4th-10th is not lost; this is a one-token change, low risk, but it is not required by any success criterion. For "Portal" it would not help (the 1986 game is an exact name match and comes first), which is exactly why step 1 matters.

## Portal: IGDB id with evidence (D-05)

**Valve's 2007 Portal = IGDB game id 71.** [VERIFIED: live IGDB API]

| Evidence | Value |
|----------|-------|
| `games` id 71 | name "Portal", `first_release_date` 1191974400 = **2007-10-10**, developer **Valve** (company 56) + Electronic Arts publisher, genres Platform/Puzzle/Adventure, themes Action/Science fiction/Comedy, First person, Single player, aggregated_rating 80, `game_type` Main Game, cover `coay61`, URL igdb.com/games/portal |
| Steam mapping | `external_games` uid "400", source 1 -> game 71 (also uid "52003" -> 71, a different Steam app) |
| Current wrong id | 14546 = "Portal", `first_release_date` 531187200 = **1986-11-01**, developer Nexa Corporation, publisher Activision, Commodore/Atari/DOS, Adventure |
| App search | `search "Portal"; limit 10` returns Portal Maze 2, a Brazilian title, **14546**, Portal Knights DLC, ..., Portal 64, then id 71 (8th). With `limit 3` (what `SearchGameAsync` uses) id 71 never appears. |
| Cross-check | `Games.Id 107696` "Portal 2" already carries IGDB 72, consistent with 71 for Portal. No other Games row uses 14546 or 71 today. |

The id is derived from the Steam mapping and the release-year/developer evidence; the owner confirms at the checkpoint (D-05). The script header should cite: Steam AppId 400 -> external_games -> game 71; `first_release_date` 2007-10-10; developer Valve; wrong id 14546 = 1986-11-01 Nexa/Activision; and that the app's `limit 3` search cannot surface id 71.

### Live pre-fix state of Games.Id 107693 (for the REVERSAL block)
Captured read-only 2026-10-27. **The executor must re-capture immediately before authoring (D-06) and compare.** [VERIFIED: sqlcmd]

| Column | Value |
|--------|-------|
| Id / UserId / Name | 107693 / 2 / Portal |
| SteamAppId / Source / IsActive | 400 / Steam / 1 |
| IGDBId | 14546 |
| FirstReleaseDate | 1986-11-01 00:00:00 |
| AggregatedRating / Count | 80.0 / 0 |
| CoverImageUrl | https://images.igdb.com/igdb/image/upload/t_cover_big/coaanx.jpg |
| IGDBUpdatedAt | 2026-08-28 02:26:32.000 |
| LastIGDBSyncAt | 2026-08-30 00:58:28.0118486 |
| LastImageSyncDate | 2026-10-27 20:08:52.467 |
| ModifiedAt / DeletedAt | NULL / NULL |
| GameGenres | GenreId 1 (Puzzle), 3 (Shooter), 7 (Platform) |
| GameThemes | ThemeId 1 |
| GamePlayerPerspectives | PerspectiveId 1 |
| GameModesRelation | GameModeId 3 |
| GameTags | TagId 15 ("Action"), **left alone** per D-06 |
| GameImages | 2 rows, both `screenshot` (ids 4464 `h1sxo1adkfjzwpksozfy` 320x256; 4467 `xx33ndsquzvhr2i0quao` 320x200), `SyncedAt` 2026-10-27 20:08:52 |

Differences from script 38's REVERSAL: Portal **has** GameImages rows (38's rows had none), so the REVERSAL should restore them (or state they are rebuilt by the image worker; restoring is safer and cheap) and must `DELETE` re-derived rows first, exactly as 38 does. Genre "Shooter" on the old row is another symptom of the wrong match. After the re-sync, GameTags still holds only "Action" (the new Portal's themes are Action/Sci-fi/Comedy but `GameSyncManager` only fills tags when the game has none); this is a consequence of the locked D-06 decision and should be noted in the script header so it is not rediscovered as a bug.

Script shape: copy `Database/38_FixHalfLifeIgdbId.sql` structure verbatim (`USE GamePlannerDB; GO`, `SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET XACT_ABORT ON; SET NOCOUNT ON;`, `BEGIN TRANSACTION; IF EXISTS (SELECT 1 FROM Games WHERE Id = 107693 AND UserId = 2 AND IGDBId = 14546) BEGIN ... END; COMMIT TRANSACTION; GO`). Add a post-condition check inside the transaction (e.g. `IF @@ROWCOUNT <> 1 THROW`) if desired; 38 does not, and "refuses to change anything if the row is not in the expected state" is already satisfied by the `IF EXISTS` guard (re-running is a no-op). Success criterion 3 asks for a "working rollback section": include the REVERSAL as commented SQL like 38, and have the executor dry-run it logically against the captured values; the owner applies scripts manually in SSMS.

## Cover loop design (D-01, D-02)

Current code (read): `SyncCoversAsync` does `while (!ct.IsCancellationRequested) { batch = db.Games.Where(IsActive && CoverImageUrl == null).OrderBy(Id).Take(50)...; ... if (batch.Count < BatchSize) break; }`. If 50+ rows stay uncovered, the same first 50 come back forever (verified by reading; today only 23 rows are NULL-cover, so it does not trigger yet).

Change:
- Add `int lastId = 0;` and `.Where(g => g.IsActive && g.CoverImageUrl == null && g.Id > lastId)`; after processing set `lastId = batch[^1].Id` (the query is `OrderBy(g => g.Id)`).
- Drop `IGDBUpdatedAt` from the `.Select(...)` and the `currentUpdatedAt` parameter from `SaveCoverAsync`, and remove the `igdbUpdatedAt <= currentUpdatedAt` block. Keep `DateTimeOffset.FromUnixTimeSeconds(result.UpdatedAt)` since `sp_UpdateGameCover` still takes `@IGDBUpdatedAt`.
- Keep `if (batch.Count < BatchSize) break;` and the `batch.Count == 0` break.
- Minor pre-existing log bug: `logger.LogInformation("covers {Processed}/{Total}", processed, batch.Count)` passes the batch size as Total. Fix opportunistically (pass the real total) or leave; not in a requirement.
- Success criterion 1's second clause (games with a cover still skip the refetch) is satisfied structurally: `CoverImageUrl == null` is the selector, so a row with a cover is never selected or requested. Assert it in test T1 by checking that no request for that row's IGDB id appears.
- `sp_UpdateGameCover` requires `IsActive = 1` and sets `LastImageSyncDate` as a side effect. [VERIFIED: Database/11]

## Test infrastructure (D-15, D-16)

### GamePlanner.Tests (xUnit)
- `ApiFactory` (WebApplicationFactory, env "Testing", test-only secrets, workers removed, every `IHttpClientFactory` client answered by `RecordingHandler`) is for HTTP-level tests. For manager tests do **not** use it. The precedent is `GameManagerAtomicityTests`: build `GamePlannerDbContext` + manager by hand against SQL Server, connection from env `ConnectionStrings__DefaultConnection` or default `Server=localhost;Database=GamePlannerDB;Trusted_Connection=True;TrustServerCertificate=True;`, probe rows prefixed `gsd-probe-`, cleanup in `finally`.
- For IGDB stubbing use the `IgdbServiceContractTests.Build()` pattern: `RecordingHandler` with a `Responder` (token host `id.twitch.tv` -> `{"access_token":"tok","expires_in":3600}`; `api.igdb.com` -> JSON by `r.RequestUri.AbsolutePath` + body), `StubHttpClientFactory(handler)`, in-memory `IConfiguration` with `IGDB:ClientId/ClientSecret`, `new IgdbService(factory, config, logger)`. Then `new GameImageManager(db, igdb, config, logger)` / `new GameSyncManager(db, igdb, logger)`. `RecordingHandler.Requests` exposes `Uri` and `Body` so tests can assert query text and request counts.
- `IgdbService.BatchSize` etc. read config; nothing else needed. Stub JSON examples: cover `[{"id":391942,"name":"x","cover":{"id":1,"url":"//images.igdb.com/igdb/image/upload/t_thumb/abc.jpg"},"updated_at":1700000000}]`; screenshots `[{"id":1,"image_id":"img1","url":"//images.igdb.com/igdb/image/upload/t_thumb/img1.jpg","width":1920,"height":1080,"game":<igdbId>}]`; external games `[{"id":1,"uid":"400","game":71,"external_game_source":1}]`.
- Baseline: `dotnet build GamePlanner.Tests` succeeds (0 warnings, 0 errors). `.planning/codebase/TESTING.md` is stale (says no tests exist); ignore it.

### Pitfall 1 (important): the managers scan every user's rows
`GameImageManager.RunAsync`, `SyncCoversAsync`, `SyncScreenshotsAndArtworksAsync` and `GameSyncManager.RunAsync` select across **all users**. Against the dev DB a test calling them also processes the owner's rows (23 NULL-cover rows, ~27 rows due for image sync, hundreds with missing metadata) and `sp_UpdateGameLastImageSync` stamps `LastImageSyncDate` on the owner's rows, and `GameSyncManager` waits 250 ms per row. That conflicts with "tests must not touch UserId 2 data".

Recommendation (prescriptive): point the three new test classes at a throwaway database `GamePlannerDB_Test` through `ConnectionStrings__DefaultConnection`, and make the test base class refuse to run unless the database name ends in `_Test`. Build it by running `Database/*.sql` in order with `GamePlannerDB` rewritten to `GamePlannerDB_Test` (34 of 39 scripts contain `USE GamePlannerDB;`; `01_CreateDatabase.sql` also names the DB in `CREATE DATABASE`; scripts 10, 11, 27, 28, 31 have no `USE` line and rely on the connection's database, so pass `-d GamePlannerDB_Test` to sqlcmd). **Not tried** (creating a DB is not read-only); Wave 0 must verify the scripts apply cleanly on an empty DB and note any that need seed data. If that proves too costly, the fallback is `InternalsVisibleTo("GamePlanner.Tests")` on the API assembly plus making `EnrichGameAsync`, `SaveCoverAsync`-level logic reachable, and accepting that the cover-loop test still scans live rows with the stub returning `[]` for non-probe ids (no cover writes, but ~5 s of 250 ms delays for the 22 no-IGDBId rows and no screenshot-phase call). Make this an explicit owner-visible trade-off in the plan.

Whatever DB is used: every test needs a `CancellationTokenSource(TimeSpan.FromSeconds(30))` passed as `ct`, so a regressed cursor fails instead of hanging.

### Tests to write
| ID | Covers | Arrange | Assert |
|----|--------|---------|--------|
| T1a | IMG-01 | Probe game: `IsActive=1`, `IGDBId=<fake>`, `IGDBUpdatedAt` set (e.g. 2026-06-11), `CoverImageUrl NULL`. Stub `/games` -> one game with `updated_at` older than the stored `IGDBUpdatedAt` and a cover. | Cover URL is `https:` + `t_cover_big`, `IGDBUpdatedAt` stamped. Pre-fix code would skip it. |
| T1b | IMG-01 | Second probe game with a cover already set. | No request body contains that game's IGDB id; its cover unchanged. |
| T1c | D-02 | >= 51 probe games with fake IGDB ids (51 forces a second iteration with `BatchSize` 50; use 120 to prove > 2), stub returns `[]`. | Run completes within the CTS; the number of `/games` cover requests equals ceil(N/50) for the probe set (each row requested once). Pre-fix code loops until cancelled. |
| T2 | IMG-02 | Two probe games, same `IGDBId`, same user (different `Id`), `LastImageSyncDate NULL`. Stub `/screenshots` and `/artworks` returning images with `game` = that id. | `GameImages` has the same `IgdbImageId` set for both `GameId`s for both types; both games got `LastImageSyncDate`. |
| T3a | IMG-03 | Probe game, `SteamAppId=400`, `IGDBId NULL`. Stub `/external_games` -> `game:71`; `/games` for `where id = 71` -> enrichment with release 1191974400. | Request body to `/external_games` contains `uid = "400"` and `external_game_source = 1`; row ends with `IGDBId = 71`; no `search "` request was made. |
| T3b | D-09 step 2 | Probe game, no SteamAppId, stub `search` returns [A, exact-name, C]. | Row gets the exact-name game's id. |
| T3c | D-09 step 3 | No SteamAppId, stub search returns [A, B, C] with no exact name. | Row gets A. |
| T3d | D-09 fallback | SteamAppId set but `/external_games` returns `[]`. | Falls back to name search. |
| T4 | F1 | `GameImagesController` (new `gameId` filter) with two same-user siblings. | Returns each screenshot once for a given `gameId`. Can be a controller-level test with `DbContext` + `ControllerContext` claims, no HTTP stack. |

### e2e (Playwright, `e2e/tests/uat.spec.ts`)
- Config: `testDir ./tests`, `workers: 1`, serial, `channel: 'chrome'`, `ignoreHTTPSErrors: true`, `storageState .auth/uat.json` created by `global-setup.ts` which logs in as `UAT_EMAIL` (hard-checked to equal `uat-test@gameplanner.local`) and refuses a token whose user id is 2. Env from `e2e/.env` (`UAT_EMAIL`, `UAT_PASSWORD`; the `.env.example` could not be read here, it is deny-listed). Needs the dev UI (4200) and API (7174) running.
- **The cert proxy workaround is not encoded in `e2e/`** (no `7175`, `rejectUnauthorized` or `route.fetch` anywhere). It is not needed there because the suite uses system Chrome with `ignoreHTTPSErrors`. The proxy is only for ad-hoc live verification through the Playwright MCP browser (D-17): node HTTP->HTTPS proxy on 127.0.0.1:7175 with `rejectUnauthorized:false`, plus `context.route('https://localhost:7174/**', route => route.fetch(...))`. That is a verification-session detail, not part of the committed suite.
- Existing helpers to reuse: `bearer()`, `api(request)`, `filterGames`, `worker(request,name)`; the existing "workflows" block deletes games named `uat-*` or `Celeste` before and after, so a lightbox fixture must not use those names.
- **Fixture (D-16):** the UAT user (id 17) currently owns `Hades` (Id 108205, IGDB 113112, 10 screenshots) and `Portal 2: In Motion` (Id 108206, IGDB 99969, 5 screenshots), plus a probe row. [VERIFIED: sqlcmd] They are not deleted by the suite. Make the test self-resolving and re-runnable: `GET /api/gameimages?type=screenshot` as the UAT user, group by `gameId`, pick a game with >= 3 screenshots (>= 2 is the stated minimum; 3 gives distinct prev and next). If none exists, create one with `POST /api/games/from-igdb` (`{ igdbId: 113112, name: 'Hades', estimatedMinutes: 60, genres: [], coverUrl: null }`), then `POST /api/worker/GameImageSyncWorker/run`, and poll `GET /api/gameimages` until >= 3 (the existing worker tests show the polling pattern). Do not hard-code game ids.
- Selectors available now: `.carousel-item` (click opens), `.lightbox-overlay`, `.lightbox-img`, `.lightbox-arrow-prev` / `.lightbox-arrow-next` (aria-labels "Previous screenshot" / "Next screenshot"), `.lightbox-counter` ("N / M"), `.lightbox-close` (aria-label "Close").
- Assertions: open at index 1 -> counter "1 / M"; ArrowRight and next button advance; ArrowLeft and prev go back; from "M / M" next shows "1 / M"; from "1 / M" prev shows "M / M"; Escape removes `.lightbox-overlay`; overlay-click also closes.
- "No visible load delay" (D-14): register `page.on('request')` before opening and collect URLs containing `t_1080p`. After opening the lightbox at index i, `expect.poll` until requests for the images at i+1 and i-1 (mod M) have been issued **before** any navigation click. After clicking next, assert `.lightbox-img` has `complete === true` and `naturalWidth > 0` immediately (`expect(locator).toHaveJSProperty('complete', true)`) and that no new `t_1080p` request was issued for the target image (it came from cache). Throttle is optional; the request-already-issued check is deterministic.
- Layout check: run once at a short viewport to confirm the counter is not clipped (see WIP note).

## Architecture Patterns

### Request/data flow for this phase
```
GameSyncWorker (non-force) -> GameSyncManager.RunAsync
   rows missing metadata or IGDBId (all users)
      -> EnrichGameAsync(id, igdbId?, name, steamAppId?, ...)
           igdbId set        -> IgdbService.FetchEnrichmentByIdAsync
           no igdbId + steam -> IgdbService.FindGameIdBySteamAppIdAsync (external_games, source 1)
                                  hit -> FetchEnrichmentByIdAsync(id)   miss/empty -> SearchGameAsync(name) (exact, else first)
           -> EF update genres/themes/... + game.IGDBId ??= result.Id

GameImageSyncWorker -> GameImageManager.RunAsync
   covers: NULL-cover rows, Id cursor, 50 per batch
      withId   -> FetchCoversByIdsAsync -> SaveCoverAsync (no freshness skip) -> sp_UpdateGameCover
      withoutId-> SearchCoverByNameAsync -> SaveCoverAsync
   images: rows with IGDBId due (2-day cutoff), 50 per batch
      FetchScreenshots/ArtworksByGameIdsAsync -> igdbToLocal(igdbId -> [localIds]) -> sp_UpsertGameImage per local id -> sp_UpdateGameLastImageSync

Browser: game-detail -> GET /api/games/{id}/detail -> GET /api/gameimages?type=screenshot&gameId={id}
         carousel -> lightbox (index, wrap, preload +/-1, keys, Escape)
```

### Pattern 1: Id cursor paging for a draining query
Use `Id > lastId` with `OrderBy(Id)` when rows may not leave the filter after processing (IGDB has no cover). [VERIFIED: matches D-02]

### Pattern 2: Service returns data, manager decides
`IgdbService.FindGameIdBySteamAppIdAsync` returns `long?`; the selection order lives in `EnrichGameAsync`.

### Pattern 3: Guarded one-off data fix
`IF EXISTS (pre-fix tuple)` -> delete derived rows -> update -> commit; REVERSAL in comments. Convention-exemption note carried over from 38.

### Pattern 4: Per-game image read (F1)
```csharp
// GameImagesController.Get: add [FromQuery] int? gameId = null
if (gameId.HasValue)
    query = query.Where(i => i.GameId == gameId.Value);
else if (igdbId.HasValue)
    query = query.Where(i => i.Game.IGDBId == igdbId.Value);
```
Still user-scoped by `i.Game.UserId == userId`, so no cross-user exposure. Frontend: `getGameImages('screenshot', game.igdbId, game.id)` sending `gameId`. Keep `igdbId` param working for compatibility; nothing else in the UI calls it.

### Anti-Patterns to Avoid
- **Rewriting the WIP.** Review, commit, extend only.
- **Re-matching existing rows** (D-10) or touching `GameEnrichmentManager` / `SearchCoverByNameAsync` (D-08).
- **Using the `category` field** on `external_games`; it no longer exists.
- **Unquoted or unfiltered uid queries**; `uid` collides across sources.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Steam -> IGDB mapping | Fuzzy title/year matching | IGDB `external_games` (source 1, uid) | Exact, and 49 existing rows prove name matching goes wrong |
| Image upsert | Custom insert/exists logic | `sp_UpsertGameImage` (MERGE) | Already idempotent per `(GameId, IgdbImageId)` |
| Carousel paging/wrap | New index structure | The WIP modulo helpers | Already correct |
| Stub HTTP | A new fake server | `RecordingHandler` + `StubHttpClientFactory` | Already used by the contract tests |

## Common Pitfalls

### Pitfall 1: Tests mutate the owner's data
See Test infrastructure. Managers are global across users; use a `_Test` database or accept the documented fallback. A `finally` cleanup is mandatory either way.

### Pitfall 2: Duplicate screenshots in the carousel (F1)
Appears only after IMG-02 plus a same-user shared IGDBId. Verify live on Dark Messiah 107697/107698 after the matcher run, or in T4.

### Pitfall 3: `GameSyncManager` paging with `Skip(offset)` over a shrinking set
The batch query filters rows that need enrichment and uses `.Skip(offset)`; rows leave the filter once enriched, so later batches skip unprocessed rows. Pre-existing and not in a requirement, but it means a single sync run may not reach every row on large sets, and the matcher's 16 live rows might need a second run. The owner's DB currently has few rows needing sync, so it is a risk, not a blocker. Do not fix here; mention it in the summary if observed.

### Pitfall 4: Screenshot/artwork `limit 500` per batch
`FetchScreenshotsByGameIdsAsync` requests up to 50 games with `limit 500` and no offset. The owner's library averages 7.6 screenshots/game (max 22); the first 50 games measured 310 screenshots / 125 artworks (under the cap), but an image-rich batch could exceed 500 and silently drop images, which would contradict "full screenshot set". Not required by the decisions; flag it. Cheap fix if the owner wants it: loop with `offset` until fewer than 500 rows return.

### Pitfall 5: Script 39 applied twice / on a different state
The `IF EXISTS` on `(Id=107693, UserId=2, IGDBId=14546)` makes re-runs and wrong-state runs a no-op; that is the intended "refuses to change anything" behaviour. State it in the header.

### Pitfall 6: Old covers and `LastImageSyncDate`
After script 39 the row has `CoverImageUrl NULL` and `LastImageSyncDate NULL`, so it is picked by both the cover and image phases. Order: `GameSyncWorker` first (sets IGDBUpdatedAt, scalars), then `GameImageSyncWorker`.

### Pitfall 7: The matcher changes the owner's data on its first run
The non-force `GameSyncWorker` after the matcher lands will assign IGDBIds to the 22 NULL-IGDBId Steam rows that resolve (16 do; Appendix B) and a same-user pair appears (Dark Messiah). That is expected product behaviour and the intended live proof for IMG-02, but it is a visible data change; warn the owner.

## Code Examples

### Escape in the existing handler (D-12)
```typescript
@HostListener('window:keydown', ['$event'])
handleLightboxKeydown(event: KeyboardEvent) {
  if (this.lightboxIndex === null) return;
  if (event.key === 'ArrowRight') this.nextImage();
  else if (event.key === 'ArrowLeft') this.prevImage();
  else if (event.key === 'Escape') this.closeLightbox();
}
```

### SaveCoverAsync after D-01
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

## State of the Art

| Old | Current | Impact |
|-----|---------|--------|
| `external_games.category` enum | `external_game_source` reference (Steam = 1) | The query must use `external_game_source` |
| `games.category` | `games.game_type` object | Not used by this phase's queries; do not add `category` anywhere |

## Runtime State Inventory

This phase is a data correction (Portal) rather than a rename, but the same question applies: what still holds the old IGDBId after the script?

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | `Games` 107693 (IGDBId, scalars, cover), `GameGenres` (1,3,7), `GameThemes` (1), `GamePlayerPerspectives` (1), `GameModesRelation` (3), `GameImages` (2 screenshots), `GameTags` (15). No other Games row has IGDB 14546 | Script 39 data migration (live row); GameTags intentionally kept |
| Live service config | None. IGDB is read-only for this app | None |
| OS-registered state | None. Workers are in-process `BackgroundService` | None |
| Secrets/env vars | IGDB credentials unchanged; none reference the id | None |
| Build artifacts | None | None |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The numbered scripts apply cleanly to an empty `GamePlannerDB_Test` after a name rewrite (scripts 10/11/27/28/31 have no `USE`) | Test infrastructure | Wave 0 cost grows; fallback is the `InternalsVisibleTo` route with documented side effects |
| A2 | Official IGDB docs describe `external_game_source`/`uid` as above (docs returned 403, shape confirmed by live behaviour only) | IGDB external-games | Low: behaviour was observed on the live API the app uses |
| A3 | Owner accepts that the first matcher-enabled non-force `GameSyncWorker` run assigns IGDBIds to ~16 rows | Pitfall 7 | Surprise data change; mitigated by telling the owner at the Portal checkpoint |
| A4 | The counter clipping at short viewports is a cosmetic risk only | Code State | Cosmetic |

## Open Questions (RESOLVED)

All four were answered by the owner at plan time (2026-10-27) and recorded as locked decisions in `06-CONTEXT.md` ("Added at plan time").

1. **Test database vs. live DB for manager tests** - **RESOLVED by D-18:** manager tests run against a throwaway `GamePlannerDB_Test` built from the numbered scripts, chosen through `ConnectionStrings__DefaultConnection`, with a guard that refuses any database name without the `_Test` suffix. The `InternalsVisibleTo` fallback is not used.
   - Known: managers scan all users; existing tests hit the live dev DB for probe rows only.
   - Unclear (at research time): owner appetite for a `GamePlannerDB_Test` database.
   - Recommendation (adopted): create it, guard by name suffix.
2. **F1 fix location** - **RESOLVED by D-19:** server-side `gameId` filter on `GET /api/gameimages`, kept user-scoped; the game-detail page passes `game.id`. Client-side de-duplication is not used.
3. **Should `SearchGameAsync` use `limit 10` instead of 3?** - **RESOLVED by D-20:** yes, raise `limit 3` to `limit 10`.
4. **Screenshot batch `limit 500` truncation (Pitfall 4)** - **RESOLVED by D-21:** page the screenshot/artwork fetch (offset paging until a short page) instead of a single `limit 500` per 50-game batch.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build/test | yes | 10.0.401 (runtime 8.0.26 present) | - |
| Node.js | e2e, probes | yes | v24.19.0 | - |
| SQL Server (localhost, Windows auth) + `sqlcmd` | tests, live checks | yes | `sqlcmd` at `C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn` | - |
| IGDB credentials (`dotnet user-secrets`) | live probes, dev API | yes (keys present; values not printed) | - | - |
| Playwright 1.64.1 + system Chrome | e2e | installed in `e2e/node_modules`; Chrome not checked | - | MCP browser + 7175 proxy workaround |
| Dev API (7174) / UI (4200) running | e2e, D-17 | not checked (not started by research) | - | start per CLAUDE.md |

## Security Domain

`security_enforcement` is enabled (ASVS level 1, block on high).

| ASVS Category | Applies | Control |
|---------------|---------|---------|
| V4 Access Control | yes | `GameImagesController` stays `[Authorize]` and keeps `i.Game.UserId == userId`; the new `gameId` filter is additive to that |
| V5 Input Validation | yes | Steam uid is built from an `int` (no injection); the existing name queries escape only `"` and not `\`; do not copy that pattern for new string inputs. `gameId` is an `int?` model-bound |
| V6 Cryptography / V2 / V3 | no | untouched |
| V14 Config / secrets | yes | Research and tests must not log or commit IGDB secrets; the test config uses `ApiFactory.TestIgdbSecret` style placeholders |

| Threat | STRIDE | Mitigation |
|--------|--------|------------|
| Cross-user image read via `gameId` | Information disclosure | Keep the `UserId` predicate; add a test that another user's `gameId` returns `[]` |
| Script 39 modifies the wrong row | Tampering | Guard on `(Id, UserId, IGDBId)`, transaction, owner confirmation checkpoint, REVERSAL block |
| Secret leakage in logs/tests | Information disclosure | Stubbed IGDB in tests; `IgdbService` already logs only status/body, covered by `IgdbServiceContractTests` |

## Appendix A: D-07 suspect scan (read-only, UserId 2, Steam rows, 2026-10-27)

Method: for every active UserId 2 row with a `SteamAppId` (480), query IGDB `external_games` (Steam, uid) in batches of 50 and compare with the stored `IGDBId`. Result: 474 mapped, 6 unmapped, 0 multi-mapped, **409 agree, 49 disagree**. A disagreement is a suspect, not proof of error (some stored ids may be intentional remaster/edition entries, e.g. Disco Elysium, Resident Evil 2/4, Persona 4 Golden, Metro Exodus Enhanced). Format: SteamAppId | name | stored IGDBId -> IGDB Steam mapping.

```
10 Counter-Strike 266357->241 | 60 Ricochet 368008->7589 | 130 Half-Life: Blue Shift 285235->2549
400 Portal 14546->71 (fixed by 39) | 500 Left 4 Dead 60984->122 | 7670 BioShock 152660->20
12210 GTA IV Complete 27912->731 | 13500 Prince of Persia: Warrior Within 213052->837
13560 Splinter Cell 134350->910 | 13570 Splinter Cell: Chaos Theory 243021->911
34870 Sniper Ghost Warrior 2 138980->1368 | 55150 Space Marine Anniversary 186034->578
205950 Jet Set Radio 21004->1569 | 208650 Batman: Arkham Knight 26041->5503 | 228300 Remember Me 372230->1827
235210 Strider 6969->5333 (stored release 1989-03-01, 1989 arcade game) | 242760 The Forest 99425->7504
268500 XCOM 2 86268->10919 | 272060 Serena 325536->14528 | 294860 Valkyria Chronicles 75848->18967
356190 Shadow of War 52198->27421 | 435150 Divinity: Original Sin 2 154209->11800 | 448280 Job Simulator 120688->12596
460790 Bayonetta 279337->2136 | 460810 Vanquish 220716->3218 | 524220 NieR:Automata 391942->11208
571740 Golf It! 64369->30099 | 632470 Disco Elysium 335434->26472 | 678950 DRAGON BALL FighterZ 144760->36911
678960 CODE VEIN 347636->28168 | 812140 AC Odyssey 164698->103054 | 858260 Until You Fall 99964->115478
883710 Resident Evil 2 347128->19686 | 911400 AC III Remastered 109532->237701 | 955050 Bright Memory 134072->113739
997070 Marvel's Avengers 259466->26950 | 1097350 Weird West 250504->127350 | 1113000 Persona 4 Golden 2985->234702
1184370 Pathfinder: WotR EE 226521->127254 | 1332010 Stray 222370->110248 | 1384160 GUILTY GEAR -STRIVE- 264825->125764
1422450 Deadlock 31699->301298 | 1449560 Metro Exodus EE 143292->37016 | 2012320 Parallel Experiment 285166->198114
2050650 Resident Evil 4 145191->132181 | 2069040 Unboxing the Cryptic Killer 247757->192087
2124490 SILENT HILL 2 222485->222341 | 2592160 Dispatch 376768->325609 | 3837340 FINAL FANTASY VII 207028->207026
```
Second heuristic (SQL only): rows with a Steam app and `FirstReleaseDate < 1995-01-01`: Portal (1986) and **Strider** (Id 107764, 1989-03-01). Strider is the other implausible-for-Steam date. Only Portal is fixed in this phase (D-07/D-10); the rest go in the phase summary as a future todo.

## Appendix B: Rows the matcher would resolve (Steam rows with NULL IGDBId)

16 of 22 resolve exactly: Dark Messiah SP/MP (2100, 2130) -> 2369 (a same-user shared pair), Mass Effect (17460) -> 73, Oblivion GOTY (22330) -> 59, FINAL FANTASY VII 2013 (39140) -> 392808, Darkest Dungeon (262060) -> 8965, Serious Sam Fusion (564310) -> 91191, Batman: The Enemy Within (675260) -> 51525, We Were Here Too (677160) -> 54486, NINJA GAIDEN 3 (1369760) -> 5317, Until Then (1574820) -> 134165, NINJA GAIDEN S (1580780) -> 143619, NINJA GAIDEN S2 (1580790) -> 7385, AGAINST (1584840) -> 152211, Servant of the Lake (3636770) -> 350179, The Blood of Dawnwalker (3751260) -> 282831. Unmapped (name search fallback): Wallpaper Engine, The Division PTS, Hunt Showdown 1896, Lossless Scaling, AGAINST Beatmap Editor, HITMAN WOA. (Count of resolved above is 16 including both Dark Messiah rows.)

## Sources

### Primary (HIGH confidence)
- Live IGDB v4 API (`external_game_sources`, `external_games`, `games`, `screenshots`), queried read-only with the app's configured credentials on 2026-10-27. Scripts kept in the session scratchpad; no secrets printed.
- Live dev DB via `sqlcmd` SELECTs only (Games, GameImages, junction tables, indexes).
- Repository files read: `GameImageManager.cs`, `GameSyncManager.cs`, `IgdbService.cs`, `IgdbModels.cs`, `GameImagesController.cs`, `WorkerController.cs`, `GameImageSyncWorker.cs`, `Database/11`, `32`, `38`, `game-detail.component.*`, `game.service.ts`, `GamePlanner.Tests/*`, `e2e/*`, `.planning/todos/pending/2026-10-24-*.md`.

### Secondary / not reachable
- api-docs.igdb.com returned HTTP 403 to the fetch tool; a web search found only third-party wrappers and nothing authoritative about `external_game_source`. Behaviour was verified directly instead.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, nothing new.
- IGDB external-games shape and Portal id: HIGH, verified live.
- Code state / WIP review: HIGH, read directly.
- Test isolation recommendation: MEDIUM, scratch-DB build not attempted.
- e2e fixture strategy: HIGH for the data (verified), MEDIUM for exact selectors until the first run.

**Research date:** 2026-10-27
**Valid until:** 2026-11-26 for IGDB shape (live API, stable but IGDB has migrated this model recently); the DB facts (F2, F3, Appendix) are a snapshot and will drift with every worker run, so re-check them at execution time.
