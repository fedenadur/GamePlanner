---
created: 2026-08-31T20:00:00.000Z
title: Half-Life / Half-Life 2 share one IGDBId under the same user
area: database
severity: minor
files:
  - GamePlanner.API/Managers/GameManager.cs
---

## Problem

Discovered incidentally during 04-03's Task 1 pre-flight checkpoint (the live-database
fact-gathering required before dropping `UX_Games_IGDBId`), not through any dedicated
investigation. Explicitly out of scope for Phase 4.

`Games.Id 107683` ("Half-Life") and `Id 107687` ("Half-Life 2") share `IGDBId = 267639`
under the same `UserId`, both `IsActive = 1`:

```
Id      | Name        | UserId | IsActive
107683  | Half-Life   | 2      | 1
107687  | Half-Life 2 | 2      | 1
```

Two distinct games should never share one `IGDBId` — that value is supposed to uniquely
identify a single title on IGDB. This is unlikely to be an application dedup-logic bug:
`GameManager.CreateFromIgdbAsync`'s existing dedup check operates on
`(IGDBId, UserId, IsActive)` and would only prevent a *second* row from being created for
the same `IGDBId` under the same user — it can't explain how two rows with *different*
names ended up sharing one `IGDBId` in the first place. The more likely explanation is a
bad match at creation time: the "Add Game" IGDB search flow picked the wrong IGDB search
result for one of these two titles (e.g. searching "Half-Life 2" resolved to the base
Half-Life's IGDBId, or vice versa), so the wrong `IGDBId` got attached to one of the two
rows.

Severity is minor/cosmetic — no rows are being silently deactivated by this, unlike the
folded upsert-collision bug 04-03 closed. It only means any `IGDBId`-keyed lookup
(catalog matching, calendar/upcoming-releases tracking) will treat these two distinct
games as one entry for this user.

## Solution / Next steps

1. Confirm the correct IGDBId for "Half-Life" and "Half-Life 2" via a fresh IGDB search
   for each title.
2. Correct whichever of the two rows (`107683` or `107687`) currently holds the wrong
   `IGDBId`.
3. No code change is expected to be required unless the same bad-match pattern turns up
   for other titles — if it does, revisit the IGDB search-result selection UX in the "Add
   Game" flow.
