# Phase 5: Regression Testing & Full-Stack Verification - Context

**Gathered:** 2026-10-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Prove the hardening changes from Phases 1-4 are correct together, not just individually (TEST-01, TEST-02):

1. Targeted automated tests for every hardening-touched area: auth (JWT lifetime/validation), `GameManager` (atomicity — partly exists), `IgdbService`, `SteamService`, `SteamTokenStore` (cleanup — exists), plus a small set of Angular tests for hardening-touched frontend code.
2. A full UAT pass over every major user workflow with the upgraded Angular client and hardened backend running together, delivered as a re-runnable Playwright script.
3. A dependency vulnerability scan (`npm audit`, `dotnet list package --vulnerable`) with high/critical findings fixed or documented.

Two items were explicitly folded in at user request (see Folded Todos): the Half-Life/Half-Life 2 IGDBId data fix, and the Steam OpenID trust-boundary review that was still Active in PROJECT.md without a phase.

</domain>

<decisions>
## Implementation Decisions

### External API tests (IgdbService / SteamService)
- **D-01:** Tests run **fully offline with a stubbed `HttpMessageHandler`** injected through `IHttpClientFactory` — no real IGDB/Steam calls, no credentials needed, deterministic. Assert the Phase 2 hardening contract: credentials sent in headers/body and never in the request URL, failures are surfaced/thrown (not silently swallowed), and log output never contains secret values.

### Auth tests
- **D-02:** Cover **token issuance and validation only**: issued JWT lifetime is 24h, and the API rejects expired tokens, tokens with an invalid signature, and tokens with the wrong issuer/audience. Run against the API hosted **in-process inside the test** (e.g. `WebApplicationFactory`), never against the developer's running API. Rate-limit and CORS tests are **not** in scope (not chosen).

### Frontend tests
- **D-03:** Add **5-8 targeted Karma/Jasmine tests** (Angular currently has zero specs; toolchain already installed): `auth.interceptor.ts` attaches the token, `auth.guard.ts` redirects to `/login` without a token, and the game-list delete failure shows the fixed message `'Could not delete game. Please try again.'` with no server-derived text. No broader frontend coverage.

### Full-stack UAT
- **D-04:** UAT is a **Playwright script committed to the repo**, re-runnable on demand, covering: login, game CRUD (add via IGDB search, edit, delete), session tracking / now-playing, journal, and triggering the sync workers (IGDB, images, HLTB, Steam playtime) with results checked on `/workers`. — **Reversibility:** costly — adds a new dev dependency (`@playwright/test`) and test folder that later tooling/CI would build on.
  - **Approved deviation (2026-10-24, plan-phase):** the user accepted that "edit" runs through `PUT /api/games/{id}` with the test account's token, with the new name checked in the UI, because the app has no game-edit screen and building one would be out-of-scope feature work (recorded in 05-06).
- **D-05:** The script logs in as the dedicated test account `uat-test@gameplanner.local` (UserId 17), never the owner's account (UserId 2). Credentials must not be committed — read from environment variables / local config.
- **D-06:** Steps that need the owner's real Steam account (Steam login, Steam library import) are **done manually by the user**; Claude verifies the outcome via SQL and API logs. Everything else is automated.

### Dependency vulnerabilities
- **D-07:** **Fix high and critical** findings from `npm audit` and `dotnet list package --vulnerable` as long as the fix stays inside Angular 22's peer ranges. Anything that cannot be fixed without breaking the stack (e.g. Phase 1's `image-size`/`uuid`, which needed a `--force` downgrade of `@angular-devkit/build-angular`) is **documented as an accepted risk with its reason**. Moderate/low findings are reported, not fixed.

### Half-Life IGDBId data fix
- **D-08:** Look up the correct IGDBIds for "Half-Life" and "Half-Life 2", correct the wrong row (107683 or 107687) via a **new numbered SQL script** applied manually (project convention), and **scan all of the owner's active games for other pairs of different titles sharing one IGDBId**; fix any found the same way. — **Reversibility:** costly — data update on live personal rows; script should record the old values so it can be reversed.

### Steam OpenID trust-boundary review
- **D-09:** Review the Steam OpenID login flow (`SteamAuthService.HandleCallbackAsync` / `ValidateAndExtractSteamIdAsync`, `AuthController` Steam endpoints) for trust-boundary issues and add targeted tests for the callback validation (e.g. rejected `is_valid:false`, wrong `claimed_id` prefix, malformed SteamId). Fix concrete issues found; anything larger is logged as a todo, not built here.

