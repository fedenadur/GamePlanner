# Phase 5: Regression Testing & Full-Stack Verification - Research

**Researched:** 2026-10-24
**Domain:** .NET 8 xUnit + in-process `WebApplicationFactory`, Angular 22 Karma/Jasmine, Playwright E2E against live dev servers, npm/NuGet vulnerability remediation, Steam OpenID 2.0 review, one-off SQL data fix
**Confidence:** HIGH for toolchain mechanics (most were executed against the real repo this session); MEDIUM for Steam/IGDB protocol details (training knowledge, tagged `[ASSUMED]`)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**External API tests (IgdbService / SteamService)**
- **D-01:** Tests run **fully offline with a stubbed `HttpMessageHandler`** injected through `IHttpClientFactory` — no real IGDB/Steam calls, no credentials needed, deterministic. Assert the Phase 2 hardening contract: credentials sent in headers/body and never in the request URL, failures are surfaced/thrown (not silently swallowed), and log output never contains secret values.

**Auth tests**
- **D-02:** Cover **token issuance and validation only**: issued JWT lifetime is 24h, and the API rejects expired tokens, tokens with an invalid signature, and tokens with the wrong issuer/audience. Run against the API hosted **in-process inside the test** (e.g. `WebApplicationFactory`), never against the developer's running API. Rate-limit and CORS tests are **not** in scope (not chosen).

**Frontend tests**
- **D-03:** Add **5-8 targeted Karma/Jasmine tests** (Angular currently has zero specs; toolchain already installed): `auth.interceptor.ts` attaches the token, `auth.guard.ts` redirects to `/login` without a token, and the game-list delete failure shows the fixed message `'Could not delete game. Please try again.'` with no server-derived text. No broader frontend coverage.

**Full-stack UAT**
- **D-04:** UAT is a **Playwright script committed to the repo**, re-runnable on demand, covering: login, game CRUD (add via IGDB search, edit, delete), session tracking / now-playing, journal, and triggering the sync workers (IGDB, images, HLTB, Steam playtime) with results checked on `/workers`. — **Reversibility:** costly — adds a new dev dependency (`@playwright/test`) and test folder that later tooling/CI would build on.
- **D-05:** The script logs in as the dedicated test account `uat-test@gameplanner.local` (UserId 17), never the owner's account (UserId 2). Credentials must not be committed — read from environment variables / local config.
- **D-06:** Steps that need the owner's real Steam account (Steam login, Steam library import) are **done manually by the user**; Claude verifies the outcome via SQL and API logs. Everything else is automated.

**Dependency vulnerabilities**
- **D-07:** **Fix high and critical** findings from `npm audit` and `dotnet list package --vulnerable` as long as the fix stays inside Angular 22's peer ranges. Anything that cannot be fixed without breaking the stack (e.g. Phase 1's `image-size`/`uuid`, which needed a `--force` downgrade of `@angular-devkit/build-angular`) is **documented as an accepted risk with its reason**. Moderate/low findings are reported, not fixed.

**Half-Life IGDBId data fix**
- **D-08:** Look up the correct IGDBIds for "Half-Life" and "Half-Life 2", correct the wrong row (107683 or 107687) via a **new numbered SQL script** applied manually (project convention), and **scan all of the owner's active games for other pairs of different titles sharing one IGDBId**; fix any found the same way. — **Reversibility:** costly — data update on live personal rows; script should record the old values so it can be reversed.

**Steam OpenID trust-boundary review**
- **D-09:** Review the Steam OpenID login flow (`SteamAuthService.HandleCallbackAsync` / `ValidateAndExtractSteamIdAsync`, `AuthController` Steam endpoints) for trust-boundary issues and add targeted tests for the callback validation (e.g. rejected `is_valid:false`, wrong `claimed_id` prefix, malformed SteamId). Fix concrete issues found; anything larger is logged as a todo, not built here.

### Claude's Discretion
- Test project layout: extend the existing `GamePlanner.Tests` xUnit project; choice of in-process host helper and HTTP stub style.
- Whether Playwright runs against the existing dev servers or starts them itself; handling of the untrusted dev HTTPS cert (e.g. `ignoreHTTPSErrors` in Playwright config instead of the ad-hoc proxy used in Phase 4 UAT).
- Exact UAT step list and assertions within the workflows listed in D-04.
- Order of plans (tests vs scan vs UAT vs data fix).

### Deferred Ideas (OUT OF SCOPE)
- Rate-limit and CORS automated tests — offered, not chosen for this phase.
- Fixing moderate/low dependency vulnerabilities — report only (D-07).
- `/workers` dashboard shows UTC timestamps as local time — pre-existing display bug, out of hardening scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TEST-01 | Targeted tests covering hardening-touched paths (auth, `GameManager`, `IgdbService`, `SteamService`, `SteamTokenStore`) | xUnit project already exists with 9 facts (GameManager atomicity x5, SteamTokenStore sweep x4). New work: `WebApplicationFactory` auth tests (D-02), stubbed-handler IGDB/Steam tests (D-01), Steam OpenID callback tests (D-09), 5-8 Karma specs (D-03). Mechanics proven this session. |
| TEST-02 | Full-stack UAT pass validating combined frontend+backend contract | Playwright `@playwright/test` 1.64.1 + `channel: 'chrome'` + `ignoreHTTPSErrors` verified working against the live dev servers (login page, cross-origin XHR to `https://localhost:7174`, route guard redirect). Plus dependency scan (D-07) and data fix (D-08) fold-ins. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

- **Stored procedures ONLY for Insert/Update/Delete; EF LINQ for reads.** The Half-Life fix is a one-off numbered script (`Database/38_*.sql`), not app code; app-side writes remain SPs. Reads in tests may use EF/Dapper directly (existing tests already do).
- **Services only for external APIs; logic belongs in Managers.** Any Steam OpenID fix stays in `SteamAuthService` (already a "service" that is really auth logic — do not move it in this phase).
- **No unnecessary abstractions / boilerplate.** Do not introduce a test base-class hierarchy, repository fakes, or a mocking framework: a ~20-line `RecordingHandler`, a ~20-line `CapturingLoggerProvider`, and one `ApiFactory` are sufficient.
- **Be concise; step-by-step; ask one short question if unclear. Clarity > Simplicity > Performance > Completeness.**
- `.claude/CLAUDE.md`: hardening only, no feature work; targeted tests only, not a full suite; respect SP/EF convention.
- DB migrations: next numbered file, applied manually. **Next free number is `38`** (last is `37_SeedWorkerStatus_SteamTokenCleanup.sql`). [VERIFIED: `ls Database`]
- Frontend: 2-space indent, single quotes in `.ts`, explicit types, `Observable<T>`.
- Secrets never committed; gitleaks pre-commit hook is installed (Phase 2). `.gitignore` already ignores `.env`, `.env.local`, `node_modules/`, `bin/`, `obj/`, `dist/`.
- **Working-tree hazard (carry-over from Phases 1-4):** `git status` shows unrelated uncommitted WIP in `GamePlanner.API/Managers/GameImageManager.cs` and `game-planner-ui/src/app/components/game-detail/game-detail.component.{ts,html,css}`. Plans must stage explicit paths only (never `git add -A`/`.`).

## Summary

Every mechanism the phase needs was exercised against the real repo during this research, so the plans can be prescriptive. **Angular:** `ng test` works unchanged with the installed `@angular-devkit/build-angular:karma` builder (deprecated-warning only) when invoked as `npx ng test --watch=false --browsers=ChromeHeadless` (Chrome 155 present; `karma.conf.js` defaults to non-headless `Chrome` + `singleRun:false`, so an npm script is needed). A throwaway probe of a class-based `AuthInterceptor` spec (via `HTTP_INTERCEPTORS` + `provideHttpClient(withInterceptorsFromDi())` + `provideHttpClientTesting()`) and an `AuthGuard` spec both passed under Angular 22.1/zone.js 0.15/jasmine-core 6.3; the probe was deleted and the tree is unchanged. `GameListComponent` has a single constructor dependency (`GameService`), so the delete-failure spec needs no TestBed rendering.

**Backend:** `Program.cs` uses top-level statements with no `public partial class Program`, registers seven `IHostedService` workers that start real DB/IGDB work immediately, and reads `Jwt:Secret` (blank in `appsettings.json`) lazily inside `AddJwtBearer`. An in-process `WebApplicationFactory<Program>` therefore needs: a one-line `public partial class Program { }`, `UseEnvironment("Testing")` (so real user-secrets are NOT loaded), `UseSetting("Jwt:Secret", <test constant>)`, and `RemoveAll<IHostedService>()`. `ClockSkew` is deliberately left at the 5-minute default (Phase 3 D-03), so an "expired token" test must expire the token by more than 5 minutes.

