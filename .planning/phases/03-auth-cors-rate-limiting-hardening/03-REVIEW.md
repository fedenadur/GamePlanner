---
phase: 03-auth-cors-rate-limiting-hardening
reviewed: 2026-08-31T12:17:53Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - GamePlanner.API/Controllers/GameJournalController.cs
  - GamePlanner.API/Controllers/GamesController.cs
  - GamePlanner.API/Controllers/SessionController.cs
  - GamePlanner.API/Managers/SessionManager.cs
  - GamePlanner.API/Managers/SessionPlannerManager.cs
  - GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs
  - GamePlanner.API/Program.cs
  - GamePlanner.API/Services/AuthService.cs
findings:
  critical: 0
  warning: 7
  info: 4
  total: 11
status: issues_found
---

# Phase 03: Code Review Report

**Reviewed:** 2026-08-31T12:17:53Z
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

Reviewed the auth/CORS/rate-limiting hardening changes. The core hardening work is sound: CORS is pinned to a single configured origin (fail-fast validated at startup), JWT validation now pins `HmacSha256` and validates issuer/audience/lifetime, and per-user mutation rate limiting is applied consistently across all POST/PUT/DELETE actions in the three reviewed controllers.

No critical/blocker-level vulnerabilities (injection, auth bypass, hardcoded secrets) were found in these 8 files. However, several warnings surfaced: an email-normalization inconsistency between register/login that can cause unhandled 500s or login mismatches, a timing side-channel that leaks user existence during login, missing password-strength validation, an unvalidated `Jwt:Secret` that violates the fail-fast pattern already applied to `App:FrontendUrl`, a request-validation gap in the session planner endpoint, the absence of a global exception handler, and an unrated external-API-triggering endpoint. These are all fixable without touching the already-hardened CORS/rate-limiting core.

## Warnings

### WR-01: Timing side-channel in login allows user enumeration

**File:** `GamePlanner.API/Services/AuthService.cs:39-51`
**Issue:** `LoginAsync` returns immediately (fast path, no BCrypt call) when the user is not found or inactive, but calls the deliberately slow `BCrypt.Net.BCrypt.Verify` when the user exists but the password is wrong. The measurable response-time difference lets an attacker enumerate valid registered emails by timing the response, independent of the rate limiter.
**Fix:** Always perform a constant-cost operation on both paths, e.g. verify against a precomputed dummy hash when the user isn't found:
```csharp
var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.IsActive);
var hashToCheck = user?.PasswordHash ?? DummyHashForTimingParity;
var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, hashToCheck);
if (user is null || user.PasswordHash is null || !passwordOk)
{
    logger.LogWarning(...);
    return null;
}
```

### WR-02: Inconsistent email normalization between register and login

**File:** `GamePlanner.API/Services/AuthService.cs:16, 21, 40`
**Issue:** `RegisterAsync` stores the email trimmed and lowercased (`request.Email.ToLower().Trim()`, line 21), but the pre-insert uniqueness check on line 16 (`u.Email == request.Email.ToLower()`) does not trim. `LoginAsync`'s lookup on line 40 also does not trim. Two consequences: (1) a registration whose email has leading/trailing whitespace can pass the uniqueness pre-check against an existing trimmed email, then fail on the DB's `UQ_Users_Email` unique constraint with an unhandled `DbUpdateException` (500) instead of the intended "email already taken" response; (2) a legitimate user who logs in with incidental whitespace around their email (common with copy/paste) will fail to match their own trimmed, stored record and be denied login.
**Fix:** Normalize consistently everywhere email is compared or stored:
```csharp
var normalizedEmail = request.Email.Trim().ToLower();
if (await db.Users.AnyAsync(u => u.Email == normalizedEmail)) return null;
...
Email = normalizedEmail,
```
and in `LoginAsync`:
```csharp
var normalizedEmail = request.Email.Trim().ToLower();
var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive);
```

### WR-03: No password strength/length validation before hashing

**File:** `GamePlanner.API/Services/AuthService.cs:14-24`
**Issue:** `RegisterAsync` hashes `request.Password` directly with no minimum length or complexity check (and `RegisterRequest.Password` has no `[Required]`/`[MinLength]` attributes in `Domain/AuthDto.cs`). A 1-character password is accepted. BCrypt also silently truncates input beyond 72 bytes, so overly long passwords provide no additional entropy beyond that point — this should be bounded explicitly rather than left implicit.
**Fix:** Add explicit validation (e.g. `[MinLength(8)]` on the DTO, or a manager-side check) before calling `BCrypt.HashPassword`, and cap accepted length (e.g. 128 chars) to avoid relying on BCrypt's silent truncation behavior.

### WR-04: `Jwt:Secret` is not validated at startup, unlike `App:FrontendUrl`

**File:** `GamePlanner.API/Program.cs:56-72`, `GamePlanner.API/Services/AuthService.cs:63`
**Issue:** Program.cs explicitly fail-fasts if `App:FrontendUrl` is missing (lines 117-119: throws `InvalidOperationException` with a clear message), but `jwt["Secret"]!` is dereferenced with the null-forgiving operator with no equivalent check, both in the `AddJwtBearer` configuration (Program.cs:68) and in `AuthService.GenerateToken` (line 63). If `Jwt:Secret` is missing/misconfigured, the failure surfaces later as an unhandled `ArgumentNullException` on the first authenticated request rather than at startup, which is inconsistent with the fail-fast pattern this phase established elsewhere.
**Fix:** Validate `Jwt:Secret` (and `Issuer`/`Audience` if required) at startup the same way `App:FrontendUrl` is validated:
```csharp
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException("Missing required configuration key 'Jwt:Secret'.");
```

