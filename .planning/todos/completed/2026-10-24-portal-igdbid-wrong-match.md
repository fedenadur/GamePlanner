---
created: 2026-10-24T22:00:00.000Z
title: Portal carries a likely wrong IGDBId (14546, 1986 release date)
area: database
severity: minor
resolves_phase: 6
files:
  - GamePlanner.API/Managers/GameEnrichmentManager.cs
---

## Problem

Observed during Phase 5 plan 05-02 (D-08 research). Not fixed in Phase 5 (out of scope).

`Games.Id 107693` ("Portal", `SteamAppId` 400, UserId 2) carries `IGDBId = 14546` with
`FirstReleaseDate = 1986-11-01`. That is most likely the 1986 Infocom text adventure "Portal",
not Valve's 2007 Portal: Steam-import enrichment appears to have picked the wrong first match
by name. The same-user duplicate-title scan cannot detect this, because no other row shares the
IGDBId.

## Solution / Next steps

1. Look up the correct IGDBId for Valve's Portal (2007) via the app's IGDB search, matching exact
   title and release year.
2. Correct it with a guarded, reversible numbered script, the same shape as
   `Database/38_FixHalfLifeIgdbId.sql`, then run a non-force re-sync.
3. Consider whether Steam-import enrichment should prefer a match on `SteamAppId`/release year
   over the first name match.

## Resolution

Resolved in Phase 6 (06-game-images-metadata), 2026-11-05.

- `Database/39_FixPortalIgdbId.sql` (commit `15738e4`, guarded and reversible) was applied by the owner in SSMS on 2026-11-05: Portal 107693 IGDBId 14546 -> 71.
- `PORTAL_OK` (06-05): after the workers re-derived the row from IGDB 71, Games 107693 reads IGDBId 71, FirstReleaseDate 2007-10-10, a `t_cover_big` cover, 5 screenshots, genres and tag kept.
- Owner confirmation (D-26, 2026-11-05): the owner opened /games/107693 as themselves; the cover is Valve's Portal, screenshots show, and the lightbox works (prev/next, arrow keys, counter, Escape). The page does not display a release date at all, so the "2007" check was not applicable to the UI; follow-up in `.planning/todos/pending/2026-11-05-release-year-next-to-title.md`.
- Matcher changes (plan 06-04): `6e5f3cd`, `7807fa5` (Steam AppId match through IGDB external_games, 10 search results, first match never accepted blindly); `MATCHER_OK` (06-05): 22 of 22 NULL-IGDBId rows equal their preview.
- Remaining suspect rows from the D-07 scan are tracked in `.planning/todos/pending/2026-10-27-igdbid-suspect-rematch.md`.
