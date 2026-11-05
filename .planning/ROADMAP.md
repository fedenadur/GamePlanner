# Roadmap: GamePlanner

## Milestones

- ✅ **v1.0 Security Hardening** — Phases 1-5 (shipped 2026-10-27) — [archive](milestones/v1.0-ROADMAP.md)
- 🚧 **v1.1 Cleanup + Platform Analysis** — Phases 6-10 (in progress)

## Overview

v1.1 closes the backlog left after v1.0 and settles, with evidence, whether another game store can be integrated the way Steam is. Phase 6 goes first because it owns the uncommitted image and lightbox work already in the working tree: it reviews and commits that work, then fixes the remaining cover and IGDBId data problems. Phase 7 closes the Steam OpenID follow-ups (S5-S7 plus the `UQ_Users_Email` NULL bug). Phase 8 adds manual playtime entry on top of the committed game-detail screen and stops Steam sync from lowering it. Phase 9 does the copy and type-scale cleanup last, so the new scale also covers the Phase 6 and Phase 8 UI. Phase 10 records the platform recommendation. The research docs for it are already written. Each numbered SQL script takes the next free number in `Database/` when its phase runs (39 is free when this roadmap is written).

## Phases

**Phase Numbering:**

- Integer phases (6, 7, 8): Planned milestone work, continuing from v1.0
- Decimal phases (6.1, 6.2): Urgent insertions (marked with INSERTED)

<details>
<summary>✅ v1.0 Security Hardening (Phases 1-5) — SHIPPED 2026-10-27</summary>

- [x] Phase 1: Angular EOL Upgrade (6/6 plans) — completed 2026-08-29
- [x] Phase 2: Credential & Secrets Hardening (5/5 plans) — completed 2026-08-30
- [x] Phase 3: Auth, CORS & Rate Limiting Hardening (3/3 plans) — completed 2026-08-31
- [x] Phase 4: Data Layer Reliability & Audit Trail (3/3 plans) — completed 2026-10-24
- [x] Phase 5: Regression Testing & Full-Stack Verification (6/6 plans) — completed 2026-10-27

</details>

### 🚧 v1.1 Cleanup + Platform Analysis (In Progress)

**Milestone Goal:** Close the 5 pending todos plus the in-progress image fix, and decide, with evidence, which other game store (if any) to integrate next.

- [ ] **Phase 6: Game Images & Metadata** - Land the uncommitted image/lightbox work, then make sure every game gets its cover, screenshots and correct IGDB match
- [ ] **Phase 7: Steam Login Hardening** - Any number of first-time Steam users can log in, and the OpenID round trip is browser-bound, rate limited and safe against double submits
- [ ] **Phase 8: Manual Playtime** - User can record hours Steam can't sync, and Steam sync never lowers them
- [ ] **Phase 9: UI Copy & Type Scale** - Error and empty states give a next step, and the stylesheet uses one smaller type scale
- [ ] **Phase 10: Platform Recommendation** - Record which store to integrate next, based on the finished GOG/Epic/Xbox analysis

## Phase Details

### Phase 6: Game Images & Metadata

**Goal**: Every local game shows its correct cover and full screenshot set, and the user can browse screenshots in the game-detail lightbox
**Depends on**: Nothing (first v1.1 phase)
**Requirements**: IMG-01, IMG-02, IMG-03, UI-01
**Success Criteria** (what must be TRUE):

  1. A game whose `CoverImageUrl` is NULL but whose `IGDBUpdatedAt` is already set (e.g. Half-Life 107683, Half-Life 2 107687) shows a cover after one non-force `GameImageSyncWorker` run, while games that already have a cover still skip the refetch when IGDB data is unchanged
  2. When two or more local games share one IGDBId, each of their game-detail pages shows the same screenshots and artworks after an image sync
  3. Portal (`Games.Id 107693`) shows Valve's 2007 Portal (2007 release date, matching cover) after the numbered fix script is applied and a non-force re-sync runs; the script refuses to change anything if the row is not in the expected state, and it includes a working rollback section
  4. In the game-detail lightbox the user can move between screenshots with prev/next buttons and the Left/Right arrow keys, wrapping at both ends and showing a position counter, and the next or previous image appears without a visible load delay

