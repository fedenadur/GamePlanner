# Phase 6: Game Images & Metadata - Context

**Gathered:** 2026-10-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Land the uncommitted image/lightbox WIP and make every local game show its correct cover and full screenshot set:

- **IMG-01:** a game with `CoverImageUrl` NULL gets a cover even when `IGDBUpdatedAt` is set
- **IMG-02:** screenshots and artworks are saved for every local game that shares an IGDBId (WIP in `GameImageManager.cs`)
- **IMG-03:** Portal (`Games.Id 107693`) gets Valve's 2007 IGDBId through a guarded, reversible script 39. The Steam-import matcher is also improved so new imports stop picking wrong matches.
- **UI-01:** screenshot navigation in the game-detail lightbox (WIP in the `game-detail` component)

The phase starts by reviewing and committing the WIP. It finishes and verifies that work and does not rewrite it.

</domain>

<decisions>
## Implementation Decisions

### Cover fetch (IMG-01)
- **D-01:** Remove the `IGDBUpdatedAt` freshness skip from `GameImageManager.SaveCoverAsync` entirely. `SyncCoversAsync` only selects rows with `CoverImageUrl IS NULL`, so the skip can never apply correctly there. `sp_UpdateGameCover` still stamps `IGDBUpdatedAt`. The same change applies to the `withoutId` (name-search) path, which calls the same method.
- **D-02:** Fix the cover loop's re-fetch bug in the same change. Today it re-queries "first 50 rows with NULL cover" on every iteration, so 50 or more rows that IGDB has no cover for are fetched again and again until cancellation. Page with an Id cursor (`Id > lastId`) so each row is tried once per run.
- **D-03:** No data script for Half-Life (107683) and Half-Life 2 (107687). After the code fix, one non-force `GameImageSyncWorker` run must fill their covers, and that run is the proof for success criterion 1.

### Shared IGDBId images (IMG-02)
- **D-04:** Keep the WIP as written: `igdbToLocal` becomes `Dictionary<long, List<int>>` and `sp_UpsertGameImage` runs once per local game. Review it, commit it, and verify it with a test (D-15) and a live check (D-17).

### Portal fix (IMG-03)
- **D-05:** Claude looks up Valve's Portal on IGDB (exact title "Portal", release year 2007) through the app's search and/or a direct IGDB id query, and writes the evidence into the script header the way script 38 does. The owner confirms the ID at a checkpoint before applying the script. — **Reversibility:** one-way (practically) — it writes a live row in the owner's DB. The script's REVERSAL block makes it recoverable.
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

### IGDB matcher for new matches (IMG-03 prevention)
- **D-08:** The matcher runs only in `GameSyncManager.EnrichGameAsync`, on the branch where the row has no IGDBId and the code calls `igdb.SearchGameAsync(name)`.
  - The legacy `GameEnrichmentManager` stays as it is.
  - `GameImageManager`'s `SearchCoverByNameAsync` stays as it is (it stores no IGDBId).
  - The Add-game IGDB search UI stays as it is.
- **D-09:** Order:
  1. If the row has a `SteamAppId`, resolve it through IGDB's external-games mapping (Steam uid → IGDB game). This is exact.
  2. Otherwise, or when there is no mapping, use the name search and prefer an exact case-insensitive name match among the results.
  3. If no name matches exactly, fall back to the first hit, as today.

  The new IGDB query lives in `IgdbService` (Services are for external APIs). The selection logic lives in the manager. The researcher must confirm the current IGDB external-games query shape (endpoint, Steam source/category field and uid field), since IGDB has changed that model.
- **D-10:** Only new matches use the new matcher. Already-matched rows are not re-matched. Suspects from D-07 are reported, not changed.

### Lightbox (UI-01)
- **D-11:** Keep the WIP: prev/next buttons, Left/Right keys, wrap at both ends, an "N / M" counter, neighbor preload and the 180ms fade.
- **D-12:** Add Escape to close in the existing `handleLightboxKeydown` handler.
- **D-13:** Only screenshots can be browsed. Artworks keep syncing but are not added to the carousel or lightbox.
- **D-14:** Keep the 20ms class-toggle fade. Neighbor preload covers "no visible load delay". Verification confirms that.

### Verification
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

