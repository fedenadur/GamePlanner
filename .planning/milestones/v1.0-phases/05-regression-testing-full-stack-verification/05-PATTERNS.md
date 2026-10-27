# Phase 5: Regression Testing & Full-Stack Verification - Pattern Map

**Mapped:** 2026-10-24
**Files analyzed:** 22 new/modified
**Analogs found:** 14 / 22 (the rest are greenfield test support or scripts where RESEARCH.md patterns are the source)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `GamePlanner.Tests/Support/ApiFactory.cs` | test-support | request-response | none (RESEARCH Pattern 1) | no analog |
| `GamePlanner.Tests/Support/RecordingHandler.cs` | test-support | request-response | none (RESEARCH Pattern 3) | no analog |
| `GamePlanner.Tests/Support/StubHttpClientFactory.cs` | test-support | request-response | none (RESEARCH Pattern 3) | no analog |
| `GamePlanner.Tests/Support/CapturingLoggerProvider.cs` | test-support | event-driven | none | no analog |
| `GamePlanner.Tests/AuthTokenTests.cs` | test | request-response | `GamePlanner.Tests/SteamTokenStoreSweepTests.cs` (pure part), `GameManagerAtomicityTests.cs` (config setup) | role-match |
| `GamePlanner.Tests/IgdbServiceContractTests.cs` | test | request-response | `SteamTokenStoreSweepTests.cs` (in-memory style) | role-match |
| `GamePlanner.Tests/SteamServiceContractTests.cs` | test | request-response | same | role-match |
| `GamePlanner.Tests/SteamAuthServiceTests.cs` | test | request-response | `GameManagerAtomicityTests.cs` (DB success path + cleanup in `finally`) | role-match |
| `GamePlanner.Tests/GamePlanner.Tests.csproj` (modify) | config | n/a | itself | exact |
| `GamePlanner.API/Program.cs` (modify, +1 line) | config | n/a | itself | exact |
| `GamePlanner.API/appsettings.json` (modify, log filter) | config | n/a | itself | exact |
| `GamePlanner.API/Services/AuthService.cs` (modify, Email claim) | service | transform | its own `steamId` claim branch (lines 72-73) | exact |
| `GamePlanner.API/Services/SteamAuthService.cs` (modify, S2-S4) | service | request-response | itself | exact |
| `GamePlanner.API/Controllers/AuthController.cs` (modify, callback URL helper) | controller | request-response | itself | exact |
| `game-planner-ui/src/app/interceptors/auth.interceptor.spec.ts` | test | request-response | none (no existing specs); RESEARCH Pattern 7 | no analog |
| `game-planner-ui/src/app/guards/auth.guard.spec.ts` | test | request-response | none; RESEARCH Pattern 7 | no analog |
| `game-planner-ui/src/app/components/game-list/game-list.component.spec.ts` | test | request-response | none; RESEARCH Pattern 7 | no analog |
| `game-planner-ui/package.json` (modify, `test:ci`, `@angular/*` bump) | config | n/a | itself | exact |
| `e2e/package.json`, `playwright.config.ts`, `global-setup.ts`, `tests/uat.spec.ts`, `.env.example` | config/test | request-response | none (RESEARCH Pattern 6) | no analog |
| `.gitignore` (modify: `e2e/.auth/`, `playwright-report/`, `test-results/`) | config | n/a | existing `.gitignore` | exact |
| `Database/38_FixHalfLifeIgdbId.sql` | migration | CRUD | `Database/37_SeedWorkerStatus_SteamTokenCleanup.sql`, `36_AddAuditColumns_ModifiedAt_DeletedAt.sql` | role-match |

## Pattern Assignments

### Backend test files (all under `GamePlanner.Tests/`)

**Analog:** `GamePlanner.Tests/SteamTokenStoreSweepTests.cs` and `GameManagerAtomicityTests.cs`

**Conventions to copy** (file-scoped namespace, implicit usings, `<Using Include="Xunit" />` already in csproj so no `using Xunit;`):

