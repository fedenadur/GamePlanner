---
created: 2026-10-27T22:45:00.000Z
title: Allow manual playtime entry for games Steam can't sync (family-shared, non-Steam)
area: api
severity: minor
files:
  - GamePlanner.API/Managers/SteamPlaytimeSyncManager.cs
  - game-planner-ui/src/app/components/game-detail/game-detail.component.html
  - game-planner-ui/src/app/components/game-detail/game-detail.component.ts
---

## Problem

Observed after quick task 261027-qhu. "Hollow Knight: Silksong" (`Games.Id 108154`, UserId 2)
shows "Not played" although the user has ~40h on it in Steam.

- The game is family-shared, so it is not returned by `IPlayerService/GetOwnedGames` (540 owned
  games, appid 1030300 absent). The public Steam Web API exposes no playtime for borrowed games.
- The row also has `SteamAppId = NULL` (added from IGDB), and `SteamPlaytimeSyncManager` only
  matches by `SteamAppId`.
- No request DTO or UI exposes `PlaytimeHours`, so the user cannot enter hours manually.

## Solution / Next steps

1. Add `PlaytimeHours` to the game update flow (request DTO + stored procedure, per the
   SP-for-writes convention) and an editable "Horas jugadas" field in game detail.
2. Decide how the Steam sync treats manually entered hours. Today it overwrites whenever
   `PlaytimeHours` differs, which only matters if the game also has a `SteamAppId` that Steam
   returns. Options: never overwrite with a lower value, or flag the value as manual.
