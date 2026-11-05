---
phase: 06-game-images-metadata
plan: 06
subsystem: verification
tags: [lightbox, e2e, playwright, owner-checkpoint, todos]
status: in-progress
requires: [06-01, 06-02, 06-03, 06-04, 06-05]
requirements: [UI-01, IMG-01, IMG-02, IMG-03]
---

# Phase 6 Plan 06: Live verification and todo closure Summary (IN PROGRESS)

Task 1 is done and committed. The plan is paused at the Task 2 owner checkpoint (D-26). Tasks 2 (record) and 3 (full suite, todo closure) and the final sections of this file are still to be written by the continuation agent.

## Lightbox e2e

Run on the 06-05 stack (not restarted), as uat-test@gameplanner.local. Final log: `06-06-lightbox.log` (`LIGHTBOX_RUN_1_EXIT=0`, `LIGHTBOX_RUN_2_EXIT=0`).

```
Run 1: 2 passed (3.2s)
  NAV_IMAGE_MS next attempt=1 ms=8.2 budget=250
  NAV_IMAGE_MS prev attempt=1 ms=11.6 budget=250
Run 2: 2 passed (3.4s)
  NAV_IMAGE_MS next attempt=1 ms=2.7 budget=250
  NAV_IMAGE_MS prev attempt=1 ms=12.4 budget=250
```

Every swap met the 250 ms budget on the first attempt (no retry used). Network is not throttled (known limitation from 06-03).

## API and repro page check

`06-06-page-api-check.log`, `PAGE_API_CHECK_EXIT=0`:

```
REPRO_API_OK id=108276 screenshots=12
  owner id 107693: images=[] detail=404
  owner id 107697: images=[] detail=404
  owner id 107698: images=[] detail=404
OWNER_IDS_SCOPED_OK
REPRO_PAGE_OK cover=t_cover_big carousel=12
```

Repro row 108276 was then soft-deleted with the UAT token (`DELETE` 204) and reads `IsActive = 0` (`REPRO_REMOVED`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Lightbox counter clipped on a 1280x600 viewport**
- **Found during:** Task 1, first lightbox runs (both runs failed the `counter stays visible on a short viewport` test: counter bottom 606 > 600; the other test passed)
- **Issue:** `.lightbox-img` allowed `max-height: 90vh`; the counter is positioned 2.25rem below the content box, so a tall image pushed it past the viewport bottom.
- **Fix:** `.lightbox-img { max-height: calc(100vh - 6rem); }` (one line; assertions and the 250 ms budget untouched). The ng serve dev server picked the change up without a restart; the log was truncated and both runs re-done, both green.
- **Files modified:** `game-planner-ui/src/app/components/game-detail/game-detail.component.css`
- **Commit:** `1eddb9f`

Evidence commit for Task 1: `2af1835`.

## Tracer note

Task 1 is typed `tracer`; its verify passed end-to-end. The interactive tracer gate is folded into the Task 2 owner checkpoint that follows immediately.

<!-- gsd:write-continue -->
