---
phase: 05-regression-testing-full-stack-verification
plan: 04
subsystem: auth
tags: [steam-openid, jwt, fail-closed, xunit, trust-boundary]
status: complete
requirements: [TEST-01]

requires:
  - phase: 05-regression-testing-full-stack-verification
    provides: "ApiFactory hermetic host, RecordingHandler, StubHttpClientFactory, CapturingLoggerProvider (05-01)"
provides:
  - "First-time Steam login (user without email) works end to end: callback -> code -> exchange -> JWT"
  - "SteamAuthService relying-party checks: return_to binding, mode, ns, op_endpoint, required signed fields, line-exact is_valid:true"
  - "Fail-closed Steam verification and best-effort player summary"
  - "SteamAuthServiceTests: 26 cases (unit, DB-backed, controller-level)"
affects: [05-06]

tech-stack:
  added: []
  patterns: ["Local OpenID field checks before any network call, fixed-reason Warning per rejection", "Direct construction of SteamAuthService with StubHttpClientFactory for rejection matrix"]

key-files:
  created:
    - GamePlanner.Tests/SteamAuthServiceTests.cs
    - .planning/todos/pending/2026-10-24-steam-openid-followups.md
  modified:
    - GamePlanner.API/Services/AuthService.cs
    - GamePlanner.API/Services/SteamAuthService.cs
    - GamePlanner.API/Controllers/AuthController.cs

key-decisions:
  - "Email claim emitted only when Users.Email is non-empty; this also removes pre-existing warning CS8604 at AuthService.cs"
  - "Rejections log fixed reason strings only; the one exception is the openid.signed name list, regex-sanitized (letters, underscore, dot, comma, max 200) or replaced by <malformed>"
  - "Callback URL has a single source (AuthController.SteamCallbackUrl) used by both the redirect to Steam and the return_to verification"

patterns-established:
  - "Probe Steam users use a random fake SteamId (7656119 + 10 digits) and are deleted in finally; tests that could accidentally accept an assertion also use a fake id"

metrics:
  tasks: 2
  commits: [b604ef7, 51f047a]
  completed: 2026-10-24
---

# Phase 5 Plan 4: Steam OpenID trust-boundary fixes Summary

A first-time Steam login now completes (null-email users get a token with no email claim), and the callback verifies the assertion is addressed to this app, is signed over the fields it trusts, and fails closed on any Steam or HTTP failure, all locked by 26 targeted tests.

## Tasks

| Task | Name | Commit |
|------|------|--------|
| 1 (tracer) | Null-email token fix, return_to binding, single callback-URL source | b604ef7 |
| 2 | Fail-closed handling, strict OpenID field checks, rejection matrix, S5-S7 todo | 51f047a |

## Tracer evidence (D-09 S1, autonomous run)

- Before the fix: `SteamCallback_NewUserWithoutEmail_IssuesCodeThatExchangesForToken` failed with `ArgumentNullException: Value cannot be null (Parameter 'value')` thrown from `AuthService.GenerateToken` line 66 (`new Claim(ClaimTypes.Email, user.Email)`), reached via `SteamAuthService.HandleCallbackAsync` and `AuthController.SteamCallback` (the in-process host surfaced it as an unhandled exception, i.e. a 500 in production). The user row had already been persisted at that point. `GenerateToken_UserWithoutEmail_OmitsEmailClaim_KeepsSteamId` failed with the same exception.
- After the fix: all 3 Task 1 facts passed; `⚡ Tracer verified end-to-end — expanding`.
- Verify greps: `if (!string.IsNullOrEmpty(user.Email))`, `string expectedReturnTo`, `StringComparison.Ordinal`, `ILogger<SteamAuthService> logger`, `HandleCallbackAsync(Request.Query, SteamCallbackUrl)` present; `SteamCallbackUrl` appears 3 times and `/api/auth/steam/callback` once in `AuthController.cs`; zero probe Steam users remain in `Users`.

## D-09 review table (as delivered)

