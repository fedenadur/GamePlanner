---
status: complete
phase: 05-regression-testing-full-stack-verification
source: [05-01-SUMMARY.md, 05-02-SUMMARY.md, 05-03-SUMMARY.md, 05-04-SUMMARY.md, 05-05-SUMMARY.md, 05-06-SUMMARY.md]
started: 2026-10-27T21:00:00Z
updated: 2026-10-27T21:20:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Cold Start Smoke Test
expected: API and Angular dev server boot from the current tree; GET /api/games returns 401 unauthenticated and http://localhost:4200 serves <app-root>.
result: pass
source: automated
evidence: "API (PID 31012, restarted 2026-10-27 for H1) -> 401; UI -> <app-root> present"

### 2. Backend regression suite
expected: dotnet test passes all JWT, GameManager atomicity, IGDB/Steam contract, SteamTokenStore and Steam OpenID tests.
result: pass
source: automated
evidence: "61 passed, 0 failed (built to an isolated output dir because the running API and Visual Studio lock bin/)"

### 3. Frontend specs
expected: npm run test:ci runs AuthInterceptor, AuthGuard and game-list delete-failure specs headless, all green.
result: pass
source: automated
evidence: "Chrome Headless 156: 8 of 8 SUCCESS"

### 4. Steam API key never logged
expected: Logging:LogLevel:System.Net.Http.HttpClient is Warning; real-pipeline leak test passes.
result: pass
source: automated
evidence: "appsettings.json line 6; leak test included in the 61"

### 5. Half-Life / Half-Life 2 IGDBId correction
expected: Rows 107683/107687 hold IGDBId 231/233 with covers; UserId 2 duplicate-title scan returns 0 groups.
result: pass
source: automated
evidence: "107683|Half-Life|231|cover; 107687|Half-Life 2|233|cover; dupgroups=0"

### 6. Full-stack Playwright UAT (UAT account)
expected: Login, error copies, IGDB add, edit, now-playing, journal, planner isolation, delete + 404, three Run-now workers and Steam playtime worker all pass.
result: pass
source: automated
evidence: "16 passed (3.1m), exit 0"

### 7. Steam login with owner account (H1)
expected: Owner signs in with Steam; no 'Steam callback rejected' warning; exactly one Users row for the owner's SteamId.
result: pass
source: owner, 2026-10-27 (recorded in 05-VERIFICATION.md re-verification)

### 8. Steam library import with owner account (H2)
expected: Newest ImportJobs row Completed with no error; owner active games not below 481.
result: pass
source: owner, 2026-10-27 (ImportJobs Id 4 Completed; 485 active games)

### 9. Dependency scan
expected: No critical or high findings from the pre-hardening baseline remain.
result: pass
source: automated
evidence: "npm audit: 1 high (vite, accepted in 05-DEPENDENCY-AUDIT.md) + 2 low; NuGet: 2 moderate IdentityModel 7.0.3 (report-only per D-07)"

## Summary

total: 9
passed: 9
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

[none]
