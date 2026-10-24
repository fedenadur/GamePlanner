---
created: 2026-10-24T22:00:00.000Z
title: Portal carries a likely wrong IGDBId (14546, 1986 release date)
area: database
severity: minor
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