### Folded Todos
- **2026-10-24-cover-skipped-when-igdbupdatedat-set** → IMG-01 (D-01..D-03). Its data workaround (the `UPDATE ... IGDBUpdatedAt = NULL`) is not used, per D-03.
- **2026-10-24-portal-igdbid-wrong-match** → IMG-03 (D-05..D-10), including its step 3 (matcher improvement), which the owner chose to include.

Move both to done when the phase completes.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements and scope
- `.planning/ROADMAP.md` § Phase 6: goal, the 4 success criteria, notes on the WIP and script shape
- `.planning/REQUIREMENTS.md`: IMG-01, IMG-02, IMG-03, UI-01
- `.planning/todos/pending/2026-10-24-cover-skipped-when-igdbupdatedat-set.md`: origin of IMG-01
- `.planning/todos/pending/2026-10-24-portal-igdbid-wrong-match.md`: origin of IMG-03 and the matcher

### Data-fix pattern
- `Database/38_FixHalfLifeIgdbId.sql`: the exact template for script 39 (guard, transaction, cleared columns, exemption note, REVERSAL block)
- `Database/34_FixStoredProcedure_UpsertIGDBGame.sql`, `Database/33_RemoveUniqueIndex_IGDBId.sql`: IGDBId is not unique, and the upsert is user-scoped and UPDATE-only

### Codebase maps
- `.planning/codebase/CONVENTIONS.md`, `.planning/codebase/TESTING.md`, `.planning/codebase/INTEGRATIONS.md` (IGDB)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `GamePlanner.Tests/Support/ApiFactory.cs`, `StubHttpClientFactory.cs` and `RecordingHandler.cs` stub IGDB HTTP for manager tests against real SQL. `IgdbServiceContractTests.cs` shows the pattern.
- `e2e/tests/uat.spec.ts` is the Playwright UAT. It already has worker "Run now" tests (around line 215) that can drive the image and sync workers.
- `IgdbService.OptimizeCoverUrl` / `OptimizeImageUrl` build the URLs. `lightboxUrl()` swaps `t_screenshot_big` for `t_1080p`.

### Established Patterns
- Writes go through stored procedures via Dapper (`sp_UpdateGameCover`, `sp_UpsertGameImage`, `sp_UpsertIGDBGame`), and reads through EF LINQ.
- Worker managers return `(FailedCount, FailedNames/Detail)` and keep a `Task.Delay` between IGDB calls.
- IGDB query strings are built inline in `IgdbService` with escaped names (`search "..."; fields ...; limit N;`).

### Integration Points
- `GameImageManager.SyncCoversAsync` (lines ~60-133) and `SaveCoverAsync` (~135-160): IMG-01 and the cursor fix.
- `GameImageManager.SyncScreenshotsAndArtworksAsync` / `SaveImagesAsync`: the IMG-02 WIP.
- `GameSyncManager.EnrichGameAsync` (~85-95): matcher insertion point. The batch select (~49) must also project `SteamAppId`.
- `IgdbService.SearchGameAsync` (line 65) returns the first of `limit 3`. The matcher needs all results, or a new method that returns them.
- `game-detail.component.ts` / `.html` / `.css`: the UI-01 WIP, plus Escape in `handleLightboxKeydown`.

</code_context>

<specifics>
## Specific Ideas

- Proof rows: Half-Life 107683 and Half-Life 2 107687 (UserId 2) for IMG-01; Portal 107693 (`SteamAppId` 400, current IGDBId 14546, 1986) for IMG-03.
- Script number 39 is free (STATE.md). Expected filename: `Database/39_FixPortalIgdbId.sql`.
- Keep the WIP in the working tree until it is committed: no stash, checkout or reset (STATE.md blocker).

</specifics>

<deferred>
## Deferred Ideas

- Using the improved matcher in `GameEnrichmentManager` and the cover name search: not chosen, sync path only (D-08).
- Re-matching suspects found by the D-07 scan: needs a per-row decision, so a future todo if the scan finds any.
- Showing artworks in the carousel or lightbox: new display capability.
- Touch/swipe navigation in the lightbox: new capability, not discussed.

### Reviewed Todos (not folded)
- `2026-10-24-steam-openid-followups` → Phase 7
- `2026-10-27-manual-playtime-entry` → Phase 8
- `2026-10-24-ui-copy-and-type-scale-cleanup` → Phase 9

</deferred>

---

*Phase: 06-game-images-metadata*
*Context gathered: 2026-10-27*