**Notes**: Start by reviewing and committing the uncommitted WIP in `GamePlanner.API/Managers/GameImageManager.cs` (IGDBId to local games is now one-to-many) and the `game-detail` component (lightbox prev/next, arrow keys, neighbor preload). Finish and verify it; do not rewrite it. IMG-01 also touches `GameImageManager.SaveCoverAsync`, so it builds on the committed WIP. IMG-03 follows the shape of `Database/38_FixHalfLifeIgdbId.sql` (guarded, reversible) and needs the owner's go-ahead because it writes a live row.
**Plans**: 4/6 plans executed

Plans:

- [x] 06-01-PLAN.md — Commit the WIP, build the GamePlannerDB_Test harness and move every DB test onto it (D-18, D-23, D-25), fix cover sync (D-01/D-02, per-row retry of a failed batch), sync images before covers [wave 1]
- [x] 06-02-PLAN.md — Portal script 39 with dry-run and REVERSAL proof, D-07 suspect scan, matcher preview with expected ids from the committed 06-04 queries, sibling-pair gate, owner checkpoint (apply in SSMS + restart heads-up) [wave 3, checkpoint]
- [x] 06-03-PLAN.md — Per-game screenshot read (`gameId`, D-19), Escape (D-12), Karma specs, lightbox e2e test with timed swap (D-16, D-14) [wave 2]
- [x] 06-04-PLAN.md — Steam AppId matcher via IGDB external_games (D-08..D-10, sorted query, selection in the manager), search limit 10 (D-20), Id-cursor sync loop, paged image fetch (D-21) [wave 2]
- [ ] 06-05-PLAN.md — Owner confirmation checkpoint (D-24), single controlled restart, live data verification of SC1-SC3 recomputed with live SQL (D-17, D-22) [wave 4, checkpoint]
- [ ] 06-06-PLAN.md — Lightbox e2e twice with the 250 ms gate (D-27), UAT-side API and repro page check, owner page checkpoint (D-26), full suite gated on exit code, close confirmed todos [wave 5, checkpoint]

**UI hint**: yes

### Phase 7: Steam Login Hardening

