---
phase: 05-regression-testing-full-stack-verification
plan: 06
subsystem: testing
tags: [playwright, e2e, uat, chrome, worker-api, rate-limit, owner-guard]
status: complete
requirements: [TEST-02]

requires:
  - phase: 05-regression-testing-full-stack-verification
    provides: "05-02 Half-Life fix, 05-04 Steam OpenID hardening, 05-05 Angular 22.2.3 client and test:ci"
provides:
  - "e2e/: isolated Playwright package (@playwright/test 1.64.1 exact) with a committed, re-runnable 16-test serial UAT"
  - "global-setup owner guard: refuses any email but uat-test@gameplanner.local and any token for UserId 2"
  - "Full-stack proof on the restarted merged stack: login, games CRUD, now-playing, journal, planner isolation, 3 triggered workers, Steam playtime worker"
  - "e2e row in 05-DEPENDENCY-AUDIT.md (0 vulnerabilities)"
affects: [phase-05-verification, phase-05-human-uat]

tech-stack:
  added: ["@playwright/test 1.64.1 (e2e/ only, devDependency)"]
  patterns: ["system Chrome (channel: 'chrome') + ignoreHTTPSErrors instead of the Phase 4 HTTP-to-HTTPS proxy", "worker outcomes asserted from GET /api/worker JSON, never from page timestamp text", "single API login in globalSetup written to storageState (gp_token)"]

key-files:
  created:
    - e2e/package.json
    - e2e/package-lock.json
    - e2e/playwright.config.ts
    - e2e/global-setup.ts
    - e2e/.env.example
    - e2e/tests/uat.spec.ts
  modified:
    - .gitignore
    - .planning/phases/05-regression-testing-full-stack-verification/05-DEPENDENCY-AUDIT.md

key-decisions:
  - "Package gate (Task 1): user approved installing exactly @playwright/test@1.64.1"
  - "Steam playtime trigger (Task 2): accept-restart, the API restart is the trigger (recorded D-04 deviation)"
  - "Edit step drives PUT /api/games/{id} (user-approved D-04 deviation, 2026-10-24)"

metrics:
  tasks: 5 (2 resolved checkpoints + 3 executed)
  commits: [6ff2970, d3ab17d, 64698cd]
  completed: 2026-10-24
---

# Phase 5 Plan 6: Full-stack Playwright UAT Summary

A committed 16-test serial Playwright suite (system Chrome, `ignoreHTTPSErrors`) now drives the restarted, merged stack as the dedicated test account only and passes twice in a row: login and error copies, games CRUD via IGDB, now-playing, journal, session-planner isolation, the three triggerable sync workers and the Steam playtime worker.

## Tasks

| Task | Name | Commit |
|------|------|--------|
| 1 | Package legitimacy gate for `@playwright/test` 1.64.1 | resolved by user before execution (no commit) |
| 2 | Steam playtime trigger decision | resolved by user before execution (no commit) |
| 3 (tracer) | Restart dev stack, scaffold `e2e/`, auth slice | 6ff2970 |
| 4 | Games CRUD (IGDB add, API edit, delete), now-playing, journal | d3ab17d |
| 5 | Planner isolation, three worker runs, Steam playtime, e2e audit row | 64698cd |

## Package gate (Task 1)

The user approved installing exactly `@playwright/test@1.64.1` ("parece legitimo"). Evidence gathered by the orchestrator: `npm view` shows repository github.com/microsoft/playwright, maintainers include microsoft-oss-releases, microsoft1es, dgozman-ms, pavelfeldman and yurys, the version is not deprecated, and there is no postinstall script. The install added 3 packages and `npm audit` reports 0 vulnerabilities. No `playwright install` was run (system Chrome).

## Stack restart (Task 3)