| ID | Finding | Severity | Disposition |
|----|---------|----------|-------------|
| S1 | `GenerateToken` threw for `Email = null`; user persisted, login 500 | high | Fixed (b604ef7); tracer fact + 2 unit facts |
| S2 | `openid.return_to` never verified (cross-relying-party replay) | medium | Fixed (b604ef7), ordinal compare to `SteamCallbackUrl` before any Steam call |
| S3 | Steam/HTTP failure -> raw 500; player-summary failure blocked login | medium | Fixed (51f047a): try/catch returns null -> `steam_failed` redirect; summary failure degrades to no profile data |
| S4 | SteamId from fields not confirmed signed; `is_valid` substring match | low | Fixed (51f047a): mode/ns/op_endpoint/signed-field checks, line-exact `is_valid:true` |
| S5 | No state/browser binding (login CSRF) | low | Logged, not built (todo) |
| S6 | No rate limit on `/steam/callback`, `/steam/exchange`; `?code=` in history | low | Logged, not built (todo) |
| S7 | Research: no unique index on `Users.SteamId` | low | Research premise was wrong: filtered `UX_Users_SteamId` exists. Residual: double submit gives a 500 from `DbUpdateException`. Logged (todo) |

## Verification

- `dotnet test GamePlanner.Tests/GamePlanner.Tests.csproj`: 61 passed, 0 failed (35 before this plan + 26 new), run twice consecutively.
- `dotnet build GamePlanner.sln`: 0 warnings, 0 errors (the pre-existing CS8604 is gone with the claim change).
- Zero probe Steam users remain in `Users` after the runs; the owner's row (SteamId `76561198115372977`) was never read or written.
- Mutation check for the S4 exact-line rule: temporarily restoring `body.Contains("is_valid:true")` made the three embedded-text cases fail; the rule was restored (file identical to the committed version).

## Deviations from Plan

**1. [Rule 1 - Test hygiene] Embedded-`is_valid` theory used a fixed SteamId and could leave a row**
- **Found during:** mutation check on Task 2
- **Issue:** with the S4 rule deliberately broken, the theory's fixed SteamId (`76561190000000001`) was accepted and left a `Users` row (Id 24), which then made two DB-backed facts fail with a `UQ_Users_Email` violation.
- **Fix:** deleted the stray probe row (Id 24, fake id, `Email IS NULL`) and switched the theory to a random fake SteamId with cleanup in `finally`. Cases that return before touching the DB keep the fixed id.
- **Files modified:** `GamePlanner.Tests/SteamAuthServiceTests.cs` (51f047a)

**2. [TDD note] Task 2 tests were written after the implementation**
- Task 2 is `tdd="true"` but the strict checks and the rejection matrix landed in one commit, so no separate RED commit exists. Compensated by the mutation check above. Task 1 did record a genuine pre-fix failure.

**3. [Plan premise corrected] S7**
- See table; documented in the todo.

## Findings outside scope (logged, not fixed)

`UQ_Users_Email` is an unfiltered UNIQUE constraint, so at most one user with `Email = NULL` can exist. The second distinct first-time Steam login would fail with a unique-key violation (500). Hidden today because the owner's Steam row has a synthetic `@steam.local` email. Needs a numbered SQL script, so recorded in `.planning/todos/pending/2026-10-24-steam-openid-followups.md` with fix options. Relevant to 05-06 H1: the owner's live login uses the existing row, so it is unaffected.

## Flagged assumptions carried forward

- **A4 (signed-field list):** `openid.signed` must contain `op_endpoint, claimed_id, identity, return_to, response_nonce`. Not confirmable offline; the owner's live Steam login (05-06 H1) is the confirmation. If Steam rejects, the Warning log contains the received `openid.signed` list (field names only).
- **Exact `return_to` match:** assumes Steam echoes `return_to` unchanged. Observed value to be recorded at the 05-06 live test if it differs.

## Known Stubs

None.

## Threat Flags

None. No new endpoints or trust boundaries; the changes tighten the existing callback.

## Self-Check: PASSED

- FOUND: GamePlanner.Tests/SteamAuthServiceTests.cs, .planning/todos/pending/2026-10-24-steam-openid-followups.md, GamePlanner.API/Services/SteamAuthService.cs
- FOUND commits: b604ef7, 51f047a
