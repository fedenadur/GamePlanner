# MVP release checklist: Now Playing + What to Play

Branch `feat/mvp-now-playing`. Spec: `docs/superpowers/specs/2026-11-05-mvp-now-playing-recommender-design.md`. The runs below were made on 2026-11-06.

## Completed features

- [x] Devices per user (profile) and one Now Playing slot per device, General slot when none.
- [x] What to Play recommender with reasons, filters and "Start on {device}".
- [x] Play sessions: log, edit, delete; hours kept apart from Steam playtime.
- [x] Finish flow (Finished with rating, Dropped, Paused) that updates the Journal.
- [x] Library independent of Steam: store, edit, filters (status, store, device, length, favorites).
- [x] Per-user preferences.
- [x] Simplified menu and admin-only Workers section (`IsAdmin`).
- [x] Robustness fixes with tests of the critical scenarios.
- [x] Actionable empty and error states and a single type scale (absorbs Phase 9).

## Tests run (real results)

| Suite | Command | Result |
|-------|---------|--------|
| Backend | `dotnet test GamePlanner.Tests` (against `GamePlannerDB_Test`) | 223 passed, 0 failed, 0 skipped, 223 total (20 s) |
| Frontend | `npx ng test --watch=false --browsers=ChromeHeadless` | 89 of 89 SUCCESS |
| E2E | `cd e2e && npx playwright test` (API on :7174 and UI on :4200 against `GamePlannerDB`, as the UAT user) | 19 passed, 0 failed (17.4 s) |

The E2E run includes `daily flow` (devices PC and Steam Deck, top pick on Steam Deck, 1,5 h session with a note, finish with 4 stars = rating 8, Journal shows Finished, game detail lists the session, What to Play no longer offers the game, cleanup and device restore; every cleanup call is checked and a failure fails the run) and `non-admin cannot see workers`.

Changes to `e2e/tests/uat.spec.ts` for the MVP behavior:

- The empty-state copy is now "No games match your filters. Clear them to see your whole library."
- Now Playing uses the per-device "Pick a game" and "Finish / remove" controls instead of the old slot buttons.
- The Journal test first removes the entry that Now Playing creates, so the picker offers the game.
- The old `/api/session/plan` check is now `/api/what-to-play`.
- The four admin-only worker "Run now" tests were replaced by a non-admin 403 check.

Cleanup verification (fix round 1): user 17's devices were set to `[]` through the API before the run; after the full Playwright run `GET /api/profile/devices` returned `{"devices":[]}` again. An earlier full run in the same session stopped at one auth test (login page rendered blank) and was re-run; the passing run is the one counted above.

## Known issues

- 6 leftover `Genres` rows named `uat-genre-<ts>` from the first daily-flow run (the spec now reuses the single name `uat-genre`, which still exists as a row after the first run). There is no API to delete genres. The owner can remove them in SSMS with `DELETE FROM Genres WHERE Name LIKE 'uat-genre-%'` after checking the junction table that references `Genres` (the game-genre link) has no rows for them.

- Admin-only worker "Run now" flows (GameSync, GameImageSync, GameHLTBSync, SteamPlaytimeSync) are no longer covered by e2e, because the UAT account is not admin. They were not re-verified in this release.
- The lightbox e2e fixture triggers `GameImageSyncWorker` when no game with 2+ screenshots exists; that needs an admin and would fail for the UAT user. It passed in the run above because a fixture game already existed.
- The e2e suite writes to the owner database as the UAT user (cleanup in `afterAll`); it needs the API and UI running.
- Phase 7 (Steam login hardening) is independent of the MVP and still pending.
- Not checked in this release: a manual walk-through of every screen on narrow widths, and applying scripts 40-42 on a database built from zero (the owner database already has them; the test database is rebuilt from all scripts by `scripts/build-test-db.ps1` and the backend suite ran against it).

## Deferred (spec section 3, non-goals)

- New integrations with Epic, GOG, Xbox, PlayStation (the store stays a hand-entered field).
- External AI for recommendations.
- Translating the UI (stays in English).
- New advanced statistics (Stats is only fixed and simplified).
- Mobile app, cross-device sync, cloud services.

## How to run

1. Apply `Database/01` to `42` in order in SSMS (see `README.md`).
2. Set the four secrets (`dotnet user-secrets` or environment variables).
3. `cd GamePlanner.API && dotnet run --launch-profile https`; `cd game-planner-ui && npm start`.
4. To use Workers: `UPDATE Users SET IsAdmin = 1 WHERE Id = <id>;` and sign in again.
5. Tests: the three commands in the table above.

## Final state

- Backend, frontend and e2e suites pass with the numbers above.
- Phase 8 is superseded by this MVP (PLAY-02 no longer applies) and Phase 9 is absorbed by Task 17; see `.planning/ROADMAP.md` and `.planning/REQUIREMENTS.md`.
- Ready for review; merge and deployment have not been done.
