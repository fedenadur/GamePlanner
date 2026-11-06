---
phase: 06-game-images-metadata
plan: 06
subsystem: verification
tags: [lightbox, e2e, playwright, owner-checkpoint, todos]
status: complete
requires: [06-01, 06-02, 06-03, 06-04, 06-05]
requirements: [UI-01, IMG-01, IMG-02, IMG-03]
---

# Phase 6 Plan 06: Live verification and todo closure Summary

Lightbox e2e passed twice within the 250 ms budget, the UAT-side API and repro page check passed, the owner confirmed their Portal and sibling-pair pages (D-26), the full UAT suite exited 0 (18 passed), and both folded todos were closed.

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

## Owner page check (D-26)

OWNER_PAGES=confirmed

The owner opened their own pages as themselves on 2026-11-05 (Claude never logged in as the owner):

- Portal `/games/107693`: cover is Valve's Portal, screenshots present, prev/next buttons and arrow keys work, counter shown, Escape closes.
- Dark Messiah `/games/107697` and `/games/107698` (SIBLING_PAIR): OK, same screenshots, each once.

**Observation (not a gap):** the owner noted the page shows no release date anywhere. This is expected: neither `GameDto` nor `GameDetailDto` exposes `FirstReleaseDate` and no template renders it, so the plan's "release date reads 2007" question could not be answered on the page. The 2007-10-10 value is proven by SQL (`PORTAL_OK`, 06-05). The owner asked for a follow-up, captured as `.planning/todos/pending/2026-11-05-release-year-next-to-title.md` (stays pending; committed with this plan's docs commit).

## Full suite

`06-06-full-suite.log`: `SUITE_STARTED_AT=2026-11-05T18:08:20Z`, run with `UAT_API_STARTED_AT=2026-11-05T17:46:58Z`, `SUITE_EXIT=0`.

```
18 passed (2.9m)
  auth x4, workflows x8, lightbox x2 (both passed), workers x4 (GameSync 2.2m, GameImageSync 8.7s, GameHLTBSync 17.0s, SteamPlaytime restart-initiated 20ms)
  NAV_IMAGE_MS next attempt=1 ms=1.2 budget=250
  NAV_IMAGE_MS prev attempt=1 ms=15.5 budget=250
```

Live trace check: all three run-now workers (GameSyncWorker, GameImageSyncWorker, GameHLTBSyncWorker) have `LastRunAt` at or after `SUITE_STARTED_AT` (count = 3), and `SUITE_STARTED_AT` is after `UAT_API_STARTED_AT`, so the suite ran on the stack 06-05 restarted.

## Todo moves

- `2026-10-24-cover-skipped-when-igdbupdatedat-set.md`: pending -> completed with `## Resolution` (06-01 commits, T1a-T1f, `REPRO_OK` 06-05, `REPRO_PAGE_OK` 06-06, regression checks, D-03 workaround not used).
- `2026-10-24-portal-igdbid-wrong-match.md`: pending -> completed with `## Resolution` (script 39, 14546 -> 71, `PORTAL_OK`, owner confirmation, 06-04 matcher commits, `MATCHER_OK`).
- `2026-10-27-igdbid-suspect-rematch.md`: stays pending (D-07).
- `2026-11-05-release-year-next-to-title.md`: new, stays pending.

## Gaps

None. (The missing release date on the page is an observation recorded above, not a gap against this phase's requirements.)

## Known Stubs

None.

## Threat Flags

None. No new endpoints, auth paths or schema changes; no stack restart, no owner login, no forced worker run.

## Self-Check: PASSED

- Evidence logs present: `06-06-lightbox.log`, `06-06-page-api-check.log`, `06-06-full-suite.log` (`SUITE_EXIT=0`).
- Commits `2af1835` and `1eddb9f` exist (Task 1 evidence and CSS fix).
- Both resolved todos exist under `.planning/todos/completed/` with `## Resolution`; the suspect todo remains in `pending/`.