**Three real defects were found while researching and should be fixed as part of the phase (D-01 / D-09 "fix concrete issues found"):** (1) **The Steam API key is logged in clear text at Information level by HttpClientFactory** on every Steam call (`System.Net.Http.HttpClient.Default.LogicalHandler`/`.ClientHandler` log the full URL incl. `?key=`) — reproduced empirically on net8.0 with the API's own logging defaults; a one-line `"System.Net.Http.HttpClient": "Warning"` filter fixes it and a log-capture test locks it in. (2) **A first-time Steam login (new user) throws `ArgumentNullException`** because `AuthService.GenerateToken` does `new Claim(ClaimTypes.Email, user.Email)` and Steam-created users have `Email = null` (reproduced), after the user row is already persisted. (3) **`SteamAuthService` never verifies `openid.return_to`** (cross-relying-party assertion replay) and lets Steam/HTTP failures surface as unhandled 500s. The dependency scan found 29 npm advisories in the current lockfile (5 critical / 14 high) but regenerating the lockfile against `package.json` ranges bumped to `^22.2.3` leaves **3 (1 high, 2 low), all caused by one exactly-pinned `vite 8.3.0` inside `@angular/build@22.2.4`** — documented accepted risk. The .NET scan finds two High transitive packages in the **test** project only, cleared by `xunit` 2.5.3 -> 2.9.3.

**Primary recommendation:** Extend `GamePlanner.Tests` with `Microsoft.AspNetCore.Mvc.Testing` 8.0.32 and one `ApiFactory`; write all external-API tests against a recording `HttpMessageHandler` wired through the *real* HttpClientFactory pipeline (so the URL-logging leak is actually observable); put Playwright in an isolated `e2e/` folder with its own `package.json`, `channel: 'chrome'`, `ignoreHTTPSErrors: true`, serial workers, credentials from env vars, against the already-running dev servers; do the npm remediation via `package.json` bump to `^22.2.3` + lockfile regeneration with npm 11 (`npx -y npm@11`), because the installed global npm is 8.12.1 and `npm audit fix` crashes on it.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| JWT issuance (24h) | API / Backend (`AuthService.GenerateToken`) | — | Pure function of config + `User`; unit-testable without a DB (`db` unused in this method) |
| JWT validation (expiry/signature/iss/aud/alg) | API / Backend (JwtBearer middleware in `Program.cs`) | — | Must be tested through the real middleware pipeline (in-process host), not by re-implementing validation in the test |
| Token attach / 401 handling | Browser / Client (`AuthInterceptor`) | — | Angular interceptor; Karma unit test |
| Route protection | Browser / Client (`AuthGuard`) | API (real enforcement) | Client guard is UX only; API `[Authorize]` is the enforcement tier |
| Credential transport to IGDB/Steam | API / Backend (`IgdbService`/`SteamService`) | — | Outbound HTTP; test with stubbed handler |
| Log redaction of Steam key | API / Backend (logging config in `appsettings.json` + service catch blocks) | — | Leak vector is HttpClientFactory's built-in logging, i.e. configuration, not service code |
| Steam OpenID assertion verification | API / Backend (`SteamAuthService`) | External IdP (Steam `check_authentication`) | Relying party must verify `return_to`/signed fields itself; Steam only verifies the signature |
| Steam token hand-off | API (`SteamTokenStore`, `/steam/exchange`) | Browser (reads `?code=`) | Opaque one-time code in URL, bearer token only in POST response body |
| Worker triggering | API (`WorkerController`) | Browser (`/workers` page) | UAT drives it from the UI; state checked via `GET /api/worker` JSON (not the UTC-as-local display) |
| Half-Life data fix | Database / Storage (numbered SQL script) | — | One-off data repair; no app-code change |
| Dependency remediation | Build tooling (npm / NuGet) | — | `package.json` / `.csproj` / lockfile only |

## Standard Stack

### Core (backend tests — add to `GamePlanner.Tests.csproj`)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.AspNetCore.Mvc.Testing | 8.0.32 (matches the 8.0.32 EF/ASP.NET line already resolved) | `WebApplicationFactory<Program>` in-process host for D-02 / controller-level D-09 test | Microsoft's documented integration-test host; depends on `Microsoft.AspNetCore.TestHost` 8.0.32 + `Hosting` 8.0.1 `[CITED: nuget.org nuspec 8.0.32]` |
| xunit | **2.9.3** (bump from 2.5.3) | Test framework | 2.9.3 ships a `netstandard2.0` group, so on net8 it no longer pulls `NETStandard.Library 1.6.1` -> removes the two High advisories (see D-07 section) `[VERIFIED: nuspec dependency groups, nuget.org]` |
| Microsoft.NET.Test.Sdk | 17.14.1 (optional bump from 17.8.0) | Test host | Latest 17.x; 18.x exists but unnecessary `[ASSUMED]` compatible |
| xunit.runner.visualstudio | 3.1.5 (optional bump from 2.5.3) | `dotnet test` discovery | 3.x runs xUnit v2 tests; optional — only bump if 2.5.3 fails against xunit 2.9.3 `[ASSUMED]` |
| coverlet.collector | 6.0.0 (leave) | Already present | Not used in this phase |

No mocking framework (Moq/NSubstitute) and no `Microsoft.Extensions.Diagnostics.Testing` are needed: a hand-written `RecordingHandler : HttpMessageHandler` and a 15-line `ILoggerProvider` are less code than the dependency.