- Dev API and `ng serve` were both stopped on entry (port scan: nothing listening on 4200, 7174, 5127).
- `UAT_API_STARTED_AT` = `2026-10-24T22:07:32Z`.
- API: `cd GamePlanner.API && dotnet run --launch-profile https` (Bash background). UI: `cd game-planner-ui && npm start` (Bash background). The earlier exit-127 failure did not recur from the Bash tool.
- Readiness: `curl -sk https://localhost:7174/api/games` returned `401`; `curl -s http://localhost:4200` returned HTML containing `<app-root`.
- `npm ci` in `game-planner-ui` was skipped: `node_modules` had been installed from the regenerated lockfile in 05-05 and verified there (build, `test:ci` 8/8, boot smoke). The user's WIP (`GameImageManager.cs`, `game-detail.component.*`) ran as-is and was never staged.
- Owner guard proven: `UAT_EMAIL=someone-else@example.com npx playwright test` exits 1 in global setup with `Refusing to run UAT: UAT_EMAIL must be uat-test@gameplanner.local`. Shell variables take precedence over `.env` (`process.loadEnvFile` does not override), so the flagged dotenv-precedence assumption holds.
- Re-run setup (gitignored `e2e/.env` does not travel): copy `e2e/.env.example` to `e2e/.env`, fill `UAT_PASSWORD`, then `cd e2e && npm ci && npx playwright test`.

## D-04 deviation: game edit via API

Approved by the user during plan-phase on 2026-10-24. Reason: the Angular app has no game-edit screen (no UI caller of `PUT /api/games/{id}`), and building one would be out-of-scope feature work in a hardening-only milestone that also breaks the UI Visual Change Lock. What the UAT does instead: renames the game to `uat-<timestamp>` through `PUT /api/games/{id}` with the test account's token, then reloads `/games`, filters by the new name and asserts the card. No execution-time checkpoint was needed.

## D-04 deviation: Steam playtime trigger

- Selected option: `accept-restart`.
- Decision date: 2026-10-24 (user, at the Task 2 decision; "levanta el api y el front y probalo con playwright").
- Reason: `SteamPlaytimeSyncWorker` has no run-now trigger (`WorkerController.KnownWorkers` lists only GameSync, GameImageSync and GameHLTBSync, so `/workers` shows no `Run now` for it). Adding one is new API surface and feature work in a hardening-only milestone. Every worker runs once at API start, so the restart's startup run stands in for the trigger.
- Verified against the `UAT_API_STARTED_AT` = `2026-10-24T22:07:32Z` run: `GET /api/worker` row `SteamPlaytimeSyncWorker` was `Completed`, `lastError` null, `lastRunAt` `2026-10-24T22:07:36.85Z` (not earlier than the restart). The test title carries `accepted D-04 deviation`.
- Limitation: a later re-run without an API restart checks the most recent startup run (24-hour window), not a fresh one.

## Verification

First full run (`cd e2e && npx playwright test`, exit 0):

```
  ok  1 auth › unauthenticated visit to /games redirects to /login (591ms)
  ok  2 auth › steam failure redirect shows the fixed copy (491ms)
  ok  3 auth › wrong password shows the fixed copy (720ms)
  ok  4 auth › valid credentials land on the authenticated route (839ms)
  ok  5 workflows › clear leftovers from earlier failed runs (34ms)
  ok  6 workflows › empty state: a filter matching nothing shows the empty copy (706ms)
  ok  7 workflows › add a game through IGDB search (1.9s)
  ok  8 workflows › edit the game through PUT /api/games/{id} and see the new name (757ms)
  ok  9 workflows › now playing: place the game in a slot and remove it (1.1s)
  ok 10 workflows › journal: add an entry with notes and delete it (1.0s)
  ok 11 workflows › session planner returns only games from the UAT library (81ms)
  ok 12 workflows › delete the game from its card; the API then returns 404 (940ms)
  ok 13 workers › GameSyncWorker: Run now completes without error (2.2m)
  ok 14 workers › GameImageSyncWorker: Run now completes without error (18.8s)
  ok 15 workers › GameHLTBSyncWorker: Run now completes without error (14.8s)
  ok 16 workers › SteamPlaytimeSyncWorker restart-initiated run (accepted D-04 deviation, 05-06 Task 2) (23ms)

  16 passed (2.9m)
```

Second run (re-runnability, idempotent `uat-<timestamp>` naming): 16 passed (3.0m), same steps, exit 0.

