---
phase: 05-regression-testing-full-stack-verification
verified: 2026-10-27T20:20:00Z
status: passed
score: 3/3 roadmap success criteria verified (SC3 with accepted vite residual); all plan must-haves met after re-verification on 2026-10-27
behavior_unverified: 0
overrides_applied: 0
re_verification: true
gaps:
  - truth: "05-02: each corrected Half-Life row has a non-NULL CoverImageUrl different from its pre-fix value after non-force GameSync + GameImageSync runs"
    status: resolved
    resolution: "2026-10-27: a non-force GameImageSyncWorker run at 20:08:31 (after IGDB re-stamped IGDBUpdatedAt on 2026-10-27) filled the covers: 107683 -> coc2ea.jpg, 107687 -> co1nmw.jpg (pre-fix co779u.jpg). No data workaround was applied (the attempted UPDATE matched 0 rows). The underlying SaveCoverAsync skip bug remains open as a todo."
    reason: "Rows 107683 and 107687 keep CoverImageUrl NULL. GameImageManager.SaveCoverAsync skips with 'Cover up-to-date' because GameSyncWorker re-stamps IGDBUpdatedAt first (igdbUpdatedAt <= currentUpdatedAt) even though the cover is NULL. Pre-existing bug, not introduced by Phase 5. Genres, release dates, ratings, screenshots and artworks did re-derive. Logged as todo; the user has not yet decided on the fix. It does not block ROADMAP success criteria 1-3 but it is an unmet plan must-have."
    artifacts:
      - path: "GamePlanner.API/Managers/GameImageManager.cs"
        issue: "SaveCoverAsync freshness skip applies even when CoverImageUrl is NULL (file also carries the user's uncommitted WIP; do not touch without the owner)"
      - path: ".planning/todos/pending/2026-10-24-cover-skipped-when-igdbupdatedat-set.md"
        issue: "Open; contains the data workaround (UPDATE Games SET IGDBUpdatedAt = NULL WHERE Id IN (107683,107687), then one non-force image run) and the app fix"
    missing:
      - "Owner decision: run the one-line data workaround (writes owner UserId 2 rows), OR accept the deviation via an override entry, OR schedule the SaveCoverAsync fix in a later milestone"
deferred: []
human_verification:
  - test: "H1 - Steam login (owner account). Open http://localhost:4200/login on the running stack, click 'Sign in with Steam', complete Steam sign-in with the owner's own account."
    expected: "Lands on an authenticated route. API log has no 'Steam callback rejected' warning for that attempt (if it does, the logged reason names the S4 rule to adjust; plan 05-04 assumption A4: openid.signed must contain op_endpoint, claimed_id, identity, return_to, response_nonce, and return_to must be echoed unchanged). SELECT COUNT(*) FROM Users WHERE SteamId='76561198115372977' returns 1 (Id 2, no duplicate). No API log line contains the Steam API key."
    why_human: "Needs the owner's real Steam account and a live Steam OpenID round trip. The fail-closed field checks added in 05-04 are only proven offline against stubs; a wrong signed-field assumption would lock the owner out of Steam login, so this live confirmation is essential."
  - test: "H2 - Steam library import (owner account). Signed in as the owner, open http://localhost:4200/steam and run a Steam library import."
    expected: "Newest ImportJobs row for UserId 2 (Id > 2) reaches Status='Completed' with ErrorMessage NULL. SELECT COUNT(*) FROM Games WHERE UserId=2 AND IsActive=1 is not lower than 481 (baseline). No API log line contains the Steam API key."
    why_human: "Needs the owner's Steam account; the automated UAT is forbidden from touching UserId 2 data by design (D-05/D-06). This is the only remaining piece of ROADMAP success criterion 2 (Steam sync workflow)."
---

# Phase 5: Regression Testing & Full-Stack Verification - Verification Report

**Phase Goal:** The hardening changes across frontend and backend are proven correct together, not just individually.
**Verified:** 2026-10-24
**Status:** passed (re-verified 2026-10-27: H1, H2 passed; Half-Life cover gap resolved)
**Re-verification:** No - initial verification