### WR-05: `/api/session/plan` lacks the input validation `/api/session/recommend` has

**File:** `GamePlanner.API/Controllers/SessionController.cs:29-38`, `GamePlanner.API/Managers/SessionPlannerManager.cs:58, 89-92`
**Issue:** `Recommend` explicitly validates `AvailableMinutes`/`Mood` before calling into the manager (lines 19-20), but `Plan` performs no equivalent validation and passes the request straight through to `SessionPlannerManager.GetRecommendationsAsync`, which unconditionally calls `.Trim()` on `Mood`, `Duration`, `Perspective`, `GameLength` and `.Select(m => m.Trim()...)` over `GameModes` (lines 58, 89-92). While top-level non-nullable DTO properties are likely protected by ASP.NET Core's implicit-required model validation (`#nullable enable` + `[ApiController]`), a `GameModes` list containing a null element (e.g. `{"gameModes": ["solo", null]}`) is **not** caught by that mechanism — the list itself is non-null, so model validation passes, and `m.Trim()` on the null element throws an unhandled `NullReferenceException`, returning an unstructured 500.
**Fix:** Filter/validate list elements defensively, mirroring the guard already present in `Recommend`:
```csharp
var requestedModes = request.GameModes
    .Where(m => !string.IsNullOrWhiteSpace(m))
    .Select(m => m.Trim().ToLower())
    .ToList();
```

### WR-06: No global exception-handling middleware

**File:** `GamePlanner.API/Program.cs`
**Issue:** The pipeline (lines 129-145) has no `app.UseExceptionHandler(...)` (or equivalent problem-details middleware). Any unhandled exception — including the NRE paths noted in WR-05 — surfaces as a raw framework error response instead of a sanitized, consistent error contract, and in `Development` can leak full stack traces via the built-in developer exception page path if that were ever enabled outside dev.
**Fix:** Add centralized exception handling, e.g.:
```csharp
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");
```
or a custom `IExceptionHandler` registered via `AddExceptionHandler<T>()`, returning a consistent `ProblemDetails` response.

### WR-07: `GET /api/games/igdb/search` has no rate limiting

**File:** `GamePlanner.API/Controllers/GamesController.cs:42-58`
**Issue:** Every mutation endpoint in this phase was given `[EnableRateLimiting("mutations")]`, but `SearchIgdb` — which triggers an outbound call to the external IGDB API on every invocation via `igdb.SearchMultipleAsync` — has no rate limit at all. An authenticated user (or a compromised token) can hammer this endpoint to exhaust the shared IGDB API quota/token budget used by the whole application, which is exactly the class of risk this phase was meant to close for mutation endpoints.
**Fix:** Apply a read-oriented rate-limiting policy to this endpoint, e.g. a dedicated `"external-search"` fixed/sliding window policy scoped per user.

## Info

### IN-01: `GetUserId()` duplicated identically across three controllers

**File:** `GamePlanner.API/Controllers/GameJournalController.cs:62-66`, `GamePlanner.API/Controllers/GamesController.cs:100-104`, `GamePlanner.API/Controllers/SessionController.cs:40-44`
**Issue:** The exact same private `GetUserId()` implementation is copy-pasted in all three controllers. This is security-relevant identity-extraction logic; duplicating it increases the risk that a future fix (e.g. switching claim types, adding additional validation) is applied inconsistently.
**Fix:** Extract into a shared `ControllerBase` extension method or a common base controller, e.g. `this.User.GetUserId()`.

### IN-02: Dead/commented-out scoring code

**File:** `GamePlanner.API/Managers/SessionPlannerManager.cs:129-131`
**Issue:** `// Rating boost: +2 highly rated (≥80), +1 well rated (≥60)` and the two commented-out lines below it are leftover disabled code.
**Fix:** Remove if the rating boost is no longer wanted, or implement it if it was meant to ship.

### IN-03: `GetRecommendationsAsync` is a very long, high-complexity method

**File:** `GamePlanner.API/Managers/SessionPlannerManager.cs:36-168`
**Issue:** The method spans over 130 lines with multiple sequential filtering stages and a dense inline scoring lambda (lines 105-157) covering mood, duration, perspective, game mode, and length. This is difficult to unit test and reason about as a single unit.
**Fix:** Extract the scoring logic into a private `ScoreGame(Game g, ...)` helper method returning an `int`, and extract each filter stage into its own named method.

### IN-04: `CorsRejectionLoggingMiddleware` name implies enforcement but only logs

**File:** `GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs:5-18`
**Issue:** The middleware never blocks the request — it always calls `next(context)` regardless of origin mismatch; actual CORS enforcement happens later via `app.UseCors("AllowAngular")`. The class name ("RejectionLoggingMiddleware") is accurate but easy to misread as the enforcement point itself, especially since it runs before `UseCors` in the pipeline.
**Fix:** Consider a name/comment clarifying this is observability-only, e.g. `CorsMismatchAuditMiddleware`, and a one-line comment noting actual enforcement is `UseCors` further down the pipeline.

---

_Reviewed: 2026-08-31T12:17:53Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