- The delete test used the card's `.delete-btn` (no `FINDING` line was emitted), so the flagged delete-button-visibility assumption held: IGDB-added games default to `Manual` source. After the delete `GET /api/games/{id}` returned 404.
- `npm audit` for `e2e/`: 0 vulnerabilities (recorded in `05-DEPENDENCY-AUDIT.md`).
- Ignore gates: `git check-ignore` confirms `e2e/.env`, `e2e/.auth/uat.json`, `e2e/node_modules`; `git grep 'UAT_PASSWORD=.+' -- e2e` finds nothing; gitleaks pre-commit reported no leaks on all three commits.
- No `.html`, `.css` or component file under `game-planner-ui/src` is modified by this plan's commits; the spec has no `data-testid` and never references the force-run button; no `force=true` request was sent (asserted per worker test).
- Mutations per run: well under the 30 budget (add, edit, now-playing set/remove, journal save/delete, plan call, delete, three worker runs).

## Deviations from Plan

**1. [Environment] `npm ci` in `game-planner-ui` skipped**
- `node_modules` already matched the lockfile regenerated in 05-05 and had been verified there; the dev server compiled and served `<app-root` from it.

**2. [Rule 1 - Bug, test code] First-written spec edits fixed in flight**
- The journal "delete" control is labelled `Remove` in the template (class `btn-delete`), so the spec selects `.edit-footer .btn-delete` rather than text `Delete`. A broken string literal introduced while adding the stderr finding line was corrected before any run.

Otherwise the plan was executed as written. The leftover-cleanup test (`clear leftovers from earlier failed runs`) was added beyond the plan so a failed earlier run cannot make `from-igdb` return a stale active game; it only deletes UAT-user rows named `uat-*` or `Celeste`.

## Owner-only human checks (end-of-phase UAT, D-06)

Baselines recorded just before hand-off (SQL, 2026-10-24): UserId 2 active games = 481; Users with `SteamId = '76561198115372977'` = 1; newest `ImportJobs` row for UserId 2 is Id 2, `Completed`, `ErrorMessage` NULL.

**H1 (D-06, D-09 live re-test) - owner action:** open http://localhost:4200/login on the running stack, click "Sign in with Steam", complete Steam sign-in with the owner's own account.
Claude then verifies:
- The API console/log shows no `Steam callback rejected` warning for that attempt. If it does, the logged reason names the S4 rule to adjust (plan 05-04 assumption A4).
- `SELECT COUNT(*) FROM Users WHERE SteamId = '76561198115372977'` returns 1 (Id 2, no duplicate).
- No API log line contains the Steam API key.

**H2 (D-06) - owner action:** signed in as the owner, open http://localhost:4200/steam and run a Steam library import.
Claude then verifies via SQL:
- The newest `ImportJobs` row for UserId 2 (Id greater than 2) reaches `Status = 'Completed'` with `ErrorMessage` NULL.
- `SELECT COUNT(*) FROM Games WHERE UserId = 2 AND IsActive = 1` is not lower than 481.
- No API log line contains the Steam API key.

## Running stack (left up)

- API (`dotnet run --launch-profile https`): listening on 7174 and 5127, PID 13380.
- Angular dev server (`npm start`): listening on 4200, PID 12932.

## Known open items (not for this plan)

- Half-Life rows 107683 / 107687 keep `CoverImageUrl` NULL due to a pre-existing cover-skip bug (todo `2026-10-24-cover-skipped-when-igdbupdatedat-set.md`); not touched.
- TEST-01 / TEST-02 are not marked complete in REQUIREMENTS.md; phase completion handles it.

## Known Stubs

None.

## Threat Flags

None. T-05-28..T-05-32 and T-05-SC mitigated as planned (password only in gitignored `e2e/.env`, `.auth`/report/test-results ignored, owner guard proven by the wrong-email run, non-force worker runs only, one API login per run).

## Self-Check: PASSED

- FOUND: e2e/package.json, e2e/package-lock.json, e2e/playwright.config.ts, e2e/global-setup.ts, e2e/.env.example, e2e/tests/uat.spec.ts
- FOUND commits: 6ff2970, d3ab17d, 64698cd