**Goal**: Steam login works for any number of first-time Steam users, and the OpenID round trip is bound to the browser that started it, rate limited, and safe against double submits
**Depends on**: Nothing functional (independent of Phase 6; ordered after it so numbered SQL scripts stay sequential)
**Requirements**: AUTH-03, AUTH-04, AUTH-05, AUTH-06
**Success Criteria** (what must be TRUE):

  1. Two different Steam accounts with no email can each complete a first-time Steam login and get their own user row, with no `UQ_Users_Email` error, while email/password registration still rejects a duplicate email
  2. Sending the same Steam callback twice (double click or concurrent requests) logs the user in instead of returning a 500
  3. A Steam callback that does not carry the matching nonce from the browser that started the login is rejected, and a normal login from the starting browser (including the owner's existing Steam account) still succeeds
  4. Requests to `/api/auth/steam/callback` and `/api/auth/steam/exchange` beyond the limit get 429 and are logged as rate-limit rejections, and after a successful Steam login the browser address bar and history no longer contain `?code=`

**Notes**: AUTH-03 replaces `UQ_Users_Email` with a filtered unique index (`WHERE Email IS NOT NULL`) in the next numbered SQL script. AUTH-04 catches `DbUpdateException` in `HandleCallbackAsync` and re-reads the user by SteamId. Update the Phase 5 (05-04) Steam tests that assume only one email-less user can exist.
**Plans**: TBD

### Phase 8: Manual Playtime

**Goal**: The user can record hours played for games Steam can't sync (family-shared or non-Steam), and Steam sync never lowers a value the user entered
**Depends on**: Phase 6 (the game-detail WIP must be committed before the playtime field is added to the same component)
**Requirements**: PLAY-01, PLAY-02
**Success Criteria** (what must be TRUE):

  1. On the game-detail page the user can enter or edit hours played for any of their games, including games with no `SteamAppId` (e.g. Hollow Knight: Silksong, `Games.Id 108154`), and the value is still there after a reload instead of "Not played"
  2. The playtime write only changes the caller's own game; a request against another user's game id changes nothing
  3. When Steam reports more hours than the stored value, the Steam playtime sync raises `PlaytimeHours` to the Steam value
  4. When Steam reports fewer hours than the stored value (e.g. after a higher manual entry), the sync leaves the stored value unchanged

**Notes**: Per the project convention, the write goes through a stored procedure in the next numbered SQL script and reads stay on EF LINQ. Today no request DTO or UI exposes `PlaytimeHours`. PLAY-02 changes `SteamPlaytimeSyncManager`, which today overwrites whenever the values differ.
**Plans**: TBD
**UI hint**: yes

### Phase 9: UI Copy & Type Scale

**Goal**: Error and empty states tell the user what to do next, and the app uses one consolidated type scale
**Depends on**: Phase 8 (the type scale is consolidated after the Phase 6 lightbox and Phase 8 playtime UI land, so it covers them)
**Requirements**: UI-02, UI-03, UI-04
**Success Criteria** (what must be TRUE):

  1. With the API stopped, submitting the login or register form shows "Could not connect to server. Check your connection and try again." (or equivalent wording with a next step)
  2. A user with zero games sees a distinct "No games yet" empty state with a way to add their first game, while a user whose filters match nothing still sees "No games match your filters."
  3. The stylesheet uses fewer than the current 8 font sizes and 4 weights, and existing screens (login, register, dashboard, games list, game detail with lightbox and playtime field) show no broken layout or unreadable text
  4. Existing Karma specs and the Playwright UAT pass, with their assertions updated to the new copy

**Plans**: TBD
**UI hint**: yes

### Phase 10: Platform Recommendation

**Goal**: The decision on which game store, if any, to integrate after Steam is recorded with evidence and confirmed by the owner
**Depends on**: Nothing (documentation only; can run at any point)
**Requirements**: PLAT-01, PLAT-02, PLAT-03, PLAT-04
**Success Criteria** (what must be TRUE):

  1. `GOG.md`, `EPIC.md`, `XBOX.md` and the comparison `README.md` exist in `.planning/research/platforms/`, written in Spanish, each with login, library, metadata, images/covers, playtime, API/auth, limits/ToS, sources and a verdict (XBOX.md also covers achievements and Game Pass catalog vs user library)
  2. PROJECT.md Key Decisions has a row naming the platform to integrate next (or none) with the rationale and a link to the comparison README
  3. The README's "Recomendación propuesta (a confirmar en PLAT-04)" section now states the owner-confirmed recommendation and matches the Key Decisions row

**Notes**: PLAT-01..03 were already committed (408c1ea, 865eea1), so the remaining work is PLAT-04. The proposal to confirm or change: Xbox next, starting with a spike on the owner's account, and no GOG/Epic login integration.
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 6 → 7 → 8 → 9 → 10

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Angular EOL Upgrade | v1.0 | 6/6 | Complete | 2026-08-29 |
| 2. Credential & Secrets Hardening | v1.0 | 5/5 | Complete | 2026-08-30 |
| 3. Auth, CORS & Rate Limiting Hardening | v1.0 | 3/3 | Complete | 2026-08-31 |
| 4. Data Layer Reliability & Audit Trail | v1.0 | 3/3 | Complete | 2026-10-24 |
| 5. Regression Testing & Full-Stack Verification | v1.0 | 6/6 | Complete | 2026-10-27 |
| 6. Game Images & Metadata | v1.1 | 4/6 | In Progress|  |
| 7. Steam Login Hardening | v1.1 | 0/TBD | Not started | - |
| 8. Manual Playtime | v1.1 | 0/TBD | Not started | - |
| 9. UI Copy & Type Scale | v1.1 | 0/TBD | Not started | - |
| 10. Platform Recommendation | v1.1 | 0/TBD | Not started | - |
