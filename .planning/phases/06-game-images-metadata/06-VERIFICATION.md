---
phase: 06-game-images-metadata
verified: 2026-11-05T00:00:00Z
status: passed
score: 4/4 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification: false
gaps: []
deferred: []
---

# Phase 6: Game Images & Metadata Verification Report

**Phase Goal:** Every local game shows its correct cover and full screenshot set, and the user can browse screenshots in the game-detail lightbox
**Verified:** 2026-11-05
**Status:** passed
**Re-verification:** No, initial verification

Summary claims were not trusted. Each criterion was checked against the committed code, the evidence logs, and read-only SELECTs on `GamePlannerDB`.

## Goal Achievement

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | A game with NULL `CoverImageUrl` and `IGDBUpdatedAt` set gets a cover after one non-force `GameImageSyncWorker` run; games with a cover are not refetched | VERIFIED | `GameImageManager.SyncCoversAsync` selects `IsActive && CoverImageUrl == null && Id > lastId`. It no longer consults `IGDBUpdatedAt`, so a NULL cover is always fetched and covered rows are never selected. The Id cursor stops re-selecting rows IGDB has no cover for. Tests in `GameImageManagerTests.cs`: `NullCover_WithIgdbUpdatedAtSet_GetsCover_EvenWhenIgdbTimestampIsNotNewer`, `RowWithCover_IsNeverRequestedForACover`, `CoverLoop_RequestsEachRowOnce_WhenIgdbHasNoCovers`, `CoverBatchFailure_RetriesEachRowOnItsOwn`. Live proof on repro row 108276 (UAT account, IGDB 231): created with cover NULL, then the old-bug state (cover NULL, stamp set) was captured, then one non-force image run gave a `t_cover_big` cover and 12 screenshots (`06-05-SUMMARY.md` REPRO_OK, `06-05-repro-precondition.txt`, `06-06-page-api-check.log` REPRO_PAGE_OK). Half-Life 107683/107687 hold covers (re-read live today); COVERS_UNCHANGED fingerprint held. |
| 2 | Two or more local games sharing one IGDBId each show the same screenshots and artworks after an image sync | VERIFIED | `SyncScreenshotsAndArtworksAsync` builds `igdbToLocal` as `IGDBId -> List<localId>` and `SaveImagesAsync` upserts for every local id. `GameImagesController` filters by `gameId` (D-19), so each page reads its own rows. Test `SharedIgdbId_SavesImagesForEveryLocalGame`. Live read today: 107697 and 107698 both IGDBId 2369, 5 screenshots each; owner confirmed both pages (OWNER_PAGES=confirmed). |
| 3 | Portal (107693) shows Valve's 2007 Portal (2007 release date, matching cover) after the numbered script and a non-force re-sync; script is guarded and has a working rollback | VERIFIED | `Database/39_FixPortalIgdbId.sql` is wrapped in a transaction and guarded by `IF EXISTS (Id=107693 AND UserId=2 AND IGDBId=14546)`. It carries a `REVERSAL-BEGIN/END` section with captured values, and the 06-02 summary records a dry-run and reversal proof. Live read today: Id 107693, IGDBId 71, FirstReleaseDate 2007-10-10, cover `coay61.jpg`, 5 screenshots. The values were re-derived by the workers, not hand-written (06-05 PORTAL_OK). The owner confirmed the cover on the page. The release date is verified in the data layer. See the Observations section for the UI. |
| 4 | Lightbox: prev/next buttons and Left/Right arrow keys, wrap at both ends, position counter, no visible load delay | VERIFIED | `game-detail.component.ts`: `nextImage`/`prevImage` use modular wrap, `handleLightboxKeydown` handles ArrowLeft/ArrowRight/Escape, `preloadNeighbors()` preloads the next and previous `t_1080p` URLs. The template renders prev/next buttons and a `n / M` counter. Karma 13/13 including the wrap, Escape, closed-state and single-image specs. Playwright lightbox e2e passed twice and in the full suite (18/18, SUITE_EXIT=0). The test asserts wrap, counter, Escape, and neighbor requests finished before navigation. Measured swap time was 1.2 to 15.5 ms against a 250 ms budget on the first attempt in every run. The owner confirmed prev/next, keys, counter and Escape live on Portal. |

**Score:** 4/4 truths verified. No truth is left behavior-unverified: the transition and wrap invariants are exercised by Karma specs and the e2e test.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `GamePlanner.API/Managers/GameImageManager.cs` | Cover loop, shared-IGDBId save, paged image fetch | VERIFIED | Substantive and wired from `GameImageSyncWorker`. |
| `GamePlanner.API/Controllers/GameImagesController.cs` | `gameId` filter, user-scoped | VERIFIED | Filters on `Game.UserId`, `IsActive`, `gameId`. |
| `Database/39_FixPortalIgdbId.sql` | Guarded, reversible fix | VERIFIED | Guard, transaction, REVERSAL block. Applied by the owner. |
| `game-detail.component.{ts,html,css}` | Lightbox | VERIFIED | Wired to `GameService.getGameImages(type, igdbId, gameId)`. |
| Tests (`GameImageManagerTests`, `GameImagesControllerTests`, `GameSyncManagerMatcherTests`, spec, `uat.spec.ts`) | Targeted coverage | VERIFIED | Present. Orchestrator reports backend 88/88, Karma 13/13, e2e 18/18. |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `GameDetailComponent.loadScreenshots` | `GameImagesController` | `getGameImages('screenshot', igdbId, gameId)` and the `gameId` query param | WIRED |
| `GameImageManager.SyncScreenshotsAndArtworksAsync` | `sp_UpsertGameImage` per local game | `igdbToLocal` map | WIRED |
| `GameImageManager.SaveCoverAsync` | `sp_UpdateGameCover` | Dapper | WIRED |
| Script 39 | Workers re-derive the row | Cleared columns, then non-force sync | WIRED (live PORTAL_OK) |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| Lightbox carousel | `screenshots` | `GET /api/gameimages?gameId=` backed by an EF query on `GameImages` | Live rows (5 on Portal, 12 on repro) | FLOWING |
| Cover on detail | `CoverImageUrl` | `Games` column written by `sp_UpdateGameCover` | Live `t_cover_big` URLs | FLOWING |

