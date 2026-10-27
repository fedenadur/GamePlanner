---
phase: 05-regression-testing-full-stack-verification
reviewed: 2026-10-24T00:00:00Z
depth: standard
files_reviewed: 26
files_reviewed_list:
  - .gitignore
  - Database/38_FixHalfLifeIgdbId.sql
  - GamePlanner.API/Controllers/AuthController.cs
  - GamePlanner.API/Program.cs
  - GamePlanner.API/Services/AuthService.cs
  - GamePlanner.API/Services/SteamAuthService.cs
  - GamePlanner.API/appsettings.json
  - GamePlanner.Tests/AuthTokenTests.cs
  - GamePlanner.Tests/GameManagerAtomicityTests.cs
  - GamePlanner.Tests/GamePlanner.Tests.csproj
  - GamePlanner.Tests/IgdbServiceContractTests.cs
  - GamePlanner.Tests/SteamAuthServiceTests.cs
  - GamePlanner.Tests/SteamServiceContractTests.cs
  - GamePlanner.Tests/Support/ApiFactory.cs
  - GamePlanner.Tests/Support/CapturingLoggerProvider.cs
  - GamePlanner.Tests/Support/RecordingHandler.cs
  - GamePlanner.Tests/Support/StubHttpClientFactory.cs
  - e2e/.env.example
  - e2e/global-setup.ts
  - e2e/package.json
  - e2e/playwright.config.ts
  - e2e/tests/uat.spec.ts
  - game-planner-ui/package.json
  - game-planner-ui/src/app/components/game-list/game-list.component.spec.ts
  - game-planner-ui/src/app/guards/auth.guard.spec.ts
  - game-planner-ui/src/app/interceptors/auth.interceptor.spec.ts
findings:
  critical: 0
  warning: 10
  info: 5
  total: 15
status: issues_found
---

# Phase 5: Code Review Report

**Reviewed:** 2026-10-24
**Depth:** standard
**Files Reviewed:** 26 (e2e/.env.example was permission-denied for reading; its gitignore handling was verified instead)
**Status:** issues_found

## Summary

The Steam OpenID hardening (mode/ns/op_endpoint/return_to/signed-field checks, strict `is_valid:true` line match, fail-closed try/catch) is sound for the claims it makes, with no auth bypass found. The test suites are broadly well constructed. Remaining concerns: replay protection is fully delegated to Steam; the rate-limit design is weak (the "auth" limiter is global rather than per client, and the Steam endpoints are unlimited); JWT-secret misconfiguration is not caught at startup; there are some pre-existing AuthService weaknesses in listed files; and there are several test-reliability defects, including a real regex bug in the e2e worker test and a DB test that writes into an arbitrary real user's library.

## Structural Findings (fallow)

None provided.

## Narrative Findings (AI reviewer)

## Warnings

### WR-01: Steam assertion has no RP-side nonce/freshness check (replay relies solely on Steam)

**File:** `GamePlanner.API/Services/SteamAuthService.cs:111-153`
**Issue:** `response_nonce` is required to be *signed* but is never parsed, timestamp-checked, or recorded. A captured callback URL (browser history, proxy/access logs, Referer, a shared screenshot) logs the victim in again if Steam's `check_authentication` accepts it a second time. OpenID 2.0 section 11.3 makes nonce uniqueness the RP's responsibility, and Steam's behavior here is not something to assume; the tests only mock Steam.
**Fix:** Parse the nonce and reject stale or reused ones:
```csharp
var nonce = query["openid.response_nonce"].ToString();           // 2026-10-24T22:00:00Zabc
if (nonce.Length < 21 || !DateTime.TryParse(nonce[..20], null,
        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var ts)
    || Math.Abs((DateTime.UtcNow - ts).TotalMinutes) > 5)
    return Reject("response_nonce is stale or malformed");
if (!seenNonces.TryAdd(nonce, DateTime.UtcNow))   // singleton ConcurrentDictionary with expiry cleanup
    return Reject("response_nonce already used");
```
Add a test for each branch.

### WR-02: Steam login, callback and exchange endpoints are not rate limited

**File:** `GamePlanner.API/Controllers/AuthController.cs:54-84`
**Issue:** Only `register` and `login` carry `[EnableRateLimiting("auth")]`.
- `steam/callback` is anonymous. Any request that passes the cheap local checks (trivially forgeable: right op_endpoint, return_to, signed list, any 17-digit id) triggers an outbound POST to steamcommunity.com, which makes it an unauthenticated amplification and resource-exhaustion path. Repeated abuse could get the server IP throttled by Steam, which would break legitimate Steam logins.
- `steam/exchange` is also unthrottled. The code is a 128-bit GUID, so guessing is infeasible, but the endpoint still allows free probing and load.

