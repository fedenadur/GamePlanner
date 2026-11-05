---
phase: 06-game-images-metadata
reviewed: 2026-11-05T00:00:00Z
depth: standard
files_reviewed: 22
files_reviewed_list:
  - Database/39_FixPortalIgdbId.sql
  - GamePlanner.API/Controllers/GameImagesController.cs
  - GamePlanner.API/Domain/IgdbModels.cs
  - GamePlanner.API/Managers/GameImageManager.cs
  - GamePlanner.API/Managers/GameSyncManager.cs
  - GamePlanner.API/Services/IgdbService.cs
  - GamePlanner.Tests/GameImageManagerTests.cs
  - GamePlanner.Tests/GameImagesControllerTests.cs
  - GamePlanner.Tests/GameManagerAtomicityTests.cs
  - GamePlanner.Tests/GameSyncManagerMatcherTests.cs
  - GamePlanner.Tests/IgdbServiceContractTests.cs
  - GamePlanner.Tests/SteamAuthServiceTests.cs
  - GamePlanner.Tests/Support/ApiFactory.cs
  - GamePlanner.Tests/Support/IgdbStub.cs
  - GamePlanner.Tests/Support/TestDatabase.cs
  - scripts/build-test-db.ps1
  - e2e/tests/uat.spec.ts
  - game-planner-ui/src/app/components/game-detail/game-detail.component.css
  - game-planner-ui/src/app/components/game-detail/game-detail.component.html
  - game-planner-ui/src/app/components/game-detail/game-detail.component.ts
  - game-planner-ui/src/app/components/game-detail/game-detail.component.spec.ts
  - game-planner-ui/src/app/services/game.service.ts
findings:
  critical: 0
  warning: 5
  info: 4
  total: 9
status: issues_found
---

# Phase 6: Code Review Report

**Reviewed:** 2026-11-05
**Depth:** standard
**Files Reviewed:** 22
**Status:** issues_found

## Summary

Phase 06 delivers: a per-game screenshot filter (`gameId`) with a lightbox carousel, an Id-cursor fix for the cover and enrichment loops (they previously re-selected rows that never gained data), IGDB image pagination, Steam-mapping-first matching in `GameSyncManager`, a one-off Portal IGDBId correction, and a guarded `_Test` database for the test suite. The code is generally sound. The cursor and pagination changes are correct and well covered. The test-DB guard (`TestDatabase.ConnectionString` / `ApiFactory` pinning) is robust. SQL parameters are all bound, and there are no injection or secret issues in the changed code. No Critical issues were found.

The warnings are data-integrity and robustness gaps. A failed image fetch still stamps the batch as synced. The cover worker stamps `IGDBUpdatedAt`, which can make enrichment skip rows that still lack metadata. The match fallback chain can silently reintroduce the "wrong first hit" defect Portal had. The e2e suite leaves a persistent fixture game. The test-DB build script's name guard is weaker than its purpose requires.

## Warnings

### WR-01: A failed screenshot/artwork fetch still stamps `LastImageSyncDate`, so the batch is skipped for two days

**File:** `GamePlanner.API/Managers/GameImageManager.cs:225-275`
**Issue:** When either `FetchAllImagePagesAsync` call throws, `batchFailed` is set and the failure is counted. The code then goes on to call `sp_UpdateGameLastImageSync` for every game in the batch. The in-code comment admits "up to BatchSize games silently go two days without images", and the change only raised the log level to Error. A transient IGDB failure (429/5xx) therefore costs up to `BatchSize` games their images for the 2-day `cutoff` window. Also, `FetchAllImagePagesAsync` discards pages already fetched when a later page fails, so one failed page loses the whole result for that batch.
**Fix:** Do not stamp games from a failed batch, and keep pages already fetched:
```csharp
if (!batchFailed)
{
    foreach (var game in batch)
        await conn.ExecuteAsync("sp_UpdateGameLastImageSync", new { GameId = game.Id },
            commandType: System.Data.CommandType.StoredProcedure);
}
```
Because the image loop has no Id cursor (it relies on the stamp to drop rows from the filter), a failed batch must also advance an Id cursor, as `SyncCoversAsync` does. Otherwise the same batch is re-selected forever, so add `g.Id > lastId` to the query and set `lastId = batch[^1].Id`.