## Goal Achievement

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Automated tests exist and pass for auth (JWT lifetime/validation), GameManager atomicity, IgdbService, SteamService, SteamTokenStore cleanup | VERIFIED | Re-ran in this session: `dotnet test GamePlanner.Tests --no-build` -> 61 passed, 0 failed (the normal build is blocked by the running dev API PID 13380 locking the apphost; the test DLL was built 19:02, after the last source change at 18:52). `--list-tests` shows: AuthTokenTests (24h lifetime, valid 200, missing 401, 7-case rejection matrix expired / wrong-secret / wrong-issuer / wrong-audience / HS512 / alg-none / no-exp, hermetic host), GameManagerAtomicityTests (Update / Create / CreateFromIgdb rollback + stamp + idempotent delete), IgdbServiceContractTests (6), SteamServiceContractTests (8, incl. real-pipeline key-leak test), SteamTokenStoreSweepTests (4; `RemoveExpired` exists in SteamTokenStore.cs:29 and is called by Workers/SteamTokenCleanupWorker.cs:40), SteamAuthServiceTests (26 incl. cases). Frontend: `npm run test:ci` re-run -> 8/8 SUCCESS (interceptor 4, guard 2, game-list delete message 2). Tests drive the real JwtBearer middleware through `WebApplicationFactory<Program>`, not mocks of the validator. Production changes are confirmed in code: appsettings.json `System.Net.Http.HttpClient: Warning`, `AuthService.cs:71 if (!string.IsNullOrEmpty(user.Email))`, `SteamAuthService.cs` return_to ordinal compare (l.108) and line-exact `is_valid:true` (l.143), `public partial class Program { }` (Program.cs:148). |
| 2 | A full manual UAT pass through every major workflow (login, backlog CRUD, session tracking, journal, IGDB/Steam/HLTB sync) succeeds with upgraded Angular client + hardened backend together | PARTIAL - automated part VERIFIED, Steam owner part NEEDS HUMAN | `e2e/` (committed in 6ff2970, d3ab17d, 64698cd) holds a 16-test serial Playwright suite against the running stack as the dedicated UAT account only: login errors and success, unauth redirect, empty state, IGDB add, edit (via PUT, approved D-04 deviation: no edit screen exists), now-playing, journal, planner isolation, delete + 404, GameSync / GameImageSync / GameHLTBSync "Run now" asserted from `GET /api/worker` (Completed, lastError null, no force), SteamPlaytimeSyncWorker restart-initiated run (approved deviation: no run-now trigger exists). Assertions read as real (status codes, exact UI copy, per-id isolation checks, 404 after delete), not loosened. Owner guard in global-setup.ts (email must be uat-test@gameplanner.local, refuses token NameIdentifier 2), `.env` / `.auth` / `node_modules` git-ignored, no committed password. Two consecutive green runs are reported (2.9m, 3.0m); I did NOT re-run the suite: its worker steps trigger IGDB/HLTB syncs that can touch owner data on the shared dev DB, and the Playwright report timestamp (19:17) matches the SUMMARY. Not covered by anything automated: Steam login with a real Steam account (H1) and Steam library import (H2) - deliberately left to the owner, listed under human_verification. |
| 3 | Dependency scan shows no known-EOL or high-severity vulnerable packages remaining from the pre-hardening baseline | VERIFIED (with recorded, evidence-backed residual) | Re-ran both scans. `dotnet list GamePlanner.sln package --vulnerable --include-transitive`: only 2 Moderate (Microsoft.IdentityModel.JsonWebTokens and System.IdentityModel.Tokens.Jwt 7.0.3, GHSA-59j7-ghrg-fj52) in API and Tests; the two High test-project findings (System.Net.Http, System.Text.RegularExpressions 4.3.0) are gone. `npm audit` (npm 11.21.1) in game-planner-ui: 3 findings (1 high `vite` 8.3.0, 2 low dependents), 0 critical; baseline was 5 critical / 14 high. The `vite` residual is accepted in 05-DEPENDENCY-AUDIT.md and I independently confirmed the evidence: `angular.json` has only `@angular-devkit/build-angular:{browser,dev-server,extract-i18n,karma}` builders (webpack), `@angular/build@22.2.4` is the latest non-prerelease and pins vite exactly, the fix only exists in 22.3.0-next prereleases, no `overrides` block in package.json. Judged against the ROADMAP wording "remaining from the pre-hardening baseline", the baseline critical/high items are cleared in-range; vite arrived with the Angular 22 toolchain and is dev-tooling that the project's builders never start. Caveat for the user: read literally ("no high-severity ... remaining"), `npm audit` still prints one high; if the user wants a strict zero-high, that needs the prerelease Angular build chain (out of scope under D-07). Moderate NuGet findings are report-only by D-07; a one-line JwtBearer 8.0.0 -> 8.0.32 bump is available if desired. e2e/ package audited separately: 0 vulnerabilities. |

