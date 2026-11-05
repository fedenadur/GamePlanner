---
phase: 06-game-images-metadata
plan: 04
subsystem: igdb-matcher
tags: [igdb, external-games, steam, sync, paging, xunit]
status: complete
requires: [06-01]
provides:
  - "IgdbService.FetchExternalGamesBySteamAppIdAsync (raw external_games rows, Steam source, quoted uid)"
  - "GameSyncManager matcher: Steam mapping (lowest game id) -> exact name -> first hit, NULL-IGDBId rows only"
  - "SearchGameAsync limit 10; Id-cursor sync loop; offset-paged screenshot/artwork fetch"
affects: [06-02, 06-05]
tech-stack:
  added: []
  patterns:
    - "Service builds the IGDB query and returns rows; the manager makes the choice and owns the loop"
    - "Id cursor (g.Id > lastId) for batch loops over a filter that shrinks while it is processed"
key-files:
  created:
    - GamePlanner.Tests/GameSyncManagerMatcherTests.cs
  modified:
    - GamePlanner.API/Domain/IgdbModels.cs
    - GamePlanner.API/Services/IgdbService.cs
    - GamePlanner.API/Managers/GameSyncManager.cs
    - GamePlanner.API/Managers/GameImageManager.cs
    - GamePlanner.Tests/IgdbServiceContractTests.cs
    - GamePlanner.Tests/GameImageManagerTests.cs
key-decisions:
  - "D-09: FetchExternalGamesBySteamAppIdAsync returns the raw rows (sorted 'sort game asc; limit 5;' by IGDB); EnrichGameAsync picks the lowest non-null game id. This differs from the RESEARCH FindGameIdBySteamAppIdAsync sketch on purpose"
  - "D-10: 'game.IGDBId ??= result.Id' stays the only IGDBId write; the matcher runs only for rows with a NULL IGDBId"
  - "D-20: SearchGameAsync asks for 10 results; SearchCoverByNameAsync and SearchMultipleAsync keep their limits"
  - "D-21: IgdbService.ImagePageSize = 500; both image fetches take an offset and sort by id; GameImageManager.FetchAllImagePagesAsync loops until a short page"
  - "Freshness skip applies only when the row already has an IGDBId; the Id cursor replaces Skip(offset) in GameSyncManager.RunAsync"
requirements-completed: [IMG-03, IMG-02]
duration: 12 min
completed: 2026-11-05
---

# Phase 6 Plan 04: Steam AppId matcher, search limit 10 and paged image fetch Summary

Unmatched Steam rows are now matched through IGDB's exact external_games mapping before any name search, the name search sees 10 results, one sync run reaches every row thanks to an Id cursor, and screenshot/artwork fetches are paged so batches with more than 500 images are no longer cut off.

PRE_0604_BASE=27a7e61fd0dcb30b6b79c2b061a319bfc41267a5

## Tasks and commits

| Task | Commit | Result |
|------|--------|--------|
| 1 (tracer): Steam AppId matcher | `6e5f3cd` | `IgdbExternalGame`, `FetchExternalGamesBySteamAppIdAsync`, matcher branch in `EnrichGameAsync`, 3 contract facts, T3a and T3g on `GamePlannerDB_Test` |
| 2a: RED | `8893ba4` | `SearchGame_AsksForTenResults`, T3b-T3f, T3h; exactly 4 failed (limit 10 contract, T3b, T3f, T3h) |
| 2b: GREEN | `7807fa5` | `limit 10;`, freshness skip only with an IGDBId, Id cursor in `RunAsync` |
| 3a: RED | `9c95db6` | Contract offset/sort/limit assertions, `FetchArtworks_SendsTheRequestedOffset`, T5 (compile failure counts as red) |
| 3b: GREEN | `5813a5c` | `ImagePageSize`, offset parameters, `FetchAllImagePagesAsync` |

The tracer's `<verify>` was fully automated and passed, so no mid-flight human checkpoint was raised.

## Test results

