---
phase: 03-auth-cors-rate-limiting-hardening
verified: 2026-08-31T12:30:00Z
status: passed
score: 6/8 must-haves verified
behavior_unverified: 2
overrides_applied: 0
behavior_unverified_items:

  - truth: "ROADMAP success criterion 4 — the real, post-Phase-1 Angular SPA can call every endpoint it needs against the hardened API with no CORS errors, confirmed by a full manual browser walkthrough"
    test: "Log in fresh in the browser, then walk every major workflow (login, backlog CRUD, session planner, journal entries, IGDB/Steam/HLTB sync views) with browser DevTools console open"
    expected: "No CORS error appears in the browser console on any request across any workflow"
    why_human: "This requires visually driving the real Angular SPA in a browser and reading the DevTools console — the underlying mechanism (CORS policy correctness) was proven independently via curl-based preflight checks in this verification pass, but the criterion's own wording specifically demands a browser walkthrough, which the phase's own plan (D-08) deferred to end-of-phase human verification and which was never executed by any plan in this phase"

  - truth: "Concurrent in-scope mutation requests from the SAME user are counted atomically against a single partition with no lost updates under parallel load (03-01-PLAN.md must_haves, verification: backstop)"
    test: "Fire N concurrent mutation requests from one authenticated user's token against a mutations-policy endpoint and confirm the partition's permit count decrements exactly N times with no race-condition over/under-count"
    expected: "The FixedWindowRateLimiter partition for that user's key is decremented exactly once per concurrent request, with no lost updates"
    why_human: "Marked verification: backstop in the plan frontmatter — the planner explicitly flagged this as non-inferable from sequential testing. This verification pass ran only sequential (not concurrent/parallel) requests against the rate limiter; ASP.NET Core's FixedWindowRateLimiter is documented as internally thread-safe, but no concurrent-load evidence was gathered here to confirm it empirically for this app's specific partitioning wiring"
---

# Phase 3: Auth, CORS & Rate Limiting Hardening Verification Report

**Phase Goal:** The JWT auth flow, CORS policy, and rate limiting are deliberately configured for the app's real trust boundary, and security-relevant rejections are visible in logs.
**Verified:** 2026-08-31T12:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