### WR-02: Cover sync stamps `IGDBUpdatedAt`, which can make `GameSyncManager` skip rows that still lack metadata

**File:** `GamePlanner.API/Managers/GameImageManager.cs:171-176`, `GamePlanner.API/Managers/GameSyncManager.cs:114-119`, `Database/11_AlterGames_AddImageColumns.sql` (`sp_UpdateGameCover`)
**Issue:** `sp_UpdateGameCover` writes `IGDBUpdatedAt = result.UpdatedAt` from the cover fetch. `GameSyncManager.EnrichGameAsync` treats `IGDBUpdatedAt` as the "metadata already synced" stamp: it skips when `igdbId.HasValue && storedUpdatedAt.HasValue && igdbUpdatedAt <= storedUpdatedAt`. The enrichment query selects rows because metadata is missing (no genres, no rating, and so on). If the cover worker reaches a row first, its stamp equals IGDB's `updated_at`, and the enrichment pass then logs "up-to-date" and returns `true` without filling anything. A non-force run therefore never repairs it. Phase 06 narrowed this skip (null-IGDBId rows no longer use the stamp) but left the shared stamp in place. The Portal repair depends on the sync worker running before the cover worker.
**Fix:** Make the cover path stop writing the enrichment stamp. Either pass the row's existing `IGDBUpdatedAt` through to `sp_UpdateGameCover`, or add a new migration so the SP leaves `IGDBUpdatedAt` alone. Alternatively, skip enrichment only when no metadata is missing, instead of trusting the stamp alone.

### WR-03: The Steam-then-name matcher can fall through to a wrong "first hit" and persist it permanently

**File:** `GamePlanner.API/Managers/GameSyncManager.cs:97-110`, `GamePlanner.API/Services/IgdbService.cs:68-76`
**Issue:** Two gaps in the match chain:
1. If the Steam mapping resolves to an IGDB id but `FetchEnrichmentByIdAsync(mapped)` returns null, `result` stays null and `result ??= SearchGameAsync(name)` runs. That search takes the first hit when there is no exact name match, which is the defect that stored the 1986 "Portal" originally.
2. A Steam-mapping HTTP failure throws and aborts the whole enrichment for that row, instead of degrading to the name path.

`game.IGDBId ??= result.Id` then persists whatever came back. Raising the search limit from 3 to 10 does not change the first-hit fallback. A stored wrong id is only fixed by manual SQL, as with 38/39.
**Fix:** When `steamAppId` is set and a mapping was found, do not fall back to name search; treat a null enrichment result as a retriable failure. For rows with a `steamAppId`, require an exact name match in `SearchGameAsync` rather than `FirstOrDefault()`. Catch mapping-lookup exceptions explicitly and decide deliberately between fail and degrade.

### WR-04: The e2e lightbox fixture creates a persistent game and never removes it

**File:** `e2e/tests/uat.spec.ts` (`lightboxFixture`, around the `POST /api/games/from-igdb` call)
**Issue:** When the UAT account has no game with two or more screenshots, the test creates `uat-lightbox-fixture` (igdbId 113112) through the live API and triggers the real `GameImageSyncWorker` (polling up to 15 minutes). The game is never deleted, so it persists in the owner's real database. A second run takes the `best` branch and silently depends on that leftover. Fixture selection also picks whatever game has the most screenshots, so the result depends on live data.
**Fix:** Delete the fixture in an `afterAll` via the soft-delete endpoint (`DELETE /api/games/{id}`) when this test created it. Track a `createdFixtureId` and clean up only that id.

