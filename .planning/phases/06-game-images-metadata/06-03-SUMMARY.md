---
phase: 06-game-images-metadata
plan: 03
subsystem: game-detail-screenshots
tags: [gameimages, lightbox, karma, xunit, playwright]
status: complete
requires: [06-01]
provides:
  - "GET /api/gameimages optional gameId filter (user-scoped, wins over igdbId)"
  - "GameService.getGameImages(type, igdbId, gameId)"
  - "Escape closes the game-detail lightbox"
  - "GameImagesControllerTests (T4), game-detail Karma specs, lightbox e2e describe (D-16, D-27 gate)"
affects: [06-06]
tech-stack:
  added: []
  patterns:
    - "Per-local-game image read: gameId promoted, igdbId kept as legacy param"
key-files:
  created:
    - GamePlanner.Tests/GameImagesControllerTests.cs
    - game-planner-ui/src/app/components/game-detail/game-detail.component.spec.ts
  modified:
    - GamePlanner.API/Controllers/GameImagesController.cs
    - game-planner-ui/src/app/services/game.service.ts
    - game-planner-ui/src/app/components/game-detail/game-detail.component.ts
    - e2e/tests/uat.spec.ts
key-decisions:
  - "D-19: gameId filter added on top of the unchanged i.Game.UserId == userId base predicate; the page sends game.id so sibling games sharing an IGDBId never show duplicate screenshots"
  - "D-12: Escape closes the lightbox; D-11/D-14 WIP behaviour untouched"
  - "D-16/D-27: lightbox e2e authored with a 250 ms swap gate (one retry per swap), executed later in 06-06"
requirements-completed: [UI-01, IMG-02]
duration: 12 min
completed: 2026-11-05
---

# Phase 6 Plan 03: Per-game screenshots and lightbox Escape Summary

Game-detail now reads screenshots per local game (`gameId`), so IGDBId siblings no longer double the carousel and counter, Escape closes the lightbox, and the D-16/D-27 lightbox e2e test is authored and listed (run deferred to 06-06).

## Tasks and commits

| Task | Commit | Result |
|------|--------|--------|
| 1 (tracer): per-game read | `24a7379` | Controller `gameId` filter, service param, component passes `g.id`, `GameImagesControllerTests` (5 facts), Karma spec |
| 2a: RED | `354e06c` | Four lightbox-key specs; the Escape spec failed ("Expected 1 to be null") as intended |
| 2b: GREEN + e2e | `b6e345f` | Escape branch; `lightbox` e2e describe placed before `workers` |

The tracer's `<verify>` was fully automated and passed, so no mid-flight human checkpoint was raised (`human_verify_mode` is end-of-phase).

## Test results

- `GameImagesControllerTests`: 5 passed, 0 failed, on `GamePlannerDB_Test` (A1/A2 return 3 rows each in ascending Id, other user's game and no-image game return 0, legacy igdbId returns 6, igdbId+gameId returns 3, artworks stay separate).
- Karma (`npm run test:ci`): 13 of 13 pass (9 after the tracer, 13 after Task 2). `ng build --configuration development` succeeds.
- The WIP diff of `game-detail.component.ts` against the 06-01 UI-01 WIP commit (`c2e3d48`) shows only the `loadScreenshots` change and the Escape branch. `preloadNeighbors`, the modulo wrap and the 20 ms fade are unchanged.

## `npx playwright test --list --grep lightbox`

```
Listing tests:
  uat.spec.ts:280:9 › GamePlanner UAT › lightbox › lightbox: buttons, arrow keys, wrap, counter, Escape, preload
  uat.spec.ts:329:9 › GamePlanner UAT › lightbox › lightbox: counter stays visible on a short viewport
Total: 2 tests in 1 file
```

## E2E status

The e2e run is pending in plan 06-06 by design (prohibition). No Playwright test was executed, no API or Angular server was started, no worker was triggered, and `GamePlannerDB` was not touched. 06-06 should paste every `NAV_IMAGE_MS` line into its SUMMARY. Known limitation: the network is not throttled. The e2e file could not be type-checked (no tsconfig or local tsc in `e2e/`); Playwright parsed it without errors during `--list`.

## Deviations from Plan

None - plan executed exactly as written. The fixture helper accepts worker run statuses 200, 202 or 409 when it has to create `uat-lightbox-fixture`, which is a robustness choice inside the plan's described behaviour.

## Known Stubs

None.

## Threat Flags

None. T-06-06 (IDOR via gameId) is mitigated and covered by `GameIdFilter_ForAnotherUsersGame_ReturnsEmpty`.

## Self-Check: PASSED

Files found: `GamePlanner.Tests/GameImagesControllerTests.cs`, `game-planner-ui/src/app/components/game-detail/game-detail.component.spec.ts`, `GamePlanner.API/Controllers/GameImagesController.cs`, `e2e/tests/uat.spec.ts`. Commits found in `git log`: `24a7379`, `354e06c`, `b6e345f`.