**Score:** SC1 verified, SC3 verified (accepted residual), SC2 partial (automated verified; Steam login / import pending the owner).

### Plan-level must-haves

| Plan | Must-have area | Status | Evidence |
|------|----------------|--------|----------|
| 05-01 | Hermetic in-process host, 24h token, 7-case rejection matrix, CreateFromIgdb rollback | VERIFIED | Test names above; 61/61 pass |
| 05-02 | Script 38 guarded, reversible, idempotent, user-applied, dup scan = 0, todos moved/logged | VERIFIED | `Database/38_FixHalfLifeIgdbId.sql` has CONVENTION EXEMPTION header, REVERSAL block, per-row `IF EXISTS` guards, COMMIT only in the user-run script; completed/pending todos present (halflife, portal, cover-skip) |
| 05-02 | Non-NULL CoverImageUrl on corrected rows after non-force workers | FAILED | See gap above (SUMMARY itself discloses this as "Acceptance not met") |
| 05-03 | Steam key no longer logged; IGDB/Steam contract facts | VERIFIED | Config key present; leak test through the real HttpClient pipeline in the 61 |
| 05-04 | Steam OpenID null-email crash, return_to binding, fail-closed, line-exact is_valid | VERIFIED offline; live confirmation pending (H1) | Code present and 26 tests pass. Assumption A4 (signed-field list, return_to echoed unchanged) cannot be proven offline |
| 05-05 | Karma specs + `test:ci` + in-range npm remediation + audit record | VERIFIED | 8/8 re-run; audit numbers reproduced |
| 05-06 | e2e suite, owner guard, ignores, e2e audit row, summary deviations recorded | VERIFIED (not re-run) | Files and config read; deviations recorded in SUMMARY; prohibitions (no owner data, no loosened assertions) hold per the spec read |

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| GamePlanner.Tests/{AuthTokenTests, GameManagerAtomicityTests, IgdbServiceContractTests, SteamServiceContractTests, SteamTokenStoreSweepTests, SteamAuthServiceTests}.cs + Support/* | VERIFIED | Exist, substantive, discovered and passing |
| game-planner-ui/src/app/{interceptors,guards,components/game-list}/*.spec.ts | VERIFIED | 8 specs run |
| e2e/{package.json, playwright.config.ts, global-setup.ts, tests/uat.spec.ts, .env.example} | VERIFIED | 6 files tracked; serial, system Chrome, ignoreHTTPSErrors |
| Database/38_FixHalfLifeIgdbId.sql | VERIFIED | Applied by user per SUMMARY |
| 05-DEPENDENCY-AUDIT.md | VERIFIED | Numbers reproduced |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| ApiFactory -> real Program pipeline | JwtBearer middleware | WIRED | Rejection tests assert 401 + `invalid_token` from the real middleware |
| SteamTokenCleanupWorker -> SteamTokenStore.RemoveExpired | tested sweep | WIRED | Worker line 40 calls the method under test |
| AuthController.SteamCallbackUrl -> SteamAuthService.expectedReturnTo | return_to verification | WIRED | Single URL source per 05-04, `HandleCallbackAsync(Request.Query, SteamCallbackUrl)` |
| e2e global-setup -> storageState | gp_token for the UI | WIRED | Login via API, token written for origin :4200 |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Backend suite | `dotnet test GamePlanner.Tests --no-build` | 61 passed / 0 failed | PASS |
| Frontend suite | `npm run test:ci` | 8 of 8 SUCCESS | PASS |
| NuGet scan | `dotnet list GamePlanner.sln package --vulnerable --include-transitive` | 2 Moderate per project, no High/Critical | PASS |
| npm scan | `npx npm@11.21.1 audit` | 1 high (vite), 2 low, 0 critical | PASS with accepted residual |
| e2e suite | not re-run | n/a | SKIPPED (mutates shared dev DB, triggers workers; two green runs reported) |

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| TEST-01 | 05-01, 05-03, 05-04, 05-05 | Targeted tests for auth, GameManager, IgdbService, SteamService, SteamTokenStore | SATISFIED | 61 xUnit + 8 Karma tests pass (REQUIREMENTS.md already marks it [x]) |
| TEST-02 | 05-02, 05-05, 05-06 | Full-stack UAT validating the combined frontend+backend contract | NEEDS HUMAN | Automated UAT green; Steam login and import (H1/H2) outstanding. REQUIREMENTS.md still Pending, correct for now |

All requirement IDs found in the six PLAN frontmatters (TEST-01, TEST-02) exist in REQUIREMENTS.md, and the traceability table maps only TEST-01/TEST-02 to Phase 5, so there are no orphaned requirements.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| e2e/tests/uat.spec.ts | 212 | Regex `[+-]dd:dd$` uses literal `dd` instead of `\d\d` | Warning | Offset-bearing timestamps would hit NaN (the Steam playtime check would fail, not falsely pass). Current API timestamps have no offset so it works |
| various (05-REVIEW.md) | - | 10 advisory warnings, 0 critical | Info | Advisory, not re-triaged here |
| TBD / FIXME / XXX scan over phase-touched files | - | none found | - | Debt-marker gate clear |

### Human Verification Required

1. **H1 - Steam login (owner).** See frontmatter. Most important item: it is the only live proof that the new fail-closed OpenID checks (return_to exact match, required signed fields) do not reject the real Steam response.
2. **H2 - Steam library import (owner).** See frontmatter.

### Gaps Summary

One gap, narrow and not tied to any ROADMAP success criterion: 05-02's must-have that the corrected Half-Life rows get a non-NULL cover is unmet because of a pre-existing `GameImageManager.SaveCoverAsync` skip bug. It is honestly disclosed in the SUMMARY and logged as a todo; resolution needs the owner's decision (run the one-line data workaround against UserId 2 rows, accept as an override, or fix the app later). If the user accepts the deviation, add an `overrides:` entry and the status becomes `human_needed` (H1/H2 only).

Nothing blocks proceeding in terms of code quality: tests, build and dependency state are solid. Phase completion should wait on H1/H2 and the cover decision; REQUIREMENTS.md TEST-02 should be ticked only after H1/H2 pass.

---

_Verified: 2026-10-24_
_Verifier: Claude (gsd-verifier)_

## Re-verification (2026-10-27)

All open items closed; status moved from `gaps_found` to `passed`.

| Item | Result | Evidence |
|------|--------|----------|
| H1 - Steam login (owner) | PASSED | Owner signed in with Steam on the running stack (API built 2026-10-27 17:07 from the current tree, including the 05-04 checks). `SELECT COUNT(*) FROM Users WHERE SteamId='76561198115372977'` = 1; `Users WHERE Email IS NULL` = 0 (no duplicate user). The successful login shows the fail-closed checks accept a real Steam assertion, so assumption A4 holds. The API console of this process was not readable by Claude, so the "no key in logs" check rests on the 05-03 config fix and its pipeline test. |
| H2 - Steam library import (owner) | PASSED | UI showed "Import completed — 480 games processed successfully." Newest `ImportJobs` row for UserId 2: Id 4, `Completed`, `ErrorMessage` NULL. Active games for UserId 2: 485 (baseline 481). |
| 05-02 Half-Life covers | RESOLVED | See the `gaps` frontmatter entry: both rows have new non-NULL covers from a non-force image run. |

TEST-02 is satisfied: the automated Playwright UAT plus owner checks H1/H2 cover every major workflow.