All on `GamePlannerDB_Test` with a stubbed IGDB (`IgdbStub`). Full solution run after the last commit: `dotnet build GamePlanner.sln` clean, `dotnet test GamePlanner.Tests` 88 passed, 0 failed.

- `GameSyncManagerMatcherTests` (8 facts, T3a-T3h): pass. T3h inserts 51 rows with a stub that fully enriches them; with the old `Skip(offset)` paging it failed (RED), with the Id cursor all 51 rows are matched in one run and each name is searched exactly once.
- `IgdbServiceContractTests`: external_games query shape, string uid deserialization, empty on unmapped, `limit 10;`, screenshot/artwork offset and `sort id asc; limit 500;` all pass.
- `GameImageManagerTests` (8 facts incl. `ImageRichBatch_PagesScreenshotsUntilAShortPage`: 503 rows, requests at offsets 0 then 500, one artworks request) and `GameManagerAtomicityTests` pass.
- Verify-block checks: `SearchCoverByNameAsync` still `limit 3;`; `git diff --quiet 27a7e61 -- GamePlanner.API/Managers/GameEnrichmentManager.cs` succeeds (D-08: legacy enricher byte-for-byte unchanged).

## D-09 split (differs from the RESEARCH sketch on purpose)

`IgdbService.FetchExternalGamesBySteamAppIdAsync(int, ct)` only builds `fields uid,game; where uid = "<appid>" & external_game_source = 1 & game != null; sort game asc; limit 5;` and returns the deserialized rows unchanged. `GameSyncManager.EnrichGameAsync` filters non-null `Game`, orders by it and takes the lowest, so the choice lives in the manager (RESEARCH sketched a `FindGameIdBySteamAppIdAsync` returning `long?` that would have chosen inside the service). `sort game asc` guarantees the lowest ids are inside the 5-row window. T3g locks the lowest-id choice with the higher id returned first by the stub.

## D-20 side effect

`SearchGameAsync` is shared, so the legacy `GameEnrichmentManager` name search also sees 10 candidates now. Its code is unchanged. `SearchCoverByNameAsync` and `SearchMultipleAsync` keep their limits.

## Freshness-skip finding

The old skip compared IGDB `updated_at` with a stored `IGDBUpdatedAt` even for rows with no IGDBId. For such rows the stamp comes from an earlier name-search cover of possibly another game, so the Steam match was skipped forever. Live rows 108266 (Servant of the Lake) and 108267 (The Blood of Dawnwalker) have NULL IGDBId with a stored stamp and are unblocked by `igdbId.HasValue &&` in the condition (T3f). Also, the old `Skip(offset)` paging skipped unprocessed rows once enriched rows left the filter (RESEARCH Pitfall 3); the Id cursor fixes that, which 06-05's `MATCHER_OK` depends on.

## Deviations from Plan

None - plan executed exactly as written. Two test-authoring notes: the T3b stub returns 5 hits only when the body asks for `limit 10;` (otherwise 3), which is what makes T3b discriminate the D-20 change; and `ids[^1]` in T3h was hoisted into a local because expression trees reject index-from-end (a compile fix inside my own test).

## Known Stubs

None.

## Threat Flags

None. The only new outbound surface is the external_games query to IGDB; `steamAppId` is an `int`, so interpolation cannot inject (T-06-11).

## Guard notes

No API or dev server was started, no worker was triggered, and `GamePlannerDB` was not touched. All DB tests ran against `GamePlannerDB_Test`. The matcher's first live effect on the owner's rows is left to plan 06-05 after the owner's go-ahead.

## Self-Check: PASSED

Files found: `GamePlanner.Tests/GameSyncManagerMatcherTests.cs`, `GamePlanner.API/Domain/IgdbModels.cs`, `GamePlanner.API/Services/IgdbService.cs`, `GamePlanner.API/Managers/GameSyncManager.cs`, `GamePlanner.API/Managers/GameImageManager.cs`. Commits found in `git log`: `6e5f3cd`, `8893ba4`, `7807fa5`, `9c95db6`, `5813a5c`.