### WR-05: The test-DB build script's `_Test` guard is weaker than its purpose and builds dynamic SQL from the name

**File:** `scripts/build-test-db.ps1:9-11, 25-26, 31-35`
**Issue:** The only safeguard against dropping the wrong database is `-notmatch '_Test$'`. That accepts any string ending in `_Test`, including one with `]`, `;` or spaces. `$` also matches before a trailing newline. The name is then interpolated unescaped into the `ALTER DATABASE ... DROP DATABASE` statement. The `\bGamePlannerDB\b` rewrite is also case-sensitive: any numbered script that spells the name differently (for example `gameplannerdb`) would run against the live database, because `sqlcmd` runs with `-E` (integrated auth). A grep of `Database/*.sql` today shows no such spelling, so this is latent, but the script is the project's safety boundary.
**Fix:**
```powershell
if ($Database -cnotmatch '^[A-Za-z0-9]+(_[A-Za-z0-9]+)*_Test\z') { throw 'Refusing to build: ...' }
$raw = ...; $sql = [regex]::Replace($raw, '\bGamePlannerDB\b', $Database, 'IgnoreCase')
```
Add a final post-check that no script text still references the live name, and refuse to run if `$Database -ieq 'GamePlannerDB'`.

## Info

### IN-01: Stale-response race and un-reset lightbox state in `GameDetailComponent`

**File:** `game-planner-ui/src/app/components/game-detail/game-detail.component.ts:50, 61-67`
**Issue:** `getGameDetail(...).subscribe` and `getGameImages(...).subscribe` are nested subscribes inside the `paramMap` subscription, with no `switchMap` or unsubscribe. Navigating between game details quickly lets an earlier game's screenshot response overwrite `screenshots` after the page has moved on. This partly undermines the new `gameId` filter. `lightboxIndex` is also not reset on a route change, and a reload that shrinks `screenshots` leaves a stale index (the getter then returns `undefined`, so the lightbox just disappears). This is not strictly new, but the new index-based lightbox relies on it.
**Fix:** Use `paramMap.pipe(switchMap(...))`, and reset `lightboxIndex = null` where the per-game state is cleared.

### IN-02: Lightbox keyboard and timer details

**File:** `game-planner-ui/src/app/components/game-detail/game-detail.component.ts:93-117`
**Issue:** The `window:keydown` handler does not `preventDefault()` for the arrow keys, so the page behind the overlay can scroll while the user navigates. The `setTimeout` in `triggerFade` is never cleared on destroy. The overlay has no `role="dialog"`/focus trap.
**Fix:** Call `event.preventDefault()` for handled keys. Keep the timer id and clear it in `ngOnDestroy`. Add `role="dialog" aria-modal="true"` to the overlay.

### IN-03: The e2e navigation-budget retry makes the 250 ms gate weak

**File:** `e2e/tests/uat.spec.ts` (`gatedNav`)
**Issue:** After a first miss, the test navigates back and retries. The image is then already in the browser cache, so the second attempt can pass trivially even if the first miss was a genuine preload regression. A hard 250 ms wall-clock assertion is also machine-noise sensitive.
**Fix:** Keep the first measurement as the pass/fail signal and make the retry reload via a cache-busting query, or relax the gate to "preloaded request finished before navigation" (already asserted via `requestfinished`), and treat timing as a logged metric.

### IN-04: Magic numbers, and image rows are only ever upserted

**File:** `GamePlanner.API/Managers/GameImageManager.cs:119, 126, 131` and `:300-335`
**Issue:** `Task.Delay(250, ct)` is repeated as a magic number. `SaveImagesAsync` only upserts, so images that IGDB removes, or that belong to a previous IGDB id after a correction, stay in `GameImages` until deleted by hand (the 39 script does this manually for Portal).
**Fix:** Add a named constant for the delay. Consider a follow-up SP that removes a game's rows whose `IgdbImageId` is not in the latest fetch.

---

_Reviewed: 2026-11-05_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