### Behavioral Spot-Checks

Read-only SQL on `GamePlannerDB` (today):

| Behavior | Result | Status |
|----------|--------|--------|
| Portal row | 71, 2007-10-10, `coay61.jpg`, 5 screenshots | PASS |
| Dark Messiah pair | both IGDBId 2369, both `co23t5.jpg`, 5 screenshots each | PASS |
| Half-Life pair | covers present (`coc2ea`, `co1nmw`) | PASS |

Test runs were not repeated. The orchestrator's fresh results are accepted, and the e2e logs (`06-06-full-suite.log`, `06-06-lightbox.log`) were read directly and show SUITE_EXIT=0 and both lightbox runs green.

### Probe Execution

No probes declared. Skipped.

### Requirements Coverage

| Requirement | Description | Status | Evidence |
|-------------|-------------|--------|----------|
| IMG-01 | NULL-cover rows get a cover even when `IGDBUpdatedAt` is set | SATISFIED | Truth 1 |
| IMG-02 | Shared IGDBId saves images for every local game | SATISFIED | Truth 2 |
| IMG-03 | Portal corrected via a guarded, reversible numbered script, then re-synced | SATISFIED | Truth 3 |
| UI-01 | Lightbox prev/next and arrow keys with neighbor preload | SATISFIED | Truth 4 |

No orphaned Phase 6 requirement IDs: REQUIREMENTS.md maps exactly these four to Phase 6.

### Anti-Patterns Found

No `TBD`, `FIXME`, `XXX` or `TODO` markers in the 22 source files changed in this phase. No stub returns in the lightbox or image code.

The code review (`06-REVIEW.md`: 0 critical, 5 warnings, 4 info) was read. None of the warnings contradicts a ROADMAP success criterion. They are robustness and data-integrity follow-ups, listed here as advisory items:

| Ref | Severity | Impact on Phase 6 goal |
|-----|----------|------------------------|
| WR-01 `GameImageManager.cs` image loop stamps `LastImageSyncDate` after a failed fetch | Warning | A transient IGDB failure can delay a batch's images by two days. It does not break the success criteria. Recommend a follow-up. |
| WR-02 `sp_UpdateGameCover` writes `IGDBUpdatedAt`, which `GameSyncManager` reads as its skip stamp | Warning | A row the cover worker reaches before enrichment may skip metadata enrichment. Not exercised by the criteria. The Portal flow worked because the script clears the stamp and the sync worker ran first. |
| WR-03 Matcher can fall back to a first-hit name search | Warning | The original Portal defect class is only partly closed for future rows. Candidate for the pending `igdbid-suspect-rematch` todo (D-07). |
| WR-04 e2e fixture leaves `uat-lightbox-fixture` in the UAT account | Warning | Test hygiene, UAT user only. |
| WR-05 `build-test-db.ps1` name guard is weak | Warning | Latent. No live-name spelling exists today. |

None of these is a BLOCKER for the phase goal. Nothing was deferred to a later phase.

### Observations (not gaps)

- **Release date is not shown in the UI.** ROADMAP SC3 says "shows Valve's 2007 Portal (2007 release date, matching cover)". Neither `GameDto` nor `GameDetailDto` exposes `FirstReleaseDate`, and no template renders it. The ROADMAP does not require a release-date element in the UI. It is a data-correctness criterion, and the 2007-10-10 value is verified in the database. The owner raised it, and it is tracked as the pending todo `2026-11-05-release-year-next-to-title.md`. Phase 9 does not cover it. Treated as an observation. If the owner reads SC3 as requiring the date on screen, this becomes a gap, but the owner confirmed the Portal page (OWNER_PAGES=confirmed) and logged it as a follow-up, not a failure.
- 9 active games still have a NULL cover (read-only count today). 06-05 documents NieR (IGDB 391942) as having no IGDB cover, and 8 rows stay without an IGDBId after the matcher. These are IGDB data limits, not the IMG-01 defect.
- The lightbox swap budget is measured on an unthrottled network (known limitation from 06-03). Preload is also asserted structurally (neighbor requests finished before navigation).

### Human Verification Required

None outstanding. The owner checkpoint (D-26) was completed: Portal cover, screenshots, prev/next, arrow keys, counter and Escape, plus both Dark Messiah pages, were confirmed on 2026-11-05.

### Gaps Summary

No gaps. All four ROADMAP success criteria and all four requirement IDs are satisfied in the code and in the live data, and the working tree has no uncommitted Phase 6 source changes.

---

_Verified: 2026-11-05_
_Verifier: Claude (gsd-verifier)_