### Claude's Discretion
- Test project layout: extend the existing `GamePlanner.Tests` xUnit project; choice of in-process host helper and HTTP stub style.
- Whether Playwright runs against the existing dev servers or starts them itself; handling of the untrusted dev HTTPS cert (e.g. `ignoreHTTPSErrors` in Playwright config instead of the ad-hoc proxy used in Phase 4 UAT).
- Exact UAT step list and assertions within the workflows listed in D-04.
- Order of plans (tests vs scan vs UAT vs data fix).

### Folded Todos
- **Half-Life / Half-Life 2 share one IGDBId under the same user** (`.planning/todos/pending/2026-08-31-halflife-igdbid-duplicate-same-user.md`, minor) — folded at explicit user request; handled by D-08. Not a security fix but a data-integrity cleanup discovered in Phase 4.
- **Steam OpenID trust boundary review** (PROJECT.md → Active, no roadmap phase) — folded at explicit user request; handled by D-09.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Scope & requirements
- `.planning/ROADMAP.md` §Phase 5 — goal and 3 success criteria
- `.planning/REQUIREMENTS.md` — TEST-01, TEST-02
- `.planning/PROJECT.md` — Active requirements (Steam OpenID review, targeted tests), constraints (hardening only, SPs for writes / EF LINQ for reads)

### Hardening already shipped (what the tests must lock in)
- `.planning/phases/02-credential-secrets-hardening/` — credential-in-header/redaction contract for `IgdbService`/`SteamService`
- `.planning/phases/03-auth-cors-rate-limiting-hardening/` — JWT 24h lifetime and `TokenValidationParameters`
- `.planning/phases/04-data-layer-reliability-audit-trail/04-CONTEXT.md` — D-05/D-07 atomicity, D-12..D-14 token sweep
- `.planning/phases/04-data-layer-reliability-audit-trail/04-UAT.md` — Phase 4 live checks (Playwright cert workaround, test user)
- `.planning/phases/01-angular-eol-upgrade/` — accepted `image-size`/`uuid` audit findings and why `--force` was declined

### Codebase maps
- `.planning/codebase/TESTING.md` — testing state and conventions
- `.planning/codebase/CONCERNS.md` — original audit findings

### Folded todo
- `.planning/todos/pending/2026-08-31-halflife-igdbid-duplicate-same-user.md`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `GamePlanner.Tests/` (xUnit, net8): `GameManagerAtomicityTests.cs` (5 facts, real dev DB, `gsd-probe-` rows cleaned in `finally`) and `SteamTokenStoreSweepTests.cs` (4 facts) — extend, don't duplicate.
- `IgdbService` / `SteamService` take `IHttpClientFactory` in their constructors — stub-friendly.
- `AuthService.GenerateToken(User)` is public — direct unit test target for lifetime.
- Angular: `src/app/interceptors/auth.interceptor.ts`, `src/app/guards/auth.guard.ts`, `components/game-list/game-list.component.ts` (`deleteGame` error callback); Karma/Jasmine already in `package.json` (`npm test` → `ng test`).
- Test account `uat-test@gameplanner.local` (UserId 17) exists in the dev DB for UI tests.

### Established Patterns
- Stored procedures only for insert/update/delete; EF LINQ for reads — the Half-Life fix goes in a numbered `Database/NN_*.sql` script applied manually in SSMS/sqlcmd.
- Ad-hoc `sqlcmd` batches against `Games` need `SET QUOTED_IDENTIFIER ON` (filtered indexes).
- Dev API: `https://localhost:7174` (http `5127` redirects to https); frontend `http://localhost:4200`. Playwright's Chromium rejects the dev cert.

### Integration Points
- `GamePlanner.API/Program.cs` — needs to be reachable by an in-process test host for D-02 (may require `public partial class Program`).
- `GamePlanner.API/Services/SteamAuthService.cs` + `Controllers/AuthController.cs` Steam endpoints — D-09 review target.
- `/api/worker/{name}/run` — used by the UAT script to trigger syncs.

</code_context>

<specifics>
## Specific Ideas

- User preference: Claude runs verification itself (Playwright, SQL, logs) and only hands the user steps that need their own credentials or Steam account.
- Phase 4 UAT showed `/workers` timestamps display UTC as local time (pre-existing display quirk) — the UAT script should not fail on that.

</specifics>

<deferred>
## Deferred Ideas

- Rate-limit and CORS automated tests — offered, not chosen for this phase.
- Fixing moderate/low dependency vulnerabilities — report only (D-07).
- `/workers` dashboard shows UTC timestamps as local time — pre-existing display bug, out of hardening scope.

</deferred>

---

*Phase: 05-regression-testing-full-stack-verification*
*Context gathered: 2026-10-24*