```csharp
using GamePlanner.API.Domain;
using GamePlanner.API.Services;

namespace GamePlanner.Tests;

public class SteamTokenStoreSweepTests
{
    private static AuthResponse NewResponse() => new() { Token = Guid.NewGuid().ToString("N") };

    [Fact]
    public void RemoveExpired_ShrinksStore_ToOnlyLiveEntries()
    {
        var store = new SteamTokenStore();
        ...
        Assert.Equal(4, store.Count);
```
Style: one `public class XTests` per file, `[Fact]`, Method_Behavior_Condition naming, plain `Assert.*`, XML `<summary>` on the class stating what is proven and how it touches the DB, no mocking framework, no base classes.

**Config + context setup for DB-backed tests** (`GameManagerAtomicityTests.cs` lines 19-43), reuse for `SteamAuthServiceTests` success path and for building a never-opened `GamePlannerDbContext` on rejection paths:
```csharp
private static string ConnectionString =>
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Server=localhost;Database=GamePlannerDB;Trusted_Connection=True;TrustServerCertificate=True;";

private static GamePlannerDbContext CreateContext()
{
    var options = new DbContextOptionsBuilder<GamePlannerDbContext>()
        .UseSqlServer(ConnectionString)
        .Options;
    return new GamePlannerDbContext(options);
}

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = ConnectionString })
    .Build();
```
Use the same in-memory `ConfigurationBuilder` for `new IgdbService(factory, config, logger)` / `new SteamService(...)` / `new AuthService(db: null!, config, NullLogger<AuthService>.Instance)` with `Jwt:Secret`, `IGDB:*`, `Steam:ApiKey` test constants.

**Probe-row cleanup discipline** (lines 45-57): throwaway rows get a recognizable prefix and are removed in `finally` via Dapper:
```csharp
await using var conn = new SqlConnection(ConnectionString);
await conn.OpenAsync();
await conn.ExecuteAsync("DELETE FROM Games WHERE Name LIKE @Prefix;", new { Prefix = namePrefix + "%" });
```
For the Steam new-user success test: fake SteamId `"7656119" + 10 random digits`, delete `Users WHERE SteamId = @id` in `finally`.

**Code-under-test excerpts the tests assert against:**

`IgdbService.cs` lines 8-44 (ctor `(IHttpClientFactory, IConfiguration, ILogger<IgdbService>)`, token POST with credentials in form body only, `EnsureSuccessStatusCode`, new service per test because of `_cachedToken`):
```csharp
var client = httpFactory.CreateClient();
var form = new Dictionary<string, string>
{ ["client_id"] = ClientId, ["client_secret"] = ClientSecret, ["grant_type"] = "client_credentials" };
var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(form));
response.EnsureSuccessStatusCode();
```
`SteamService.cs` lines 6, 36-52 (ctor `(IHttpClientFactory, IConfiguration, ILogger<SteamService>)`; key by design in query: `$"{BaseUrl}/ISteamUser/ResolveVanityURL/v1/?key={ApiKey}&vanityurl={vanity}"`; numeric input `>= 15` digits skips vanity resolution).

`AuthService.GenerateToken` (`AuthService.cs` lines 61-73), target of the 24h lifetime test and of the null-Email fix:
```csharp
var claims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new(ClaimTypes.Email, user.Email),      // <-- throws for Steam users (Email null)
};
if (!string.IsNullOrEmpty(user.SteamId))
    claims.Add(new Claim("steamId", user.SteamId));
```
Fix shape: mirror the existing `steamId` branch, i.e. `if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));`.

**No analog for:** `ApiFactory`, `RecordingHandler`, `StubHttpClientFactory`, `CapturingLoggerProvider`. Use RESEARCH.md Pattern 1 (ApiFactory), Pattern 3 (RecordingHandler, StubHttpClientFactory), Pattern 4 (log-capture), Pattern 2 (JWT matrix `Token(...)` helper). Put them in `Support/` with namespace `GamePlanner.Tests` (match existing flat namespace).

---

### `GamePlanner.Tests/GamePlanner.Tests.csproj` (config, modify)

**Analog:** itself (lines 12-17). Edit PackageReferences in place, keep format:
```xml
<PackageReference Include="coverlet.collector" Version="6.0.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="xunit" Version="2.5.3" />          <!-- -> 2.9.3 -->
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
<!-- add: Microsoft.AspNetCore.Mvc.Testing 8.0.32 -->
```
Pitfall: stop the running dev API before `dotnet build/test` (DLL lock).