### Core (frontend tests — nothing to install)
| Library | Installed | Purpose |
|---------|-----------|---------|
| @angular/* | 22.1.4 (lockfile) | `TestBed`, `HttpTestingController` (`@angular/common/http/testing`) |
| jasmine-core / @types/jasmine | 6.3.0 / 6.0.0 (deliberately one major back, Phase 1) | Specs |
| karma / karma-chrome-launcher / karma-jasmine | 6.4.4 / 3.2.0 / 5.1.0 | Runner |
| zone.js | 0.15.1 | `src/test.ts` imports `zone.js/testing` (zone-based tests; do NOT introduce zoneless setup) |

### Core (UAT)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| @playwright/test | 1.64.1 (latest, published 2026-10-14) | E2E runner | Already the tool used via MCP in Phases 1/3/4; node `>=20` (Node 24.19 present) `[VERIFIED: npm registry]` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Isolated `e2e/` package | `@playwright/test` as `devDependency` of `game-planner-ui` | Co-locating puts Playwright (and future audit noise) into the Angular lockfile that D-07 is trying to get clean, and mixes Karma/Playwright config. Isolated folder is recommended; both satisfy D-04. |
| `dotenv` | `process.loadEnvFile('.env')` (Node >= 20.12; Node 24.19 present) | Avoids a dependency; Playwright does not auto-load `.env`. |
| Playwright `webServer` block | Pre-running dev servers | API needs user-secrets + SQL Server + a valid dev cert; Angular needs `ng serve`. Use existing servers + fail-fast preflight (see Pattern 6). |
| Playwright-managed Chromium (`playwright install chromium`) | `channel: 'chrome'` (system Chrome 155) | Installed Playwright browsers are revision 1243 but 1.64.1 wants 1248 -> would trigger a download. `channel: 'chrome'` worked with zero downloads `[VERIFIED: probe run]`. |

**Installation:**
```bash
# Backend (from repo root)
dotnet add GamePlanner.Tests package Microsoft.AspNetCore.Mvc.Testing --version 8.0.32
dotnet add GamePlanner.Tests package xunit --version 2.9.3

# E2E (new isolated folder)
mkdir e2e && cd e2e && npm init -y
npx -y npm@11 install -D @playwright/test@1.64.1
```

**Version verification (done this session):** `npm view @playwright/test version` -> 1.64.1; NuGet flat-container index confirms `Microsoft.AspNetCore.Mvc.Testing` 8.0.32 is the latest 8.x and `xunit` 2.9.3 is the latest 2.x. `[VERIFIED: registries]`

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| @playwright/test | npm | multi-year (latest 1.64.1 published 2026-10-14) | ~49.8M/wk | github.com/microsoft/playwright | SUS (seam reason: `too-new` — only the *latest release* is 10 days old; package is Microsoft's, `postinstall: null`) | Flagged — planner must add `checkpoint:human-verify` before install (suggest pinning the exact `1.64.1` and confirming the registry page) |
| Microsoft.AspNetCore.Mvc.Testing | NuGet | multi-year, first-party Microsoft | n/a | github.com/dotnet/aspnetcore | not checkable by seam (seam supports npm/pypi/crates only) — manually confirmed on nuget.org flat-container | Approved (first-party; version matches installed 8.0.32 line) |
| xunit 2.9.3 | NuGet | already in project (2.5.3) | n/a | github.com/xunit/xunit | already a project dependency; version bump only | Approved |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** `@playwright/test` (heuristic false-positive from release recency; still insert the human-verify checkpoint per protocol). A scratch install of 1.64.1 in an isolated directory reported `found 0 vulnerabilities`, 3 packages added, no install scripts executed beyond Playwright's own.

## Architecture Patterns

### System Architecture Diagram

```
                              dotnet test (GamePlanner.Tests)
 ┌──────────────────────────────────────────────────────────────────────────────┐
 │  [Unit, no host]                                                             │
 │   AuthService.GenerateToken ──► decode JWT ──► assert exp ≈ now+24h          │
 │   IgdbService / SteamService ──► RecordingHandler (stub) ──► assert          │
 │        url has no secret · header/body carries it · throws on 4xx/5xx        │
 │   SteamAuthService.HandleCallbackAsync ──► RecordingHandler("is_valid:...")  │
 │        ──► null for invalid / wrong prefix / bad SteamId / bad return_to      │
 │                                                                              │
 │  [In-process host: ApiFactory : WebApplicationFactory<Program>]              │
 │   env=Testing · Jwt:Secret=test const · IHostedService removed               │
 │   ConfigureHttpClientDefaults(primary handler = RecordingHandler)            │
 │   HttpClient ─► GET /api/games  (Bearer <token variants>) ─► JwtBearer       │
 │        valid 200 | expired>5min 401 | bad sig 401 | wrong iss/aud 401 |      │
 │        wrong alg 401 | alg=none 401                                          │
 │   Real logging pipeline ─► CapturingLoggerProvider ─► assert no ApiKey text   │
 │   GET /api/auth/steam/callback?bogus ─► 302 …/login?error=steam_failed       │
 │                                                                              │
 │  [Real dev SQL Server — existing pattern]                                    │
 │   GameManagerAtomicityTests (5) · Steam new-user success path (probe user)   │
 └──────────────────────────────────────────────────────────────────────────────┘

        npm run test:ci (game-planner-ui) ──► Karma ──► ChromeHeadless
           AuthInterceptor spec · AuthGuard spec · GameListComponent delete spec

        npx playwright test (e2e/)  ──►  Chrome(channel) ignoreHTTPSErrors
           preflight ─► UI http://localhost:4200 ─┐
                                                   ├─► API https://localhost:7174 ─► SQL Server
           globalSetup: API login (1x, 'auth' rate-limit 10/min) ─► storageState (localStorage gp_token)
           serial specs: dashboard/games CRUD(IGDB search) · now-playing · journal ·
                         /workers Run now ─► poll GET /api/worker JSON ─► assert Completed, LastError null
           manual (D-06): Steam login + Steam import by user; Claude checks SQL/API logs
```

### Recommended Project Structure
```
GamePlanner.API/
├── Program.cs                      # + `public partial class Program { }` (1 line, bottom)
├── appsettings.json                # + "System.Net.Http.HttpClient": "Warning" under Logging:LogLevel
GamePlanner.Tests/
├── GamePlanner.Tests.csproj        # + Mvc.Testing, xunit 2.9.3
├── GameManagerAtomicityTests.cs    # existing - leave
├── SteamTokenStoreSweepTests.cs    # existing - leave
├── Support/
│   ├── ApiFactory.cs               # WebApplicationFactory<Program> (env, secret, no workers, stub handler)
│   ├── RecordingHandler.cs         # HttpMessageHandler capturing method/uri/headers/body, scripted responses
│   ├── StubHttpClientFactory.cs    # IHttpClientFactory => new HttpClient(handler, disposeHandler:false)
│   └── CapturingLoggerProvider.cs  # collects formatted message + exception.ToString() (+ scopes)
├── AuthTokenTests.cs               # GenerateToken 24h + JwtBearer validation (D-02)
├── IgdbServiceContractTests.cs     # D-01
├── SteamServiceContractTests.cs    # D-01 (+ real-pipeline log-leak test)
└── SteamAuthServiceTests.cs        # D-09
game-planner-ui/
├── package.json                    # + "test:ci": "ng test --watch=false --browsers=ChromeHeadless"
└── src/app/{interceptors/auth.interceptor.spec.ts, guards/auth.guard.spec.ts, components/game-list/game-list.component.spec.ts}
e2e/                                # NEW isolated package
├── package.json · playwright.config.ts · .env.example (committed, empty) · .env (gitignored)
├── global-setup.ts                 # preflight + API login + storageState
└── tests/uat.spec.ts               # serial
Database/38_FixHalfLifeIgdbId.sql   # D-08
```

### Pattern 1: In-process API host (D-02)
**What:** `WebApplicationFactory<Program>` with hermetic config. **When:** any test needing the real middleware pipeline (JwtBearer, controller routing).
**Why each line matters (all verified by reading `Program.cs`):** `UseEnvironment("Testing")` avoids loading real user-secrets (only loaded when env == Development) and `appsettings.Development.json`; `Jwt:Secret` is blank in `appsettings.json` and `ValidIssuer/ValidAudience` (`GamePlannerAPI`/`GamePlannerClient`) come from it; `App:FrontendUrl` is read eagerly and is present in `appsettings.json` so startup does not throw; `RemoveAll<IHostedService>()` stops `GameSyncWorker` & co. from running real sync work against the dev DB and IGDB at host start.
```csharp
// Program.cs (bottom) -- required: top-level statements generate an internal Program class
public partial class Program { }

// GamePlanner.Tests/Support/ApiFactory.cs
// Source: https://learn.microsoft.com/aspnet/core/test/integration-tests (WebApplicationFactory), adapted
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Secret = "unit-test-only-secret-key-0123456789-abcdefghij"; // >= 32 bytes for HS256
    public const string Issuer = "GamePlannerAPI", Audience = "GamePlannerClient"; // from appsettings.json
    public RecordingHandler Http { get; } = new();                                  // stub for ALL outbound HttpClients
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Secret", Secret);
        builder.UseSetting("Steam:ApiKey", "TEST-STEAM-KEY-DO-NOT-LOG");
        builder.UseSetting("IGDB:ClientId", "test-igdb-id");
        builder.UseSetting("IGDB:ClientSecret", "TEST-IGDB-SECRET-DO-NOT-LOG");
        builder.ConfigureLogging(l => l.AddProvider(Logs));
        builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<IHostedService>();
            s.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => Http));
        });
    }
    // Clients: CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), AllowAutoRedirect = false })
}
```
`UseHttpsRedirection` has no configured HTTPS port under TestServer, so it only logs a warning; using an `https://localhost` BaseAddress avoids even that.

### Pattern 2: JWT validation matrix (D-02)
Test endpoint: `GET /api/games` (`[Authorize]`; rejected tokens never reach the controller, so 401 cases do not touch SQL; the valid case runs one read-only EF query for a non-existent user id and returns `200 []`, requiring the dev SQL Server just like the existing tests).
Cases (all must be asserted against the *same* host): valid `AuthService.GenerateToken` token -> 200; `exp` = now - 1h (**not** -1 min; `ClockSkew` is 5 min) -> 401; signed with a different secret -> 401; wrong issuer -> 401; wrong audience -> 401; HS512 signed with the right secret -> 401 (`ValidAlgorithms = [HS256]`, Phase 3 D-04); `alg: none` unsigned token -> 401; no `exp` -> 401 (`RequireExpirationTime`). The lifetime test is a pure unit test: `new AuthService(db: null!, config, NullLogger<AuthService>.Instance).GenerateToken(user)` -> `new JwtSecurityTokenHandler().ReadJwtToken(t).ValidTo` within a few seconds of `UtcNow.AddHours(24)` (`GenerateToken` never touches `db`).
```csharp
static string Token(string secret, string iss, string aud, DateTime exp, string alg = SecurityAlgorithms.HmacSha256)
{
    var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), alg);
    var jwt = new JwtSecurityToken(iss, aud, [new Claim(ClaimTypes.NameIdentifier, "2147483000")],
                                   notBefore: exp.AddHours(-24), expires: exp, signingCredentials: creds);
    return new JwtSecurityTokenHandler().WriteToken(jwt);
}
```
HS512 needs a key >= 64 bytes? (`HS512` requires a 128-bit minimum in IdentityModel; use a >= 64-byte secret for that one case to avoid a spurious `IDX10653`.) `[ASSUMED]`

### Pattern 3: Recording stub through `IHttpClientFactory` (D-01)
`IgdbService`/`SteamService` call `httpFactory.CreateClient()` (default name) **on every request** and, for IGDB, set `DefaultRequestHeaders` on that fresh client — so a stub factory returning a new `HttpClient(handler, disposeHandler:false)` each time is faithful. The handler must copy the request body *inside* `SendAsync` (content is disposed after send).
```csharp
public sealed class RecordingHandler : HttpMessageHandler
{
    public record Seen(HttpMethod Method, Uri Uri, Dictionary<string,string> Headers, string? ContentType, string Body);
    public List<Seen> Requests { get; } = [];
    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new(HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    {
        var body = r.Content is null ? "" : await r.Content.ReadAsStringAsync(ct);
        Requests.Add(new(r.Method, r.RequestUri!, r.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                         r.Content?.Headers.ContentType?.MediaType, body));
        return Responder(r);
    }
}
public sealed class StubHttpClientFactory(HttpMessageHandler h) : IHttpClientFactory
{ public HttpClient CreateClient(string name) => new(h, disposeHandler: false); }
```
**Assertions available today (read from the code):**
- IGDB token request: `POST https://id.twitch.tv/oauth2/token`, `Uri.Query == ""`, `Content-Type: application/x-www-form-urlencoded`, body contains `client_id`, `client_secret`, `grant_type=client_credentials`; the secret value appears in the body only, never in `Uri`/headers.
- IGDB query request: `POST https://api.igdb.com/v4/games` (`/screenshots`, `/artworks` for image calls), `Authorization: Bearer <token>`, `Client-ID` header, `text/plain` apicalypse body, no secret in URL. Token responses need `{"access_token":"…","expires_in":…}` (see `IgdbTokenResponse` in `Domain/IgdbModels.cs`).
- Token caching: second call issues no second token request (new `IgdbService` per test — it is a singleton with an instance cache).
- Failure surfaces: token endpoint 400 and query endpoint 500 both throw `HttpRequestException` (`EnsureSuccessStatusCode`); the IGDB query failure additionally logs `LogError("IGDB error {Status}: {Body}")` — assert the log contains the status and *not* the client secret/bearer token.
- Steam: key is in the query string by design (D-02 of Phase 2) — assert `key=` is present exactly once in `Uri.Query`, that `GetOwnedGamesAsync` throws `HttpRequestException` on 500 and `InvalidOperationException` on an empty `games` list, that numeric input >= 15 digits skips vanity resolution (no `ResolveVanityURL` request), and that **neither the logged message nor `Exception.ToString()` of any captured log entry contains the API key** (stub throwing `HttpRequestException("…")` and returning 500 both).
- `GetPlayerSummaryAsync` returns `null` on non-success (does not throw) but rethrows on exceptions — assert both (this matters for the Steam callback defect below).

### Pattern 4: Observing the real HttpClient logging (the leak test)
Do NOT rely on the stub factory for the redaction assertion — it bypasses the logging handlers. Resolve `SteamService` from `ApiFactory.Services` (singleton, real `IHttpClientFactory` with the stub as primary handler, real `appsettings.json` Logging section, `CapturingLoggerProvider` attached), call `GetOwnedGamesAsync("76561198000000001", ct)` against a 200 stub, then assert `Logs.Entries.All(e => !e.Text.Contains("TEST-STEAM-KEY-DO-NOT-LOG"))`. **This test fails on the current code** (categories `System.Net.Http.HttpClient.Default.LogicalHandler` and `.ClientHandler`, EventId 100, Information: `Sending HTTP request GET …?key=<KEY>&steamid=…`) and passes once `appsettings.json` has `"System.Net.Http.HttpClient": "Warning"` under `Logging:LogLevel`. `[VERIFIED: executed on net8.0 — default Information logging printed the key; AddFilter("System.Net.Http.HttpClient", Warning) suppressed it]`. Also capture scopes (`ILoggerProvider` with `IExternalScopeProvider`) if you want the stricter check, since `LogicalHandler` puts the URL in a scope.

### Pattern 5: Steam OpenID callback tests (D-09)
`SteamAuthService(db, auth, steam, httpFactory)`; `ValidateAndExtractSteamIdAsync` is private, so test through `HandleCallbackAsync(IQueryCollection)` using `new QueryCollection(new Dictionary<string, StringValues>{…})`. All **rejection** paths return `null` *before* the DB is touched, so `db` can be a `GamePlannerDbContext` built with `UseSqlServer(<anything>)` that never opens a connection. Cases: Steam body `is_valid:false` -> null; HTTP 500 from Steam -> null (currently throws — see defects); `claimed_id` with wrong prefix (`http://evil.example/openid/id/7656…`) -> null; 16- and 18-digit and non-numeric ids -> null; `claimed_id` prefix with trailing path/query (`…/id/76561198000000001/../x`) -> null; request sent to Steam contains `openid.mode=check_authentication` and **only** to `https://steamcommunity.com/openid/login`; attacker-supplied `openid.mode=id_res` extra duplicates are overridden. The **success** path (valid -> user lookup/create -> token) needs the dev DB: use a fake `SteamId` (`"7656119" + 10 random digits`), stub the player-summary call with 404, and delete the user row in `finally` (same cleanup discipline as `gsd-probe-` rows). Controller-level offline test: with `ApiFactory`, `GET /api/auth/steam/callback?openid.claimed_id=…` + stub `is_valid:false` -> `302 Location: http://localhost:4200/login?error=steam_failed`.

### Pattern 6: Playwright config for this repo (D-04/D-05)
```ts
// e2e/playwright.config.ts
import { defineConfig } from '@playwright/test';
try { process.loadEnvFile('.env'); } catch { /* env may come from the shell */ }
export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  fullyParallel: false, workers: 1,           // shared data + 'mutations' 100/hour/user + 'auth' 10/min limits
  timeout: 120_000, expect: { timeout: 10_000 },
  retries: 0, reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.UAT_UI_URL ?? 'http://localhost:4200',
    ignoreHTTPSErrors: true,                  // dev cert at https://localhost:7174 (replaces the Phase 4 proxy hack)
    channel: 'chrome',                        // system Chrome; avoids downloading Playwright chromium-1248
    storageState: '.auth/uat.json',           // written by globalSetup; gitignore .auth/
    trace: 'retain-on-failure',
  },
});
```
`globalSetup`: (1) preflight — `fetch(UI)` and `request.get(API + '/api/games')` must give 200/401, else throw "start the dev servers (see CLAUDE.md)"; (2) refuse to run if `UAT_EMAIL` is not `uat-test@gameplanner.local` (protects the owner account, D-05); (3) `POST {API}/api/auth/login` with `UAT_EMAIL`/`UAT_PASSWORD` from env (once per run), then write storageState with origin `http://localhost:4200` localStorage `gp_token` = response token. `.env.example` is committed with empty values; `.env`, `.auth/`, `playwright-report/`, `test-results/` go in `.gitignore`. **Never write the password into any planning doc** (it exists in the user's local memory file only). `[VERIFIED: probe — unauthenticated API call -> 401, cross-origin POST from the SPA origin to the API -> 401 with no CORS/cert failure, `/games` redirects to `/login` when unauthenticated]`

