---
created: 2026-11-05T18:30:00.000Z
title: Show the release year as "(YYYY)" to the right of the game title everywhere the title appears
area: ui
severity: minor
files:
  - GamePlanner.API/Domain/GameDto.cs
  - GamePlanner.API/Mapping/GameMapping.cs
  - game-planner-ui/src/app/services/game.service.ts
  - game-planner-ui/src/app/components/game-detail/game-detail.component.html
  - game-planner-ui/src/app/components/game-card/game-card.component.html
---

## Problem

Owner request during the Phase 6 page check (D-26, 2026-11-05): the game-detail page does not show
the release date at all. `Games.FirstReleaseDate` is stored (Portal 107693 = 2007-10-10 after
script 39), but neither `GameDto` nor `GameDetailDto` exposes it and no template renders it.

## Wanted

Show the release year as `(YYYY)` to the right of the game name, e.g. "Portal (2007)", in every
place a game title is shown: game detail header, library cards, Now Playing, What to Play
results, Journal tiles, Dashboard, picker lists. When `FirstReleaseDate` is NULL, show the name
alone (no empty parentheses).

## Notes

- Expose `FirstReleaseDate` (or just the year) on `GameDto` so every list gets it.
- Fold into the MVP plan (docs/superpowers/plans/2026-11-05-mvp-now-playing-recommender.md),
  e.g. Task 5 (DTO) + Task 17 (copy pass), or do as a quick task after Phase 6.