**Fix:** Add `[EnableRateLimiting("auth")]` to `SteamCallback` and `SteamExchange`, ideally with a per-IP partition (see WR-03).

### WR-03: "auth" rate limiter is one global bucket; one client can lock everyone out of login

**File:** `GamePlanner.API/Program.cs:77-83` (also `85-95`)
**Issue:** `AddFixedWindowLimiter("auth", ...)` is not partitioned, so all callers share 10 requests per minute. Any anonymous client sending 10 bad logins a minute denies login and registration to every user, while doing nothing to slow per-account or per-IP brute force. The "mutations" policy has a similar flaw: every unauthenticated request shares the single `"anonymous"` partition. Those endpoints are `[Authorize]`d, so this is mostly latent.
**Fix:** Partition by client IP:
```csharp
options.AddPolicy<string>("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
```
If the app runs behind a proxy, also configure `UseForwardedHeaders`.

### WR-04: No startup validation of Jwt:Secret (and issuer/audience)

**File:** `GamePlanner.API/Program.cs:66-72` (also `AuthService.cs:63`)
**Issue:** `Program.cs` fails fast on a missing `App:FrontendUrl` but not on `Jwt:Secret`. `appsettings.json` ships `"Secret": ""`, and the code uses `jwt["Secret"]!`.
- With the secret unset (the default outside Development, per CLAUDE.md), the failure is deferred to the first authenticated or login request. Depending on the Microsoft.IdentityModel version it throws or builds a degenerate key, so the user sees a 500 instead of a boot failure.
- A short secret is never rejected. HS256 should have at least 256 bits (32 bytes).

**Fix:** Validate once, next to the FrontendUrl check:
```csharp
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("'Jwt:Secret' must be configured with at least 32 bytes.");
```

### WR-05: Registration duplicate check bypassable via untrimmed email; display-name address format accepted

**File:** `GamePlanner.API/Services/AuthService.cs:16-21`, `GamePlanner.API/Controllers/AuthController.cs:46-50`
**Issue (pre-existing, in a listed file):**
- The uniqueness probe compares the untrimmed `request.Email.ToLower()`, but the stored value is `ToLower().Trim()`. Registering `" a@b.com"` when `a@b.com` exists passes the probe. The result is a duplicate account, or an unhandled DbUpdateException (500) if a unique index exists.
- `LoginAsync` also does not trim.
- `new MailAddress(...)` accepts `"Bob <a@b.com>"`, so that whole string would be stored as the email.

**Fix:** Normalize once (`var email = request.Email.Trim().ToLowerInvariant();`) in both Register and Login. In `IsValidEmail`, also require `new MailAddress(email).Address == email`.

### WR-06: Login timing side channel and raw-email logging

**File:** `GamePlanner.API/Services/AuthService.cs:39-51`
**Issue (pre-existing):**
- The unknown/inactive-user path returns without running BCrypt, so response time reveals whether an email is registered. The 401 body is generic, but the timing is not.
- The raw, attacker-controlled `request.Email` is logged on failure. Users commonly paste passwords into the email field, and CR/LF characters can forge log lines in sinks that don't escape them.

**Fix:**
- Run a dummy `BCrypt.Verify(request.Password, DummyHash)` when the user is null.
- Log a hashed, or truncated and sanitized, form of the email, or only the user id when it is known.

### WR-07: Latent regex bug in e2e worker timestamp parsing

**File:** `e2e/tests/uat.spec.ts:212`
**Issue:** `/[zZ]|[+-]dd:dd$/` uses a literal `dd` instead of `\d\d`, and the `[zZ]` alternative is unanchored. A timestamp with an explicit offset (`...+00:00`) is not recognized, so `Z` gets appended, `Date.parse` returns NaN, and `expect(NaN).toBeGreaterThanOrEqual(floor)` fails. It works today only because the API currently emits offset-less UTC.
**Fix:**
```ts
const asUtc = (s: string): number => Date.parse(/(Z|[+-]\d{2}:\d{2})$/i.test(s) ? s : `${s}Z`);
```

### WR-08: e2e delete test silently passes if the UI delete button regresses

**File:** `e2e/tests/uat.spec.ts:182-191`
**Issue:** The `if (await deleteBtn.count()) {...} else { delete via API }` fallback means a broken or removed `.delete-btn` still gives a green test, with only a stderr line as a trace. The test is titled "delete the game from its card", but the card-delete path is never actually asserted.
**Fix:** Assert that the button exists (`await expect(deleteBtn).toBeVisible()`). If IGDB-added games genuinely lack it, use `test.fixme` or `test.info().annotations` so the gap shows in the report, or delete through the UI on a manually created game.

### WR-09: GameManagerAtomicityTests write probe rows into an arbitrary real user's library