All must-haves were independently re-proven against a live running instance of the API in this verification pass (not just re-reading SUMMARY.md claims) — SQL Server was confirmed running, the API was started fresh (`dotnet run`), two throwaway accounts were registered, and every behavioral claim below was re-executed via curl and the resulting log lines inspected directly, then the process was stopped and all scratch files removed.

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Issued JWTs expire after a deliberate, bounded lifetime (24h) instead of 7 days (AUTH-01) | VERIFIED | Code: `AuthService.cs:79` `expires: DateTime.UtcNow.AddHours(24)`, `grep -c 'AddDays(7)'` = 0. Live: decoded `exp` claim of a freshly issued token was 86394s ahead of now (target 86400±120s) |
| 2 | `TokenValidationParameters` explicitly validates issuer, audience, lifetime, and algorithm; a tampered token is rejected (AUTH-02) | VERIFIED | Code: `Program.cs:59-71` sets `ValidateIssuer/Audience/Lifetime/IssuerSigningKey = true`, `RequireExpirationTime = true`, `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]`. Live: `GET /api/games` with a signature-tampered bearer token returned `401`; the same token untampered returned `200` |
| 3 | CORS allowed origin loads from `App:FrontendUrl` config (no hardcoded `localhost`), restricted to explicit headers/methods, no `AllowAnyHeader`/`AllowAnyMethod`/`AllowCredentials` (NET-01, NET-02) | VERIFIED | Code: `Program.cs:117-127` — origin from config, `WithHeaders("Authorization","Content-Type")`, `WithMethods("GET","POST","PUT","DELETE")`. `grep -c` for `AllowAnyHeader`/`AllowAnyMethod`/`AllowCredentials`/`http://localhost:4200` in `Program.cs` = 0 for each. Live: preflight from `http://localhost:4200` returned `Access-Control-Allow-Headers: Authorization,Content-Type` and `Access-Control-Allow-Methods: GET,POST,PUT,DELETE`; preflight from `http://evil.example` returned **no** `Access-Control-*` headers at all |
| 4 | The real, upgraded Angular SPA can call every endpoint it needs with no CORS errors, confirmed by a full manual browser walkthrough | PRESENT_BEHAVIOR_UNVERIFIED | See `behavior_unverified_items` above — mechanism proven via curl/preflight (truth 3), but the browser walkthrough itself (the criterion's literal wording) was never executed by any plan in this phase; explicitly deferred (D-08) and never closed |
| 5 | Rate limiting rejects excessive requests on all in-scope data-mutation endpoints (game/journal/session POST/PUT/DELETE), partitioned per JWT user ID, so one user's rejections don't affect another (NET-03) | VERIFIED | Code: `Program.cs:84-94` `AddPolicy<string>("mutations", ...)` partitioned on `ClaimTypes.NameIdentifier`, `PermitLimit = 100`, `Window = TimeSpan.FromHours(1)`. All 9 in-scope actions across `GamesController` (4), `GameJournalController` (3), `SessionController` (2) carry `[EnableRateLimiting("mutations")]`; no `[HttpGet]` action does. Live: 101 sequential `DELETE /api/games/999999999` requests from user A → requests 1-100 = `404`, request 101 = `429`; user B's identical request while A was rejected = `404` (unaffected); user A's next request still `429` |
| 6 | Failed logins, CORS rejections, and rate-limit rejections all appear in a central log stream (OBS-01) | VERIFIED | Code: `AuthService.cs:44,49` (`Failed login attempt for {Email}: {Reason}`), `CorsRejectionLoggingMiddleware.cs:12-14` (`CORS rejection: Origin {Origin}...`), `Program.cs:111` (`Rate limit rejected for {UserId}...`). Live: unknown-email and wrong-password login attempts each logged one distinct-reason warning (2 total), zero password/token leakage in the log; mismatched-origin request logged exactly one `CORS rejection` warning, no-Origin and matching-Origin requests logged zero; 101st mutation request logged one `Rate limit rejected` line (two total across the two 429s triggered) |
| 7 | The session-planner cross-user data leak is closed — both `SessionManager` and `SessionPlannerManager` scope `db.Games` by the caller's `UserId`; unauthenticated session requests are rejected | VERIFIED | Code: `SessionManager.cs:15` and `SessionPlannerManager.cs:39` both contain `g.UserId == userId`; `SessionController.cs` has `GetUserId()` + two `Unauthorized()` guards + two `EnableRateLimiting("mutations")` attributes. Live: `POST /api/session/plan` and `POST /api/session/recommend` for a zero-game account returned no-games responses (`{"games":[]}` / `game: null`); unauthenticated `POST /api/session/plan` returned `401` |
| 8 | Concurrent in-scope mutation requests from the same user are counted atomically with no lost updates under parallel load (plan 03-01 backstop truth) | PRESENT_BEHAVIOR_UNVERIFIED | See `behavior_unverified_items` above — only sequential requests were exercised in this and the executor's own verification; no concurrent-load evidence gathered |

**Score:** 6/8 truths verified (2 present, behavior-unverified — routed to human verification)

### Judgment-Tier Prohibitions (non-authoritative LLM review — human sign-off still required)

Per the phase's plans, five prohibitions are marked `verification: judgment`, `status: unverified` in PLAN frontmatter. This verifier reviewed the codebase against each and found no violation, but per policy these route to the end-of-phase human checkpoint rather than being silently marked passed:

| # | Prohibition | Source | LLM-judge assessment |
|---|---|---|---|
| 1 | MUST NOT introduce sliding expiration, activity-based renewal, or a refresh token that defeats the 24h bound | 03-01 | No sliding-expiration or refresh-token code found anywhere in `AuthService.cs` or `AuthController.cs`; expiry is a fixed `AddHours(24)` at issuance |
| 2 | MUST NOT fall back to a permissive CORS configuration when `App:FrontendUrl` is absent/empty | 03-01 | `Program.cs:117-119` throws `InvalidOperationException` naming the key; no default/fallback origin exists in the code |
| 3 | MUST NOT widen the CORS allow-list beyond the four verbs/two headers | 03-01 | `Program.cs:125-126` allow-lists exactly `Authorization`, `Content-Type` and `GET/POST/PUT/DELETE`; no wildcard or additional entries |
| 4 | MUST NOT let the mutation limiter collapse into a single shared partition | 03-01 | Live-proven: user A's rejections did not affect user B (Truth 5 above) |
| 5 | MUST NOT let the two failed-login log branches become distinguishable in the HTTP response | 03-03 | `AuthController.cs:41` — exactly one `Unauthorized("Invalid email or password.")`, `grep -c` = 1; both `LoginAsync` branches return this same shape |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `GamePlanner.API/Program.cs` | `mutations` policy, `OnRejected` logging, hardened JWT params, config-sourced CORS, reordered pipeline | VERIFIED | All elements present and wired (see truths 1-6 above); build succeeds |
| `GamePlanner.API/Services/AuthService.cs` | 24h JWT lifetime, failed-login logging | VERIFIED | `AddHours(24)` present, `AddDays(7)` absent, `ILogger<AuthService>` injected, two distinct `LogWarning` branches |
| `GamePlanner.API/Controllers/GamesController.cs` | Mutations policy on all 4 write actions | VERIFIED | `CreateFromIgdb`, `Create`, `Update`, `Delete` all carry `[EnableRateLimiting("mutations")]`; GET actions do not |
| `GamePlanner.API/Controllers/GameJournalController.cs` | Mutations policy on all 3 write actions | VERIFIED | `CreateEntry`, `UpdateEntry`, `DeleteEntry` all carry the attribute; `using` import present |
| `GamePlanner.API/Controllers/SessionController.cs` | `GetUserId()`, `Unauthorized` guards, mutations policy on both actions | VERIFIED | Both `Recommend` and `Plan` resolve `userId`, guard with `Unauthorized()`, and carry the attribute |
| `GamePlanner.API/Managers/SessionManager.cs` | User-scoped recommendation query | VERIFIED | `g.UserId == userId` present in the `db.Games` predicate |
| `GamePlanner.API/Managers/SessionPlannerManager.cs` | User-scoped session-planner query | VERIFIED | `g.UserId == userId` present in the `db.Games` predicate |
| `GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs` | Origin-mismatch observation middleware, min 15 lines | VERIFIED | File exists (19 lines), namespace, guard, log template, and `await next(context)` all present; no response mutation |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `Program.cs` mutations partitioner | `HttpContext.User` NameIdentifier claim | `app.UseAuthentication()` before `app.UseRateLimiter()` | WIRED | `Program.cs:139-141` order is `UseCors → UseAuthentication → UseRateLimiter`; live-proven the partition key is a real user id, not `anonymous` (log showed `Rate limit rejected for 13 on ...`, matching the registered user's numeric id) |
| `GamesController.cs`/`GameJournalController.cs`/`SessionController.cs` | `Program.cs` mutations policy | `[EnableRateLimiting("mutations")]` | WIRED | Live-proven: 429 fired at request 101 on the bound endpoint and on a second bound endpoint (`SessionController.Plan`) once the same user's partition was exhausted — confirms all bound endpoints share one partition per user, as designed |
| `Program.cs` CORS policy | `appsettings.json App:FrontendUrl` | `builder.Configuration["App:FrontendUrl"]` | WIRED | Live-proven: preflight from the configured value (`http://localhost:4200`) succeeds; any other origin gets no `Access-Control-Allow-Origin` |
| `Program.cs` | `CorsRejectionLoggingMiddleware` | `app.UseMiddleware<...>()` between `UseHttpsRedirection` and `UseCors` | WIRED | Live-proven: mismatched-origin GET/OPTIONS requests logged exactly one warning each; no-Origin and matching-Origin requests logged zero |
| `SessionController.cs` | `SessionManager.cs` / `SessionPlannerManager.cs` | `userId.Value` passed after `Unauthorized` guard | WIRED | Live-proven: zero-game account got a no-games response from both endpoints, not another account's rows |

### Behavioral Spot-Checks (live, run by this verifier)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| JWT 24h expiry | Decode `exp` of a fresh login token | delta = 86394s (target 86400±120s) | PASS |
| Tampered-signature rejection | `GET /api/games` with mangled bearer token | `401` | PASS |
| Untampered token accepted | `GET /api/games` with valid token | `200` | PASS |
| CORS preflight, allowed origin | `OPTIONS /api/games` with `Origin: http://localhost:4200` | `Allow-Headers: Authorization,Content-Type`; `Allow-Methods: GET,POST,PUT,DELETE` | PASS |
| CORS preflight, disallowed origin | `OPTIONS /api/games` with `Origin: http://evil.example` | No `Access-Control-*` headers | PASS |
| Rate-limit boundary | 101 sequential `DELETE /api/games/999999999` as user A | 1-100 = `404`, 101 = `429` | PASS |
| Rate-limit partition isolation | User B's identical request while A is rejected | `404` (unaffected) | PASS |
| Rate-limit rejection logging | Grep log after 429s | 2× `Rate limit rejected for 13 on /api/games/999999999 by policy mutations` | PASS |
| Failed-login logging (unknown email) | `POST /api/auth/login` unregistered email | `401`; log: `Failed login attempt for ...: user not found or inactive` | PASS |
| Failed-login logging (wrong password) | `POST /api/auth/login` registered email, wrong password | `401`; log: `Failed login attempt for ...: invalid password` | PASS |
| Successful login logs nothing | `POST /api/auth/login` correct credentials | `200`; `Failed login attempt` count unchanged | PASS |
| No password leakage | Grep log for both submitted passwords | 0 matches | PASS |
| CORS-rejection logging (mismatched Origin) | `GET /api/games` with `Origin: http://evil.example` | +1 `CORS rejection` log line | PASS |
| CORS-rejection logging (no Origin) | `GET /api/games` no Origin header | +0 `CORS rejection` log lines | PASS |
| CORS-rejection logging (matching Origin) | `GET /api/games` with `Origin: http://localhost:4200` | +0 `CORS rejection` log lines | PASS |
| Session endpoint user-scoping | `POST /api/session/plan` / `/recommend` for empty-library account | No-games responses, no other account's rows | PASS |
| Session endpoint auth guard | `POST /api/session/plan` with no Authorization header | `401` | PASS |
| Build | `dotnet build GamePlanner.sln` | 0 errors, 1 pre-existing unrelated nullable warning | PASS |

All checks above were run against a freshly started instance of the API in this verification pass (SQL Server confirmed running, `dotnet user-secrets` confirmed seeded), not inferred from SUMMARY.md claims. All temporary throwaway accounts, log files, and scratch artifacts created during this pass were deleted afterward; nothing was committed.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|---|---|---|---|---|
| AUTH-01 | 03-01 | JWT lifetime reduced from 7 days to a deliberate, bounded value | SATISFIED | Truth 1 |
| AUTH-02 | 03-01 | `TokenValidationParameters` explicitly reviewed and configured | SATISFIED | Truth 2 |
| NET-01 | 03-01 | CORS allowed origins load from environment-based config | SATISFIED | Truth 3 |
| NET-02 | 03-01 | CORS restricted to explicit headers/methods | SATISFIED | Truth 3 |
| NET-03 | 03-01, 03-02 | Rate limiting extended to data-mutation endpoints, partitioned by JWT user ID | SATISFIED | Truths 5, 7 |
| OBS-01 | 03-01, 03-03 | Security-relevant events logged centrally | SATISFIED | Truth 6 |

No orphaned requirements: REQUIREMENTS.md's Phase 3 traceability row set (`AUTH-01, AUTH-02, NET-01, NET-02, NET-03, OBS-01`) matches exactly the union of `requirements:` fields declared across the three plans.

### Anti-Patterns Found

None. `grep` for `TBD|FIXME|XXX|TODO|HACK|PLACEHOLDER` across all 8 files modified/created by this phase returned zero matches. No stub return values, no empty handlers, no hardcoded-empty data flowing to a response.

A pre-existing, independently-run code review (`03-REVIEW.md`, standard depth, 2026-08-31) flagged 7 warnings and 4 info items on these same files — none critical/blocker, and none are must-haves this phase's plans committed to. Notable for context (not phase-blocking):

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| `AuthService.cs:39-51` | Timing side-channel: fast-path return on unknown-email vs. slow BCrypt call on wrong-password lets an attacker time-enumerate registered emails | Info (out of phase scope) | Not a must-have of this phase; the phase's OBS-01 logging work makes the same distinction operator-visible by design, but the phase's own prohibitions never asked for constant-time comparison |
| `Program.cs` | `Jwt:Secret` dereferenced with `!` and not fail-fast validated at startup, unlike `App:FrontendUrl` | Info (out of phase scope) | Inconsistent with this phase's own fail-fast pattern, but not one of the phase's must-haves |
| `GamesController.cs:42-58` | `GET /api/games/igdb/search` has no rate limit despite triggering outbound IGDB calls | Info (out of phase scope) | Explicitly out of scope — D-09 scopes NET-03 to POST/PUT/DELETE only |

These are legitimate follow-up candidates but do not block this phase's goal, which was scoped to auth/CORS/rate-limiting/observability, not general auth hardening.

### Human Verification Required

1. **Full manual browser walkthrough of the upgraded Angular SPA against the hardened API (ROADMAP success criterion 4, D-08)**
   **Test:** Log in fresh in the browser (to avoid a stale-token false positive per RESEARCH.md Pitfall 4), then exercise every major workflow — login, backlog CRUD, session planner, journal entries, IGDB/Steam/HLTB sync views — with the browser DevTools console open.
   **Expected:** No CORS error appears in the console on any request across any workflow.
   **Why human:** This is the criterion's own literal wording and was explicitly deferred by the phase's plan (D-08) to end-of-phase human verification. It was never executed by any of the three plans; only the underlying mechanism (CORS policy shape) was proven programmatically here.

2. **Concurrent-request atomicity of the per-user rate-limit partition (03-01-PLAN.md backstop truth)**
   **Test:** Fire a burst of concurrent (parallel, not sequential) mutation requests from the same authenticated user against a mutations-policy endpoint near the 100-permit boundary.
   **Expected:** The partition's permit count is decremented exactly once per request with no lost updates — i.e., no more than 100 succeed and no fewer than 100 succeed under concurrent load.
   **Why human:** Marked `verification: backstop` in the plan — the planner flagged this as non-inferable from sequential testing, and only sequential requests were exercised in both the executor's own verification and this pass.

3. **Sign-off on the five judgment-tier prohibitions listed above**
   **Test:** Review the "Judgment-Tier Prohibitions" table's LLM-judge assessments against the cited code locations.
   **Expected:** Human confirms or overrides each assessment.
   **Why human:** Per project policy, `verification: judgment` prohibitions require explicit human resolution at the end-of-phase checkpoint rather than being silently marked passed by an automated verifier.

### Gaps Summary

No FAILED truths, no MISSING/STUB artifacts, and no NOT_WIRED key links were found — every must-have this phase's three plans committed to is present, substantive, and live-proven working end-to-end against a running instance. The phase's core engineering goal (JWT/CORS/rate-limiting reconfiguration with visible security logging) is achieved.

The remaining gap is procedural, not code-level: ROADMAP success criterion 4 explicitly requires a **manual browser walkthrough**, and no plan in this phase performed one — plan 03-01 deferred it to "end-of-phase verification" (D-08) and that verification never happened before this report. The mechanism it would confirm (correct CORS headers for the real frontend's actual request shape) is independently proven here via curl-based preflight checks, so the risk is low, but the criterion as literally written has not been closed. Combined with the plan's own flagged backstop item (concurrent-load atomicity) and the five judgment-tier prohibitions awaiting sign-off, this phase requires a short human verification pass before it can be marked fully passed.

---

_Verified: 2026-08-31T12:30:00Z_
_Verifier: Claude (gsd-verifier)_