### Pattern 7: Karma specs (D-03) — proven shapes
```ts
// auth.interceptor.spec.ts  (class-based interceptor registered via DI -> withInterceptorsFromDi)
const auth = jasmine.createSpyObj<AuthService>('AuthService', ['getToken', 'logout']);
TestBed.configureTestingModule({ providers: [
  { provide: AuthService, useValue: auth },
  { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
  provideHttpClient(withInterceptorsFromDi()), provideHttpClientTesting() ] });
// expectOne('/x').request.headers.get('Authorization') === 'Bearer abc'; no header when token null;
// flush(…, {status: 401}) -> auth.logout called (interceptor also rethrows the error)

// auth.guard.spec.ts  (AuthGuard is a class with canActivate(): boolean, providedIn root)
const router = jasmine.createSpyObj<Router>('Router', ['navigate']);
// isLoggedIn -> false: canActivate() === false && navigate toHaveBeenCalledWith(['/login']); true: returns true, no navigate

// game-list.component.spec.ts  (single ctor dep; no TestBed rendering needed)
const svc = jasmine.createSpyObj<GameService>('GameService', ['getGames', 'deleteGame']);
svc.getGames.and.returnValue(of([]));
svc.deleteGame.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500, error: 'SqlException: secret', statusText: 'x' })));
const cmp = new GameListComponent(svc); cmp.deleteGame(1);
expect(cmp.error).toBe('Could not delete game. Please try again.');   // no '500', no 'SqlException'
```
Add: guard "returns true when logged in", interceptor "no Authorization header without token", interceptor "does not call logout on non-401" = 6-7 specs total. Run: `cd game-planner-ui && npx ng test --watch=false --browsers=ChromeHeadless` `[VERIFIED: 2 probe specs green, Chrome Headless 155]`.