---

### `GamePlanner.API/Program.cs` (modify)

**Analog:** itself. Current tail (end of file, after `app.Run();`) — append one line:
```csharp
app.MapControllers();

app.Run();

public partial class Program { }
```

### `GamePlanner.API/appsettings.json` (modify)

**Analog:** itself, lines 2-7. Add one key:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "System.Net.Http.HttpClient": "Warning"
  }
},
```

### `GamePlanner.API/Services/SteamAuthService.cs` (service, request-response; modify for S2-S4)

**Analog:** itself. Current structure to extend (lines 36-40, 80-103): `HandleCallbackAsync(IQueryCollection)` calls private `ValidateAndExtractSteamIdAsync`, which posts all query params with `openid.mode=check_authentication` to the fixed `SteamOpenIdUrl`, then substring-matches `is_valid:true`, then prefix + `^\d{17}$` on `claimed_id`.
```csharp
var args = query.ToDictionary(k => k.Key, v => v.Value.ToString());
args["openid.mode"] = "check_authentication";
var response = await client.PostAsync(SteamOpenIdUrl, content);
if (!response.IsSuccessStatusCode) return null;
var body = await response.Content.ReadAsStringAsync();
if (!body.Contains("is_valid:true")) return null;
...
if (!claimedId.StartsWith(SteamIdPrefix)) return null;
if (!System.Text.RegularExpressions.Regex.IsMatch(steamId, @"^\d{17}$")) return null;
```
Add: `expectedReturnTo` parameter compared to `openid.return_to` (exact); try/catch around the Steam POST returning `null` with `logger` (note: class currently has no `ILogger`; if added, extend primary ctor); wrap `steam.GetPlayerSummaryAsync` so failure yields `null` summary (existing code already tolerates `summary?.`). Error-handling convention to copy (CLAUDE.md): `catch (Exception ex) { logger.LogWarning(ex, "message"); }`. `GetRealm`/`BuildRedirectUrl` builds callback URL from `callbackUrl`; controller should share one helper for both `SteamLogin` and the callback.

### `GamePlanner.API/Controllers/AuthController.cs` (controller; modify)

**Analog:** itself — Steam endpoints near line 65 (`302` to `{App:FrontendUrl}/login?error=steam_failed`). Executor must Read the file (not read in this pass) to extract the callback-URL construction in `SteamLogin`, then reuse it in the callback.

---

### Frontend specs (`game-planner-ui/src/app/**/*.spec.ts`)

**Analog:** none in repo (zero existing specs). Source code under test:

`auth.interceptor.ts` lines 7-25 (class-based, DI-registered; ctor takes `AuthService`; calls `getToken()` and `logout()`):
```ts
const authReq = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
return next.handle(authReq).pipe(
  catchError((err: HttpErrorResponse) => {
    if (err.status === 401) this.auth.logout();
    return throwError(() => err);
  })
);
```
`auth.guard.ts` lines 5-14 (`providedIn: 'root'`, ctor `(AuthService, Router)`):
```ts
canActivate(): boolean {
  if (this.auth.isLoggedIn()) return true;
  this.router.navigate(['/login']);
  return false;
}
```
`game-list.component.ts` lines 131-137:
```ts
deleteGame(id: number) {
  this.error = '';
  this.gameService.deleteGame(id).subscribe({
    next: () => this.loadGames(),
    error: () => { this.error = 'Could not delete game. Please try again.'; }
  });
}
```
Copy spec shapes verbatim from RESEARCH.md Pattern 7 (spy objects via `jasmine.createSpyObj`, `HTTP_INTERCEPTORS` + `provideHttpClient(withInterceptorsFromDi())` + `provideHttpClientTesting()`, `new GameListComponent(svc)` without TestBed). Conventions: 2-space indent, single quotes, spec file next to source, `describe`/`it`. Add to `package.json` scripts: `"test:ci": "ng test --watch=false --browsers=ChromeHeadless"`.

---

### `Database/38_FixHalfLifeIgdbId.sql` (migration, CRUD)

**Analogs:** `Database/37_SeedWorkerStatus_SteamTokenCleanup.sql` (idempotent guard + `USE`/`GO` layout) and script 36 (audit columns; `ModifiedAt` convention).
```sql
USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM WorkerStatus WHERE Name = 'SteamTokenCleanupWorker')
    INSERT INTO WorkerStatus (Name) VALUES ('SteamTokenCleanupWorker');