**File:** `GamePlanner.Tests/GameManagerAtomicityTests.cs:65,111,151,192,230,266`
**Issue:** `db.Users.Select(u => u.Id).FirstAsync()` has no `OrderBy`, so it is non-deterministic, and in practice it picks the owner's account (Id 2). The tests then create, update and delete rows in that user's library in the live dev DB. A crash between create and the `finally` leaves visible `gsd-probe-` rows in the owner's library. `SteamAuthServiceTests` explicitly promises that "the owner's row is never read", but these tests do not follow the same discipline. The UAT account (Id 17) exists for exactly this purpose.
**Fix:** Insert a dedicated probe user (as the Steam tests do with a fake SteamId), or use a fixed dedicated test user id, and delete it in cleanup. At minimum, add `OrderBy`, and document and guard that the connection string points at a dev database.

### WR-10: Steam API key log-redaction rests on one config line, and the test verifies config, not redaction

**File:** `GamePlanner.API/appsettings.json:6`, `GamePlanner.Tests/SteamServiceContractTests.cs:46-67`
**Issue:** The key travels in the query string, and the `System.Net.Http.HttpClient` category logs full URLs at Information and Trace. The only protection is `"System.Net.Http.HttpClient": "Warning"` in `appsettings.json`. Any environment-specific override re-leaks the key, for example an env var such as `Logging__LogLevel__System.Net.Http.HttpClient=Information` or `Logging__LogLevel__Default=Trace` set to diagnose prod, or a different `appsettings.{Env}.json`. The regression test runs only in the "Testing" environment, so it proves level filtering, not redaction, and raising the level would silently regress key hygiene.
**Fix:** Make redaction independent of log levels:
- Send the key in a header, if Steam supports it.
- Or strip/redact the query with a custom logging `DelegatingHandler` and remove the default logging handler (`services.RemoveAll<IHttpMessageHandlerBuilderFilter>()` plus a redacting replacement).
- Add a test that sets the category to Trace and still asserts no leak.

## Info

### IN-01: DbContext instances created in test rigs are never disposed

**File:** `GamePlanner.Tests/SteamAuthServiceTests.cs:140-166`
**Issue:** `Rig.Db` and the `LoggerFactory` are created per test but never disposed, so SQL connections linger until GC. Make `Rig` `IAsyncDisposable`, or use `await using`.

### IN-02: Test configuration builders and inline-secret setup duplicated

**File:** `GamePlanner.Tests/SteamAuthServiceTests.cs:63-74,150-158`, `AuthTokenTests.cs:57-64`
**Issue:** The same `ConfigurationBuilder`/Jwt dictionary appears three times, and `ConnectionString` appears in two test classes. Move both into `ApiFactory` or a `TestConfig` static class.

### IN-03: 38_FixHalfLifeIgdbId.sql silently no-ops and hard-codes row ids

**File:** `Database/38_FixHalfLifeIgdbId.sql:72-114`
**Issue:**
- When the `IF EXISTS (Id AND UserId AND IGDBId)` guard fails (already applied, or a different DB), the script commits with no output, so the operator can't tell "applied" from "skipped".
- If another UserId 2 row already holds IGDBId 231 or 233 and a unique `(UserId, IGDBId)` index exists, the UPDATE aborts the whole transaction. XACT_ABORT is on, so the rollback is correct, but the cause is unclear.

**Fix:** Add `PRINT`/`RAISERROR` (severity 10) in an `ELSE` branch for the skipped case, and a pre-check for 231/233 collisions.

### IN-04: Password length validated in characters, but BCrypt limit is 72 bytes

**File:** `GamePlanner.API/Controllers/AuthController.cs:22`
**Issue:** `Password.Length > 72` counts UTF-16 characters. A 72-character password containing multibyte characters is longer than 72 bytes, so BCrypt silently truncates or throws, depending on the library. Validate `Encoding.UTF8.GetByteCount(request.Password) <= 72` instead.

### IN-05: e2e/Angular tooling robustness

**File:** `e2e/playwright.config.ts:3`, `e2e/tests/uat.spec.ts:13`, `game-planner-ui/package.json:26-29`
**Issue:**
- `process.loadEnvFile` needs Node 20.12+, and no `engines` field is declared. On older Node the TypeError is swallowed by an empty catch, and the user later sees a misleading "UAT_PASSWORD is empty". Add `"engines": {"node": ">=20.12"}`.
- `.auth/uat.json` is read through a cwd-relative path, so running from the repo root breaks.
- `@angular-devkit/build-angular` and `@angular/cli` are still `^22.1.6` while the framework packages moved to `^22.2.3`. Keep the toolchain in step with the framework to avoid skewed lockfile resolution.

---

_Reviewed: 2026-10-24_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