### Anti-Patterns to Avoid
- **Running tests against the developer's API/DB for auth** (D-02 forbids it); and **leaving `UseEnvironment` at default `Development`** — it silently loads the real rotated secrets from user-secrets.
- **Not removing hosted services** — `GameSyncWorker.ExecuteAsync` calls `RunAsync` immediately (real IGDB + DB writes) on host start.
- **Asserting redaction only on the stub-factory path** — misses the HttpClientFactory logging handlers (the actual leak).
- **Setting `ClockSkew = 0` to make an expired-token test easy** — Phase 3 D-03 locked it at 5 min; expire the token by >5 min instead.
- **`git add -A`** — unrelated WIP in four files (see Project Constraints).
- **Hand-rolling a JWT or OpenID verifier in the test** — drive the real middleware / real `SteamAuthService`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| In-process API host | Custom `HttpListener`/Kestrel launcher | `WebApplicationFactory<Program>` | Handles content root, TestServer, DI override hooks |
| Disabling background workers in tests | Config flags in `Program.cs` | `ConfigureTestServices(s => s.RemoveAll<IHostedService>())` | Zero production-code change |
| Stubbing outbound HTTP globally in the host | Replace `IgdbService`/`SteamService` registrations | `ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(...))` (.NET 8) | Keeps the real service code and the real logging handlers in the path |
| JWT creation/inspection in tests | Manual base64/HMAC | `JwtSecurityTokenHandler` + `SigningCredentials` (already referenced transitively) | Correct header/alg handling |
| HTTPS cert trust for Playwright | The Phase 4 node HTTP->HTTPS proxy + `context.route` | `ignoreHTTPSErrors: true` | Verified working end-to-end |
| `.env` loading for e2e | `dotenv` dependency | `process.loadEnvFile()` | Built into Node >= 20.12 |
| Dependency lockfile surgery | Hand-editing `package-lock.json` | Bump `package.json` ranges + regenerate with npm 11 | Lockstep Angular peers; hand edits cause ERESOLVE |
| OpenID 2.0 signature verification | Re-implementing HMAC/association handling | Keep delegating to Steam's `check_authentication` (stateless mode) and add the RP-side checks listed in D-09 | Only Steam holds the signing key |

**Key insight:** every hardening contract here is either a *pipeline* property (middleware, logging handlers) or a *config* property; tests that bypass the pipeline give false assurance, which is exactly how the Steam-key log leak survived Phase 2.

## Common Pitfalls

### Pitfall 1: Expired-token test passes accidentally (is actually accepted)
**What goes wrong:** token with `exp = now - 1 min` returns 200. **Why:** default `ClockSkew` 5 min (locked by Phase 3 D-03). **Avoid:** expire by >= 1 hour. **Signal:** expired-token test intermittently green/red.

### Pitfall 2: Test host starts real workers / loads real secrets
**What goes wrong:** the factory run writes `WorkerStatus` rows, calls IGDB with the owner's rotated secret, or fails with "IDX10703 key length is zero". **Avoid:** Pattern 1 (`Testing` env, `UseSetting("Jwt:Secret")`, `RemoveAll<IHostedService>()`).

### Pitfall 3: Building the test project while the dev API is running
**What goes wrong:** `dotnet test` also builds `GamePlanner.API`; the running `dotnet run` process (PID currently bound to 5127/7174) locks `GamePlanner.API/bin/Debug/net8.0/GamePlanner.API.dll`, so any rebuild after an API source change fails with a copy/lock error. **Avoid:** stop the dev API (and `ng serve` is fine) before `dotnet build/test`; restart it afterwards for UAT. Pure in-memory filters can still run with `--no-build` (verified: `SteamTokenStoreSweepTests` 4/4 pass this way).

### Pitfall 4: Steam API key in Information logs (real defect)
See Pattern 4. Config fix: `Logging:LogLevel:"System.Net.Http.HttpClient": "Warning"` in `appsettings.json` (applies to all environments). Keep the existing catch-block redaction. Also note `SteamService`'s catch blocks pass the exception object to `LogError(ex, …)`; this is safe today only because .NET 8 `HttpRequestException` messages do not contain the URL — assert it, do not assume it.

### Pitfall 5: First-time Steam login 500s (real defect)
`SteamAuthService` creates `User { SteamId, DisplayName, … }` with no `Email`; `AuthService.GenerateToken` then runs `new Claim(ClaimTypes.Email, user.Email)` -> `ArgumentNullException` `[VERIFIED: executed]`, **after** `SaveChangesAsync` has already persisted the user, so every later attempt fails identically. The owner's row (Id 2, `76561198115372977@steam.local`) was created by older code, which is why UAT never hit it. **Fix:** add the email claim only when non-empty (mirrors the existing `steamId` claim branch); the success-path test with a fake SteamId proves it.

### Pitfall 6: Steam OpenID `return_to` not verified (D-09 — see section below)

