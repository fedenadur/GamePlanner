---
phase: 05
slug: regression-testing-full-stack-verification
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-10-27
---

# Phase 05 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| test process -> developer secret store | In-process test host must not load the owner's user-secrets | IGDB/Steam/JWT secrets (high) |
| test process -> dev SQL Server | Valid-token and rollback facts touch the shared dev database | Probe rows (low) |
| client -> JwtBearer middleware | Untrusted bearer tokens; tests drive forged tokens through it | JWTs (high) |
| executor -> live dev database (owner rows) | One-off data correction (script 38) on the owner's rows | Game rows (medium) |
| executor -> dev API as UAT user | UAT credentials cross the shell | Password, JWT (high) |
| IGDB search results -> script literals | Third-party data becomes values written to owner rows | IGDB IDs (medium) |
| API -> Steam Web API | API key in query string by provider design | Steam API key (high) |
| API -> Twitch/IGDB | Client secret in form POST body; access token in header | IGDB secret/token (high) |
| API -> log sinks | Anything logged is readable by log readers | Log text (medium) |
| browser -> `/api/auth/steam/callback` | Attacker-controllable `openid.*` query parameters | OpenID assertion (high) |
| API -> Steam OpenID (`check_authentication`) | Only party that can verify the signature | Assertion echo (high) |
| callback -> SPA (`?code=`) -> `/steam/exchange` | Single-use 5-minute opaque code; bearer token only in POST body | One-time code, JWT (high) |
| npm registry / NuGet feed -> developer machine | Packages execute during install/build/test | Package code (high) |
| shipped SPA bundle -> browser | `@angular/*` runtime code runs in the user's browser | SPA code (high) |
| test artifacts -> git | storageState, traces and reports can capture the JWT | JWT (medium) |
| UAT run -> shared dev database | Owner data lives in the same database as UAT data | Owner rows (high) |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-05-01 | Information disclosure | `ApiFactory` host configuration | high | mitigate | `GamePlanner.Tests/Support/ApiFactory.cs:32` `UseEnvironment("Testing")`; `AuthTokenTests.cs:138` `TestHost_IsHermetic_NoWorkersNoUserSecrets` | closed |
| T-05-02 | Tampering | Hosted workers in test host | medium | mitigate | `ApiFactory.cs:46` removes services in namespace `GamePlanner.API.Workers`; asserted by the hermeticity fact | closed |
| T-05-03 | Spoofing | JwtBearer `TokenValidationParameters` | high | mitigate | `AuthTokenTests.cs:96-134` seven-case matrix (expired, wrong-secret, wrong-issuer, wrong-audience, hs512, alg-none, no-exp) asserts 401 + `invalid_token` | closed |
| T-05-04 | Elevation of privilege | `public partial class Program` | low | accept | `Program.cs:148` declaration only — see AR-05-01 | closed |
| T-05-05 | Tampering | Probe rows in dev DB | low | mitigate | `GameManagerAtomicityTests.cs:19` `gsd-probe-` prefix; `finally` cleanup in every fact | closed |
| T-05-SC (05-01) | Tampering | NuGet `Microsoft.AspNetCore.Mvc.Testing` 8.0.32, `xunit` 2.9.3 | low | mitigate | Exact versions pinned in `GamePlanner.Tests.csproj:14,16`; `05-DEPENDENCY-AUDIT.md` NuGet scan: no High/Critical | closed |
| T-05-06 | Tampering | `Database/38_FixHalfLifeIgdbId.sql` on owner rows | medium | mitigate | `IF EXISTS` guards on (Id, UserId, old IGDBId) lines 73/95; `XACT_ABORT` + single transaction lines 67-116; `-- REVERSAL:` block line 35; rolled-back dry run and user-applied in SSMS (05-02-SUMMARY) | closed |
| T-05-07 | Tampering | Wrong IGDB match written as correct | medium | mitigate | 05-02-SUMMARY: exact title + original year; user approved each ID at the checkpoint (Half-Life 231 confirmed out-of-band, documented deviation) | closed |
| T-05-08 | Information disclosure | UAT password / JWT in shell | low | mitigate | `git grep 'UAT_PASSWORD=.+'` and JWT-pattern grep over tracked files return nothing | closed |
| T-05-09 | Denial of service | Re-enrichment against all users' games | low | accept | See AR-05-02 | closed |
| T-05-10 | Information disclosure | HttpClientFactory logging of Steam URLs | high | mitigate | `appsettings.json:6` `System.Net.Http.HttpClient: Warning`; `SteamServiceContractTests.cs:46` real-pipeline fact (red-then-green in 05-03-SUMMARY) | closed |
| T-05-11 | Information disclosure | IGDB secret/token in URL, headers, logs | high | mitigate | `IgdbServiceContractTests.cs:72` `ClientSecret_AppearsInNoRequestUriAndNoHeader`; `:113` `Query500_..._WithoutCredentials` | closed |
| T-05-12 | Information disclosure | Steam key in exception text | medium | mitigate | `SteamServiceContractTests.cs:108,122,160` `..._WithoutLeakingKey` facts | closed |
| T-05-13 | Repudiation | HttpClient warnings hidden by filter | low | accept | See AR-05-03 | closed |
| T-05-14 | Denial of service | External-API failures swallowed | medium | mitigate | `IgdbServiceContractTests.cs:100,113` and `SteamServiceContractTests.cs:108,122,137` assert `HttpRequestException` / `InvalidOperationException` surface | closed |
| T-05-15 | Spoofing | `openid.return_to` not verified | medium | mitigate | `SteamAuthServiceTests.cs:258,262` foreign / case-differing return_to rejected without contacting Steam | closed |
| T-05-16 | Spoofing | Unsigned SteamId fields; substring `is_valid` | low | mitigate | `SteamAuthService.cs:21-22` `RequiredSignedFields`, `:104` op_endpoint check, `:143` line-exact `is_valid:true`; rejection matrix `SteamAuthServiceTests.cs:239-317` | closed |
| T-05-17 | Denial of service | First-time Steam login crash (null email) | high | mitigate | `AuthService.cs:71-72` email claim only when present; `SteamAuthServiceTests.cs:79` end-to-end callback -> code -> exchange fact | closed |
| T-05-18 | Denial of service | Steam outage -> raw 500 / summary blocks login | medium | mitigate | `SteamAuthService.cs:55-57,147-149` fail-closed / degrade; `AuthController.cs:66` `steam_failed` redirect; facts `SteamAuthServiceTests.cs:213,224,343,371,384` | closed |
| T-05-19 | Spoofing | Login CSRF, no state binding (S5) | low | accept | See AR-05-04 | closed |
| T-05-20 | Denial of service | No rate limit on callback/exchange (S6) | low | accept | See AR-05-05 | closed |
| T-05-21 | Tampering | Duplicate `Users` rows on double submit (S7) | low | accept | See AR-05-06 | closed |
| T-05-22 | Information disclosure | Rejection logs echoing `openid.*` secrets | low | mitigate | `SteamAuthService.cs:117,158` fixed reason strings, only `openid.signed` list logged; `SteamAuthServiceTests.cs:325` asserts no sig/assoc_handle/nonce in logs | closed |
| T-05-23 | Elevation of privilege | Build-tooling RCE gadgets | critical | mitigate | `05-DEPENDENCY-AUDIT.md:20` critical 5 -> 0 after in-range bump with npm 11.21.1 | closed |
| T-05-24 | Denial of service | `@angular/router` 22.0.0-22.2.0 advisories | high | mitigate | `package.json:21` `^22.2.3`; `npm ls` resolves `@angular/router@22.2.3` | closed |
| T-05-25 | Elevation of privilege | Residual `vite` 8.3.0 under `@angular/build` | low | accept | See AR-05-07 | closed |
| T-05-26 | Tampering | npm via `npx -y npm@11.21.1` | low | mitigate | Exact version pinned (`05-DEPENDENCY-AUDIT.md:10`); lockfile integrity hashes; post-install audit | closed |
| T-05-27 | Repudiation | Audit record hiding a residual finding | medium | mitigate | `05-DEPENDENCY-AUDIT.md:41-61` lists the residual high (`vite`) and both lows with evidence and revisit trigger | closed |
| T-05-SC (05-05) | Tampering | npm installs of existing `@angular/*` packages | medium | mitigate | No new package names; no `overrides` / legacy-peer config in `package.json`; post-install audit gate (`05-DEPENDENCY-AUDIT.md:14`) | closed |
| T-05-28 | Information disclosure | UAT password in committed files | high | mitigate | `e2e/.env.example` has empty `UAT_PASSWORD=`; `e2e/.env` ignored (`.gitignore:18`); `git grep 'UAT_PASSWORD=.+'` empty; gitleaks pre-commit hook installed | closed |
| T-05-29 | Information disclosure | JWT in `.auth/uat.json`, traces, report | medium | mitigate | `.gitignore:44-46` ignores `e2e/.auth/`, `e2e/playwright-report/`, `e2e/test-results/` (confirmed by `git check-ignore`); none tracked | closed |
| T-05-30 | Spoofing / Elevation of privilege | UAT run as the owner account | high | mitigate | `e2e/global-setup.ts:4-6,21,39` refuses any email but `uat-test@gameplanner.local` and any token with NameIdentifier `2`; wrong-email refusal proven in 05-06-SUMMARY | closed |
| T-05-31 | Tampering | UAT mutating owner data / forcing re-sync | medium | mitigate | `e2e/tests/uat.spec.ts:9,72` `uat-<ts>` names as UserId 17; `:242` asserts no `force=true` requests | closed |
| T-05-32 | Denial of service | Rate limiters tripped by the suite | low | mitigate | Single API login in `global-setup.ts:28`; UI logins limited in `uat.spec.ts` | closed |
| T-05-SC (05-06) | Tampering | npm install of `@playwright/test` | high | mitigate | Human legitimacy checkpoint (05-06 Task 1); exact pin `e2e/package.json:9` `1.64.1`; no `playwright install` script; `e2e/` audit recorded (`05-DEPENDENCY-AUDIT.md:101-105`) | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-05-01 | T-05-04 | `public partial class Program` is a declaration only; no route, registration or runtime behavior change | Plan 05-01 threat model | 2026-10-24 |
| AR-05-02 | T-05-09 | Non-force enrichment only (Phase 4 UAT precedent); processes rows already missing metadata | Plan 05-02 threat model | 2026-10-24 |
| AR-05-03 | T-05-13 | Filter is at Warning, so HttpClient retries/failures still log; only per-request Information lines are suppressed | Plan 05-03 threat model | 2026-10-24 |
| AR-05-04 | T-05-19 | Single-user personal app; logged in `.planning/todos/pending/2026-10-24-steam-openid-followups.md` | Plan 05-04 threat model | 2026-10-24 |
| AR-05-05 | T-05-20 | Code is single-use with a 5-minute lifetime; logged in the Steam OpenID follow-up todo | Plan 05-04 threat model | 2026-10-24 |
| AR-05-06 | T-05-21 | No schema change in this phase; logged in the Steam OpenID follow-up todo | Plan 05-04 threat model | 2026-10-24 |
| AR-05-07 | T-05-25 | No `@angular/build:*` builder in `angular.json`; Vite dev server never started and nothing from `vite` ships; revisit trigger in `05-DEPENDENCY-AUDIT.md` | Plan 05-05 threat model | 2026-10-24 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-27 | 35 | 35 | 0 | /gsd-secure-phase (L1 grep verification; auditor skipped per short-circuit rule) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-27
