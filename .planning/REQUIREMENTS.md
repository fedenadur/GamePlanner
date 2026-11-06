# Requirements: GamePlanner — v1.1 Cleanup + Platform Analysis

**Defined:** 2026-10-27
**Core Value:** Close the backlog left after v1.0 (data, auth and UI todos plus in-progress work) and decide, with evidence, whether other game stores can be integrated like Steam.

## v1 Requirements

### Images & Metadata (IMG)

- [x] **IMG-01**: A game with `CoverImageUrl` NULL gets its cover fetched by `GameImageSyncWorker` even when `IGDBUpdatedAt` is already set (freshness skip applies only when a cover exists)
- [x] **IMG-02**: When several local games share one IGDBId, screenshots and artworks are saved for every one of them (finish and verify the uncommitted `GameImageManager` change)
- [x] **IMG-03**: Portal (`Games.Id 107693`) carries the correct IGDBId for Valve's 2007 Portal, fixed via a guarded, reversible numbered SQL script and followed by a non-force re-sync

### Game Detail (UI)

- [x] **UI-01**: User can navigate screenshots in the game-detail lightbox with prev/next controls and arrow keys, with neighbor images preloaded (finish and verify the uncommitted game-detail change)
- [ ] **UI-02**: When the server is unreachable, login and register show "Could not connect to server. Check your connection and try again." (or equivalent with a next step)
- [ ] **UI-03**: A user with zero games sees a distinct empty state with a next step (e.g. "No games yet. Add your first game to get started."), separate from "No games match your filters."
- [ ] **UI-04**: The stylesheet uses a consolidated type scale (fewer than the current 8 font sizes and 4 weights), with no visual regressions on existing screens

### Playtime (PLAY)

_Phase 8 was superseded by the MVP (play sessions): PLAY-01 is covered by session logging; PLAY-02 no longer applies because Steam playtime and manual hours no longer share `PlaytimeHours`._

- [~] **PLAY-01**: User can enter or edit hours played for a game from the game-detail screen (persisted via stored procedure)
- [~] **PLAY-02**: Steam playtime sync only overwrites `PlaytimeHours` when Steam reports a higher value than the stored one (highest value wins)

### Steam Auth (AUTH)

- [ ] **AUTH-03**: A second distinct first-time Steam login succeeds — `UQ_Users_Email` no longer treats multiple NULL emails as duplicates (filtered unique index via numbered SQL script)
- [ ] **AUTH-04**: A concurrent/double Steam callback for the same SteamId does not return 500 — `DbUpdateException` is caught and the existing user is re-read (S7)
- [ ] **AUTH-05**: The Steam OpenID callback is bound to the browser that started the login via a nonce cookie carried in `openid.return_to` and verified in `SteamCallback` (S5, login CSRF)
- [ ] **AUTH-06**: `/api/auth/steam/callback` and `/api/auth/steam/exchange` are rate limited, and the SPA removes `?code=` from browser history after the exchange (S6)

### Platform Analysis (PLAT)

- [x] **PLAT-01**: `.planning/research/platforms/GOG.md` documents (in Spanish) login, library, metadata, screenshots, covers and playtime viability for GOG, with sources and a verdict
- [x] **PLAT-02**: `.planning/research/platforms/EPIC.md` documents the same for the Epic Games Store
- [x] **PLAT-03**: `.planning/research/platforms/XBOX.md` documents the same for Xbox / Game Pass, including achievements and Game Pass catalog vs user library
- [ ] **PLAT-04**: A short comparison and recommendation (which platform, if any, to integrate next) is recorded in PROJECT.md Key Decisions

## Future Requirements

- Actual integration (login + library import + playtime) of whichever platform PLAT-04 recommends — next milestone
- Manual playtime flag/history (who set the value and when) — not needed with the highest-value-wins rule

## Out of Scope

- Implementing GOG / Epic / Xbox integrations in v1.1 — analysis only, by user decision
- Reading family-shared Steam playtime — the public Steam Web API does not expose it (verified 2026-10-27); covered instead by PLAY-01
- Broad UI redesign — UI-04 consolidates the type scale only

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| IMG-01 | Phase 6 | Complete |
| IMG-02 | Phase 6 | Complete |
| IMG-03 | Phase 6 | Complete |
| UI-01 | Phase 6 | Complete |
| AUTH-03 | Phase 7 | Pending |
| AUTH-04 | Phase 7 | Pending |
| AUTH-05 | Phase 7 | Pending |
| AUTH-06 | Phase 7 | Pending |
| PLAY-01 | Phase 8 | Superseded by MVP (play sessions) |
| PLAY-02 | Phase 8 | Superseded by MVP (no longer applies: Steam no longer shares a field with manual hours) |
| UI-02 | Phase 9 | Absorbed by MVP Task 17 |
| UI-03 | Phase 9 | Absorbed by MVP Task 17 |
| UI-04 | Phase 9 | Absorbed by MVP Task 17 |
| PLAT-01 | Phase 10 | Complete |
| PLAT-02 | Phase 10 | Complete |
| PLAT-03 | Phase 10 | Complete |
| PLAT-04 | Phase 10 | Pending |

**Coverage:**

- v1 requirements: 17 total
- Mapped to phases: 17
- Unmapped: 0

---
*Requirements defined: 2026-10-27*
*Last updated: 2026-10-27 after v1.1 roadmap creation (Phases 6-10)*