### Pitfall 7: `/workers` "Run now" is global, not per-user, and Steam playtime is not triggerable
`WorkerController.KnownWorkers` = `GameSyncWorker`, `GameImageSyncWorker`, `GameHLTBSyncWorker` only; `SteamPlaytimeSyncWorker` (and Upcoming/Import/TokenCleanup) have no `/api/worker/{name}/run` path, so D-04's "Steam playtime" step cannot be driven from `/workers` — treat as D-06 (manual + `WorkerStatus` SQL/API check) or assert its row exists. The three triggerable workers process **all users'** games (including the owner's 481 active games): use non-force "Run now" only (Phase 4 UAT precedent), poll `GET /api/worker` JSON (not the UTC-as-local display text) until `status != 'Running'`, assert `lastError === null`. Do the Half-Life fix (D-08) *before* any `Force Run`.

### Pitfall 8: Playwright browser revision mismatch
Installed `ms-playwright/chromium-1243` vs 1.64.1 wanting 1248. Use `channel: 'chrome'` (Chrome 155 installed) or run `npx playwright install chromium`.

### Pitfall 9: Rate limits during UAT
`auth` policy: 10/min (login+register) — log in once in `globalSetup`, reuse `storageState`; `mutations`: 100/hour per user for game/journal/session writes — keep the UAT under ~30 mutations and make it idempotent (names `uat-<timestamp>`). Soft-deleted UAT games accumulate as `IsActive=0` rows for UserId 17 (acceptable, test account).

### Pitfall 10: npm toolchain on this machine is old
Both `npm` on PATH resolve to **8.12.1**, even with Node 24.19.0; `npm audit fix [--dry-run]` crashes ("Cannot read properties of null (reading 'edgesOut')"). Use `npx -y npm@11 …` (11.21.1) for audit/lockfile work, then verify with the project's own build/test/boot smoke (Phase 1 precedent). `npm ci` with the committed lockfile still works with 8.12.

### Pitfall 11: `sqlcmd` from Git Bash
Set `MSYS_NO_PATHCONV=1`; `-i <file>` with `-E` produced a bogus "-E and -U/-P mutually exclusive" error in this shell — use `-Q "…"` (works). Always prefix with `SET QUOTED_IDENTIFIER ON;` for statements touching `Games`. Use `-S localhost -d GamePlannerDB -E` (Windows auth).

### Pitfall 12: Stale references
`STATE.md` "Pending Todos" still lists the IGDBId-upsert todo, but it is in `.planning/todos/completed/` and Phase 4 UAT check 3 passed; the only pending todo is Half-Life (folded here). Update STATE when closing the phase.

## D-07: Dependency Vulnerability Baseline (executed read-only)

### npm (`game-planner-ui`, current `package-lock.json`)
`npm audit` today: **29 vulnerabilities — 1 low, 9 moderate, 14 high, 5 critical** (Phase 1 recorded 7 [4 moderate, 3 high]; advisories published since then plus the installed 22.1.x line account for the change). `[VERIFIED: npm audit --json]`

| Severity | Packages | Runtime or tooling | Fix inside Angular 22 range? |
|----------|----------|--------------------|------------------------------|
| high | `@angular/router` (3 **SSR-only** DoS advisories, range 22.0.0–22.2.0) | **Shipped dependency**, but advisories are SSR-specific; app is CSR-only | Yes: `^22.2.3` (all `@angular/*` runtime packages move in lockstep; they peer-pin each other exactly) |
| critical | `@angular-devkit/build-angular` (via chain), `@angular/build`, `piscina` (RCE gadget), `proxy-addr`, `shell-quote` | devDependency / build tooling | Yes — resolved by `build-angular`/`cli` 22.2.4 + fresh transitive resolution |
| high | `@angular/cli`, `@modelcontextprotocol/sdk`, `body-parser`, `brace-expansion`, `compression`, `engine.io`, `fast-uri`, `follow-redirects`, `launch-editor`, `source-map-js`, `vite`, `webpack-dev-middleware`, `zod` | devDependency / tooling | All but `vite` — yes |
| moderate/low | `webpack-dev-server`, `sockjs`/`uuid`, `qs`, `express`, `hono`, `ip-address`, `postcss-selector-parser`, `serialize-javascript`, … | tooling | report only per D-07 |

**Experiment (copy of `package.json`+lockfile in the scratchpad, `--package-lock-only`, npm 11):**
- `npm audit fix` alone -> 4 remaining (2 high: `@angular/router` stuck at 22.1.4, `vite 8.3.0`).
- **Bumping the nine `@angular/*` runtime/compiler packages to `^22.2.3` in `package.json` and regenerating the lockfile -> 3 remaining (2 low, 1 high): `vite 8.3.0–8.3.2` (high) and its dependents `@angular/build`/`@angular-devkit/build-angular` (low).** The install result: `@angular/core|router 22.2.3`, `@angular/cli` and `build-angular` 22.2.4, `typescript 6.0.3`, `rxjs 7.8.2`, `zone.js 0.15.1`, `jasmine-core 6.3.0`, `karma 6.4.4`, `@types/jasmine 6.0.0` — i.e. the Phase 1 pins survive. `image-size` and `uuid` (the Phase 1 accepted findings) no longer appear. A lockfile-only `npm install ... @angular/*@^22.2.3` onto the *existing* lockfile fails with ERESOLVE (old exact optional-peer pins); do the bump with the real `node_modules` present or from a regenerated lockfile.
- **Residual `vite` finding is not fixable in-range:** `@angular/build@22.2.4` depends on `vite` exactly `8.3.0` `[VERIFIED: npm view @angular/build@22.2.4 dependencies.vite]`, and `npm audit fix` "resolves" it by downgrading to 8.1.5 (also vulnerable). Document as accepted risk: dev-time only, and this project builds/serves with the webpack `browser`/`dev-server`/`karma` builders (`angular.json`), not `@angular/build`'s Vite dev server (`[ASSUMED]` — confirm with `npm ls vite` and a grep that no target uses `@angular/build:*`). Option the planner must NOT take silently: `overrides: { "vite": "…" }` (unsupported combination).
- Required follow-up after the bump (Phase 1 precedent): `npm ci`, `ng build`, `npx ng test --watch=false --browsers=ChromeHeadless`, dev-server boot smoke, then the UAT run.

### NuGet (`dotnet list GamePlanner.sln package --vulnerable --include-transitive`)
| Project | Package | Resolved | Severity | Cause | Fix |
|---------|---------|----------|----------|-------|-----|
| GamePlanner.Tests | `System.Net.Http` | 4.3.0 | **High** (GHSA-7jgj-8wvc-jh57) | `xunit 2.5.3 -> xunit.assert/core -> NETStandard.Library 1.6.1` | `xunit` 2.9.3 (netstandard2.0 group, no NETStandard.Library) |
| GamePlanner.Tests | `System.Text.RegularExpressions` | 4.3.0 | **High** (GHSA-cmhx-cq75-c4mj) | same chain | same |
| API + Tests | `System.IdentityModel.Tokens.Jwt`, `Microsoft.IdentityModel.JsonWebTokens` | 7.0.3 | Moderate (GHSA-59j7-ghrg-fj52 / CVE-2024-21319, JWT-token DoS) | `JwtBearer 8.0.0` (and `Microsoft.Data.SqlClient 5.1.9`) | **Report-only per D-07**; one-line patch bump `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.0 -> 8.0.32` pulls IdentityModel 7.7.3. Auth-adjacent and zero-risk, so offer it to the user as an optional inclusion. |

`--deprecated`: `xunit 2.5.3` flagged "Legacy" (alternative `xunit.v3`); 2.9.3 is fine, v3 migration is out of scope. After changes re-run the scan; goal: API clean (or moderate-only), Tests clean.

## D-08: Half-Life / Half-Life 2 IGDBId (read-only investigation; no data changed)

**Scan result (all users, active rows):**
```sql
SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON;
SELECT g.UserId, g.IGDBId, COUNT(*) AS Cnt, COUNT(DISTINCT g.Name) AS DistinctNames,
       STRING_AGG(CONCAT(g.Id,':',g.Name), ' ; ') AS Rows_
FROM Games g WHERE g.IsActive = 1 AND g.IGDBId IS NOT NULL
GROUP BY g.UserId, g.IGDBId HAVING COUNT(*) > 1;
```
Returns exactly **one** group: `UserId 2, IGDBId 267639 -> 107683:Half-Life ; 107687:Half-Life 2`. A second query without the `IsActive` filter also yields exactly 1 duplicate group (so no inactive-row duplicates). `[VERIFIED: sqlcmd against dev DB]` Scope for D-08 is therefore this one pair; the executor should re-run the scan immediately before applying the fix.

**Current rows:** both have `IGDBId 267639`, `FirstReleaseDate 2018-12-25`, identical `CoverImageUrl …/co779u.jpg`, `IGDBUpdatedAt 2026-09-28 15:56:43`, `Source = Steam`, 1 genre each; SteamAppIds are **70** (Half-Life) and **220** (Half-Life 2). Neighbouring rows look sane (Opposing Force 232, Blue Shift 285235, Source 6744, Alyx 126098).

**How to find the correct IDs (authoritative -> weakest):**
1. IGDB `external_games` lookup by Steam appid (rows carry `SteamAppId`): query `fields game.name, game; where uid = "70"` / `"220"` with the Steam source filter (`external_game_source = 1`, older docs: `category = 1`). `[ASSUMED]` — IGDB docs were not fetchable (403); verify the field names against the API response. Needs the IGDB credentials, which only exist in user-secrets; run through the app or a throwaway script without printing secrets.
2. The app's own `GET /api/games/igdb/search?name=Half-Life` as the UAT user (returns up to 5 candidates with `igdbId`).
3. Weak lead only: an unofficial mirror shows Half-Life 2 = IGDB **233** `[LOW — non-authoritative, https://moviefy.mewayz.com/details/igdb/game/233/half-life-2]`; consistent with Opposing Force = 232 in the DB, but verify.

**Script shape (`Database/38_FixHalfLifeIgdbId.sql`, applied manually):**
- `USE GamePlannerDB; SET QUOTED_IDENTIFIER ON;` (+ `SET ANSI_NULLS ON`) — mandatory because of the filtered indexes on `Games` (same lesson as scripts 34/36).
- Guard the UPDATE with the old value (`WHERE Id = <wrong row> AND UserId = 2 AND IGDBId = 267639`) so re-running is a no-op; set `ModifiedAt = SYSUTCDATETIME()` to match the Phase 4 audit convention (the SPs stamp it in T-SQL; a manual UPDATE must do it itself).
- Record old values for reversal in the script header and as a commented `-- REVERSAL:` block: `Id, IGDBId, FirstReleaseDate, CoverImageUrl, IGDBUpdatedAt, LastIGDBSyncAt, LastImageSyncDate, AggregatedRating*` of the touched row (optionally also `SELECT … INTO` a small backup table, but keep it simple).
- **Stale derived data:** `GameSyncManager.EnrichGameAsync` skips rows whose IGDB `updated_at <= IGDBUpdatedAt` and only fills genre/tag collections *if empty*, so after changing the ID the row keeps the wrong game's release date/cover/genre until those fields are reset. Include `IGDBUpdatedAt = NULL` (and decide on `CoverImageUrl`/`LastImageSyncDate`, and the row's `GameGenres`) in the script, then run the normal (non-force) GameSync/Image workers and confirm. Verify the `GameImageManager` re-fetch condition before relying on `LastImageSyncDate`.
- Verify afterwards with the scan query (0 rows) and `SELECT … WHERE Id IN (…)`.