GO
```
Add: `SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON;` before the UPDATE (filtered indexes on `Games`), guard `WHERE Id = <wrong> AND UserId = 2 AND IGDBId = 267639` so a re-run is a no-op, set `ModifiedAt = SYSUTCDATETIME()`, `IGDBUpdatedAt = NULL`, and a header comment plus commented `-- REVERSAL:` block holding the old values. Correct IGDBIds must be verified first (RESEARCH A1/A2) and confirmed with the user before applying.

---

### `e2e/` (Playwright package) — no analog

Use RESEARCH.md Pattern 6 (config, `globalSetup`, `channel: 'chrome'`, `ignoreHTTPSErrors`, serial, env-only credentials, refuse any email other than `uat-test@gameplanner.local`) and the "Worker trigger check" and selector list in "Code Examples". Frontend string anchors come from 05-UI-SPEC.md Copywriting Contract. `.gitignore` additions follow the existing sections (it already ignores `.env`, `node_modules/`):
```
# Environment
.env
.env.local
.env.*.local
```
Append `e2e/.auth/`, `e2e/playwright-report/`, `e2e/test-results/`.

## Shared Patterns

### Test hermeticity
**Source:** RESEARCH.md Pattern 1; `GameManagerAtomicityTests.cs` lines 19-31
**Apply to:** all backend tests. Pure/in-memory tests need no DB; DB tests use the dev SQL Server via the `ConnectionStrings__DefaultConnection` env override with the Windows-auth fallback, prefix rows, and clean up in `finally`. In-process host tests use `Testing` environment, test `Jwt:Secret`, and `RemoveAll<IHostedService>()`.

### Logging and error handling in API code touched by fixes
**Source:** CLAUDE.md conventions
**Apply to:** `SteamAuthService` and `AuthService` changes
```csharp
catch (Exception ex) { logger.LogWarning(ex, "message"); }
```
Structured named parameters; never log secrets or full URLs containing `key=`.

### Writes and reads
**Source:** CLAUDE.md
**Apply to:** `38_*.sql` (manual one-off, applied by hand; app-side writes stay in stored procedures, reads in EF LINQ).

### Git hygiene
**Apply to:** all plans. Unrelated WIP exists in `GamePlanner.API/Managers/GameImageManager.cs` and `game-planner-ui/src/app/components/game-detail/game-detail.component.{ts,html,css}`. Stage explicit paths only; never `git add -A`. Do not touch any `.html`/`.css`/template files (UI-SPEC Visual Change Lock).

### Secrets
**Apply to:** e2e and tests. Credentials from env only (`e2e/.env`, gitignored, with committed `.env.example` empty values); test secrets in tests are obvious constants like `TEST-STEAM-KEY-DO-NOT-LOG`; gitleaks hook is active.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Support/ApiFactory.cs` | test-support | request-response | No integration-test host exists; `Program` has no partial class yet. Use RESEARCH Pattern 1 |
| `Support/RecordingHandler.cs`, `StubHttpClientFactory.cs` | test-support | request-response | No HTTP stubs in repo. Use RESEARCH Pattern 3 |
| `Support/CapturingLoggerProvider.cs` | test-support | event-driven | No log capture exists. Use RESEARCH Pattern 4 (include scopes via `IExternalScopeProvider`) |
| All three Angular `*.spec.ts` | test | request-response | Zero specs in the Angular app. Use RESEARCH Pattern 7 |
| `e2e/*` | test/config | request-response | No Playwright config committed (earlier phases used MCP ad hoc). Use RESEARCH Pattern 6 |

## Metadata

**Analog search scope:** `GamePlanner.Tests/`, `GamePlanner.API/Services/`, `GamePlanner.API/Program.cs`, `GamePlanner.API/appsettings.json`, `Database/`, `game-planner-ui/src/app/{interceptors,guards,components/game-list}`, `.gitignore`
**Files scanned:** ~16 (partial reads of larger files; `AuthController.cs` not read, executor should read it)
**Pattern extraction date:** 2026-10-24