**Out-of-scope observation (log as todo, do not fix):** `Portal` (Id 107693, `SteamAppId 400`) carries `IGDBId 14546` and `FirstReleaseDate 1986-11-01` — almost certainly the 1986 Infocom text adventure, not Valve's Portal; the Steam-import enrichment picks `SearchGameAsync`'s first/name match and can attach the wrong IGDB entry without producing a duplicate. `[ASSUMED]` Duplicate-ID scanning (D-08) will not find this class of error.

## D-09: Steam OpenID Trust-Boundary Review (read-only)

Flow: `GET /api/auth/steam` builds `checkid_setup` redirect with `return_to = {App:BaseUrl}/api/auth/steam/callback`; Steam redirects the browser to the callback with signed `openid.*` params; `SteamAuthService.ValidateAndExtractSteamIdAsync` POSTs all received query params back to `https://steamcommunity.com/openid/login` with `openid.mode=check_authentication`, accepts if the body contains `is_valid:true`, then takes the SteamId from the *unsigned-by-us* `openid.claimed_id` (prefix + `^\d{17}$`). On success it issues a one-time code (`SteamTokenStore`, 5 min, `Guid.NewGuid()` = 122 random bits, `TryRemove` single-use) and redirects the SPA to `/login?code=…`; the SPA POSTs the code to `/api/auth/steam/exchange` to receive the JWT. Correct and good: fixed Steam endpoint (attacker params cannot redirect verification), anchored SteamId regex, bearer token never in a URL, single-use short-lived code, inactive users rejected.

| # | Issue | Severity | Disposition |
|---|-------|----------|-------------|
| S1 | **`AuthService.GenerateToken` crashes for any Steam user with null `Email`** (new Steam users) after persisting the user | High (functional; blocks all new Steam signups, poisons the row) | **Fix now** + test (Pitfall 5) |
| S2 | **`openid.return_to` is never compared to the expected callback URL.** A malicious relying party that Steam also authenticates a victim for can replay its signed assertion (return_to = attacker site) to this callback; Steam's `check_authentication` verifies only the signature, so login as the victim succeeds (OpenID 2.0 §11.1 requires the RP to verify `return_to`). | Medium | **Fix now**: pass the expected callback URL into `HandleCallbackAsync` (controller already builds it for `SteamLogin`; extract one helper) and require exact match; test with a foreign `return_to` |
| S3 | `check_authentication` call has no error handling: Steam unreachable/timeout -> unhandled exception -> raw 500 from `/steam/callback` instead of the intended `…/login?error=steam_failed` redirect. Same for `GetPlayerSummaryAsync`, which since Phase 2 **rethrows**, so a Steam Web API hiccup breaks login even though the summary is best-effort (callee already tolerates `null`). | Medium (reliability) | **Fix now**: catch in `HandleCallbackAsync`/validator -> `null` for validation failures; treat summary failure as `null` summary (log, continue) |
| S4 | `body.Contains("is_valid:true")` substring match; `openid.mode`, `openid.ns`, `openid.op_endpoint` not checked; `openid.signed` not required to cover `claimed_id`, `identity`, `return_to`, `response_nonce`, `op_endpoint`. Signature check by Steam makes exploitation unlikely, but the SteamId is read from a field the RP never confirmed is signed. | Low (defense in depth) | **Fix now** (cheap): parse response by line (`is_valid:true` exactly), require `mode == id_res`, `ns == http://specs.openid.net/auth/2.0`, `op_endpoint == https://steamcommunity.com/openid/login`, signed-list contains the five fields. Signed-field list contents are `[ASSUMED]` from OpenID 2.0 §10.1 / typical Steam responses — verify against a captured real response before enforcing, or the owner's login breaks (D-06 manual re-test required). |
| S5 | No `state`/browser binding (login CSRF: an attacker can feed the victim's browser the attacker's own valid assertion/callback and log them into the attacker's account; response_nonce replay protection is the OP's job `[ASSUMED]`) | Low for a single-user personal app | **Log as todo** (needs cookie/state in `return_to`; larger than a targeted fix) |
| S6 | `/steam/callback` and `/steam/exchange` carry no `[EnableRateLimiting]`; `?code=` stays in browser history/Referer until the SPA navigates away (code is single-use and 5-minute) | Low | **Log as todo** (optionally strip code with `history.replaceState` after exchange) |
| S7 | Concurrency/ordering: `HandleCallbackAsync` find-then-insert for new SteamId without a unique index on `Users.SteamId` (none observed in column listing) can create duplicates under double-submit | Low | note in todo; not tested here |

Offline testability: rejection paths (S2–S4, bad prefix, bad id) with `RecordingHandler` and a never-opened `DbContext`; success path (S1) with the dev DB and a probe SteamId cleaned in `finally`; end-to-end 302 behaviour via `ApiFactory`. Real-Steam behaviour (signed list, replayed nonce) can only be confirmed by the owner's manual login (D-06) — plan one manual re-test after S2/S4 land.

## Code Examples

### Karma script and run
```jsonc
// game-planner-ui/package.json
"test:ci": "ng test --watch=false --browsers=ChromeHeadless"
```

### Worker trigger check in UAT (API-level assertion, avoids the UTC display quirk)
```ts
// e2e/tests/uat.spec.ts (sketch)
const api = process.env.UAT_API_URL ?? 'https://localhost:7174';
await page.goto('/workers');
await page.locator('.worker-card', { hasText: 'GameSyncWorker' }).getByRole('button', { name: /Run now/ }).click();
await expect.poll(async () => {
  const r = await page.request.get(`${api}/api/worker`, { headers: { Authorization: `Bearer ${token}` } });
  return (await r.json()).find((w: any) => w.name === 'GameSyncWorker')?.status;
}, { timeout: 90_000, intervals: [2000] }).not.toBe('Running');
// then: status 'Completed' (verify enum text against GET /api/worker) and lastError === null
```
Selectors available (no `data-testid` exist): login `input[type=email]`, `input[placeholder="Password"]`, `button.btn-primary` ("Sign in"); games `button:has-text("+ Add Game")`; IGDB modal `input.search-input[placeholder="Search IGDB by title..."]`, `button.search-btn`; journal `button:has-text("+ Add Entry")`; now-playing `button.add-slot-btn`, picker `placeholder="Search your library…"`; workers `.worker-card` + `button:has-text("Run now")`. Routes: `/games`, `/now-playing`, `/journal`, `/workers`, `/planner`, `/stats`, `/calendar`, `/backlog-prioritizer`, `/tracked-games`, `/profile`, `/dashboard`.

### Logging fix (config only)
```jsonc
// GamePlanner.API/appsettings.json -> Logging:LogLevel
"Microsoft.AspNetCore": "Warning",
"System.Net.Http.HttpClient": "Warning"
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `WebApplicationFactory` + `Startup` | `WebApplicationFactory<Program>` with top-level statements + `public partial class Program` | .NET 6 | Required one-liner here |
| `ConfigureTestServices` + per-client handler overrides | `ConfigureHttpClientDefaults` | .NET 8 | One call stubs every `IHttpClientFactory` client |
| HttpClient logging redaction | Query-string redaction built in by default | .NET 9 `[ASSUMED]` | **Not** present on net8.0 (verified) — config filter required |
| `@angular-devkit/build-angular:karma` | `@angular/build:karma` (Vitest/Karma options) | Angular 21/22 | Current builder prints a deprecation warning only; Phase 1 declined the migration, keep |
| Playwright-managed browsers | `channel: 'chrome'` supported | long-standing | Avoids revision mismatch |

**Deprecated/outdated:** global npm 8.12.1 on this machine (audit fix crash); `xunit` 2.5.3 (Legacy, vulnerable transitive); the Phase 4 proxy workaround for the dev cert.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | IGDB `external_games` supports lookup by Steam `uid` with `external_game_source = 1` (or legacy `category = 1`) | D-08 | Lookup method fails; fall back to the app's IGDB search endpoint |
| A2 | Half-Life 2's IGDB id might be 233 (unofficial mirror) | D-08 | Wrong ID applied if used without verification — must verify before writing the script |
| A3 | Portal IGDB id 14546 is the 1986 text adventure | D-08 observation | Only a todo; no action taken |
| A4 | Steam's assertions sign `claimed_id, identity, return_to, response_nonce, op_endpoint, assoc_handle`; Steam's OP rejects replayed nonces in `check_authentication` | D-09 S4/S5 | If the signed list differs, enforcing S4 would break the owner's login — verify against a captured real response / manual login |
| A5 | `xunit.runner.visualstudio` 2.5.3 works with `xunit` 2.9.3 (else 3.1.5); `Microsoft.NET.Test.Sdk` 17.14.1 compatible | Standard Stack | Test discovery fails after bump -> bump runner |
| A6 | Vite is not exercised by this project's builders (webpack `browser`, `dev-server`, `karma`) so the residual vite finding is dev-time-irrelevant | D-07 | If a target does use Vite, finding is relevant to local dev server only |
| A7 | HS512 JWT test needs a >= 64-byte secret to avoid IDX10653 | Pattern 2 | Spurious test error, easy to adjust |
| A8 | .NET 9 added default query redaction in HttpClient logs | State of the Art | None for this phase (net8 verified) |

## Open Questions

1. **Which of 107683 / 107687 holds the wrong IGDBId, and what are the correct ids?**
   - Known: both share 267639; Steam appids 70/220 available. Unknown: IGDB's true ids (needs a credentialed IGDB call).
   - Recommendation: executor looks up via IGDB `external_games` or the app search endpoint as UserId 17; confirm with the user before applying the SQL (costly/reversibility per D-08).
2. **"Steam playtime" in D-04's worker step:** there is no `/api/worker/SteamPlaytimeSyncWorker/run`.
   - Recommendation: planner treats it as manual (D-06) with a SQL `WorkerStatus` check, or asks the user whether to add the worker to `KnownWorkers` (that would be feature work -> default: no).
3. **Should the moderate JWT-package CVE (`JwtBearer` 8.0.0 -> 8.0.32) be included?** D-07 says moderate = report-only; it is a one-line, auth-relevant patch bump. Recommend asking the user once; default per D-07 is report-only.
4. **Is `UseSetting("Jwt:Secret")` honoured for values read via `builder.Configuration` inside `Program.cs`?** Secret is read lazily inside the `AddJwtBearer` callback, so it should be. A 10-minute spike (first test) should confirm before writing the whole matrix; fall back to `ConfigureAppConfiguration(AddInMemoryCollection)`.
5. **UAT assertion that `Completed` is the exact status string** was not checked against `WorkerStatusManager`/DB — executor should read `GET /api/worker` once and pin the strings.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 8 | build/test | ✓ | 8.0.420 (also 5/9/10 installed; `dotnet --version` resolves to 10.0.401 — no `global.json`, TFM stays net8.0) | — |
| Node / npm | Angular, Playwright | ✓ / ⚠ | Node 24.19.0 / npm **8.12.1** (audit fix crashes) | `npx -y npm@11` (11.21.1 verified) |
| SQL Server (localhost, `GamePlannerDB`) | existing tests, success-path Steam test, UAT, D-08 | ✓ | `sqlcmd` 15.0.2000.5, Windows auth | — |
| `sqlcmd` | D-08 scan/verify | ✓ | needs `MSYS_NO_PATHCONV=1` and `-Q` in Git Bash | SSMS |
| Chrome | Karma ChromeHeadless, Playwright `channel:'chrome'` | ✓ | 155.0.8059.85 | `npx playwright install chromium` |
| Playwright browsers | alt. | ⚠ | chromium-1243 installed; 1.64.1 wants 1248 | use system Chrome |
| Dev API (`https://localhost:7174`, http 5127) | UAT | ✓ running now | — | `cd GamePlanner.API && dotnet run` (needs user-secrets) |
| Angular dev server (`http://localhost:4200`) | UAT | ✓ running now | — | `cd game-planner-ui && npm start` |
| IGDB credentials (user-secrets) | UAT IGDB search/sync, D-08 lookup | ✓ per Phase 2 | — | none (UAT IGDB steps are blocked without them) |
| Owner Steam account | Steam login/import UAT | manual | — | D-06 (user performs, Claude verifies via SQL/logs) |
| gitleaks | pre-commit | ✓ (Phase 2) | 8.30.1 | hook soft-fails open |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** npm 11 (use `npx`), Playwright chromium rev 1248 (use system Chrome).

## Security Domain

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | yes | BCrypt (existing), Steam OpenID RP checks (S1–S4), JWT issuance |
| V3 Session Management | yes | 24h JWT, 5-min skew, one-time Steam hand-off code; tests lock these in |
| V4 Access Control | yes (light) | `[Authorize]`; per-user scoping already fixed in Phase 3; UAT must use UserId 17 only |
| V5 Input Validation | yes | `claimed_id` prefix + `^\d{17}$` (existing); `openid.*` field validation (S4) |
| V6 Cryptography | yes | HMAC-SHA256 pinned (`ValidAlgorithms`); never hand-roll — use `System.IdentityModel.Tokens.Jwt` |
| V7 Error Handling & Logging | yes | Steam key must not reach logs (Pitfall 4); no server text in UI error (D-03) |
| V14 Config / Dependencies | yes | D-07 scan; secrets from env/user-secrets; e2e credentials via env only |

### Known Threat Patterns
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| OpenID assertion replay across relying parties (no `return_to` check) | Spoofing | Compare `openid.return_to` to own callback URL; verify via `check_authentication` |
| Secret in request URL logged by HttpClientFactory | Information disclosure | Logging category filter; secrets in headers/body where the provider allows (IGDB) |
| JWT alg confusion / `none` / expired / wrong aud | Spoofing / Elevation | Pinned algorithms, required expiry, issuer+audience validation (tests assert all) |
| Test host touching production-like secrets/data | Information disclosure / Tampering | `Testing` environment, in-memory test secret, workers removed |
| Committed test credentials | Information disclosure | env-only credentials, `.env` ignored, gitleaks hook |
| Dependency RCE gadgets in build tooling (piscina, shell-quote) | Elevation | Regenerate lockfile on patched ranges; residual vite documented |

## Sources

### Primary (HIGH confidence)
- Repository files read: `Program.cs`, `AuthService.cs`, `SteamAuthService.cs`, `SteamService.cs`, `IgdbService.cs`, `AuthController.cs`, `WorkerController.cs`, `SteamTokenStore.cs`, `GameManagerAtomicityTests.cs`, `SteamTokenStoreSweepTests.cs`, `GamePlanner.Tests.csproj`, `package.json`, `angular.json`, `karma.conf.js`, `src/test.ts`, interceptor/guard/game-list sources, `04-UAT.md`, Phase 2/3 CONTEXT, Half-Life todo.
- Executed this session: `npm audit --json`; `npx npm@11` lockfile experiments; `dotnet list package --vulnerable --include-transitive` / `--deprecated` / `dotnet nuget why`; `ng test` probe (2 specs green, then removed); Playwright 1.64.1 probe against live servers (scratchpad only); net8 console probe of HttpClientFactory logging and `Claim(null)`; read-only `sqlcmd` scans.
- NuGet flat-container nuspecs (xunit 2.9.3 dependency groups, Mvc.Testing 8.0.32, JwtBearer 8.0.32 -> IdentityModel 7.7.3) — nuget.org.
- npm registry: `@playwright/test`, `@angular/*` versions/dist-tags, `@angular/build@22.2.4` dependency on `vite 8.3.0`.

### Secondary (MEDIUM confidence)
- Microsoft docs on `WebApplicationFactory` integration testing (https://learn.microsoft.com/aspnet/core/test/integration-tests) — pattern knowledge, not re-fetched this session.
- GitHub advisory GHSA-59j7-ghrg-fj52 (CVE-2024-21319) via GitHub API.

### Tertiary (LOW confidence)
- Unofficial IGDB mirror for Half-Life 2 id 233: https://moviefy.mewayz.com/details/igdb/game/233/half-life-2 (verify before use).
- OpenID 2.0 / Steam signed-field and nonce behaviour — training knowledge.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — versions confirmed on registries, mechanisms executed.
- Architecture / test patterns: HIGH for Karma and Playwright (run), MEDIUM-HIGH for `WebApplicationFactory` config (derived from reading `Program.cs`; first test should spike `UseSetting`).
- Pitfalls: HIGH for the logging leak, null-Email crash, ClockSkew, locks, npm 8 crash (all reproduced/read); MEDIUM for Steam protocol specifics.
- D-07 numbers: HIGH as of today (advisory DB moves; re-run at execution time).

**Research date:** 2026-10-24
**Valid until:** 2026-11-07 for npm/NuGet advisory counts (fast-moving); 30 days for the rest.
