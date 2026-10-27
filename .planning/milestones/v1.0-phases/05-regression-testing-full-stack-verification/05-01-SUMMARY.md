---
phase: 05-regression-testing-full-stack-verification
plan: 01
subsystem: testing
tags: [xunit, webapplicationfactory, jwt, jwtbearer, hermetic-host, nuget-audit]
status: complete
requirements: [TEST-01]

requires:
  - phase: 03-auth-cors-rate-limiting-hardening
    provides: "JwtBearer validation parameters (HS256 pinned, issuer/audience/lifetime, 24h tokens)"
  - phase: 04-reliability-leak-fixes
    provides: "Transactional GameManager write paths"
provides:
  - "ApiFactory: hermetic in-process API host (env Testing, no workers, stubbed outbound HTTP, test-only secrets)"
  - "RecordingHandler, CapturingLoggerProvider, StubHttpClientFactory shared test support"
  - "AuthTokenTests: 24h issuance + 7-case rejection matrix decided by the real JwtBearer middleware"
  - "CreateFromIgdbAsync rollback fact (all three GameManager write paths covered)"
affects: [05-03, 05-04, 05-05]

tech-stack:
  added: ["Microsoft.AspNetCore.Mvc.Testing 8.0.32", "xunit 2.9.3 (bump from 2.5.3)"]
  patterns: ["WebApplicationFactory<Program> with UseEnvironment(Testing) + UseSetting for secrets", "ConfigureHttpClientDefaults primary handler stub", "remove only GamePlanner.API.Workers hosted services"]

key-files:
  created:
    - GamePlanner.Tests/Support/ApiFactory.cs
    - GamePlanner.Tests/Support/RecordingHandler.cs
    - GamePlanner.Tests/Support/CapturingLoggerProvider.cs
    - GamePlanner.Tests/Support/StubHttpClientFactory.cs
    - GamePlanner.Tests/AuthTokenTests.cs
  modified:
    - GamePlanner.API/Program.cs
    - GamePlanner.Tests/GamePlanner.Tests.csproj
    - GamePlanner.Tests/GameManagerAtomicityTests.cs

key-decisions:
  - "Jwt:Secret (and Steam:ApiKey, IGDB:ClientId, IGDB:ClientSecret) delivered via builder.UseSetting; Open Question 4 resolved, in-memory fallback not needed"
  - "Only hosted services in namespace GamePlanner.API.Workers are removed; the web host's own hosted service must survive"
  - "Production token validation untouched: Program.cs gains only 'public partial class Program { }'"

patterns-established:
  - "Hermetic host: Testing environment keeps user-secrets unloaded; test secrets are obvious constants"
  - "Rejection tests assert both 401 and WWW-Authenticate containing invalid_token, proving middleware rejection"

metrics:
  tasks: 2
  commits: [0337fd9, 4423c38]
  completed: 2026-10-24
---

# Phase 5 Plan 1: Hermetic API host and JWT contract tests Summary

In-process `ApiFactory : WebApplicationFactory<Program>` (env `Testing`, no workers, stubbed outbound HTTP, test-only secrets) now drives forged JWTs through the real JwtBearer middleware, locking the Phase 3 contract (24h issuance; expired, bad signature, wrong issuer/audience, HS512, `alg: none`, no-`exp` all 401 + `invalid_token`), plus the missing `CreateFromIgdbAsync` rollback fact.

## Tasks

| Task | Name | Commit |
|------|------|--------|
| 1 (tracer) | Hermetic in-process host, one authenticated request, 24h lifetime fact | 0337fd9 |
| 2 | StubHttpClientFactory, CreateFromIgdb rollback fact, full suite + NuGet scan | 4423c38 |

## Configuration mechanism (Open Question 4)

`builder.UseSetting("Jwt:Secret", ApiFactory.Secret)` worked: a token from the host's own `AuthService.GenerateToken` is accepted by the middleware, and `TestHost_IsHermetic_NoWorkersNoUserSecrets` asserts `IConfiguration["Jwt:Secret"] == ApiFactory.Secret`. The same mechanism delivers `Steam:ApiKey`, `IGDB:ClientId`, `IGDB:ClientSecret` test values. The `AddInMemoryCollection` fallback was not needed.

## Verification

- `dotnet test GamePlanner.Tests/GamePlanner.Tests.csproj`: 21 passed, 0 failed (5 original `GameManagerAtomicityTests` + new `CreateFromIgdbAsync_RollsBackInsertedGame_WhenSaveChangesThrows`, 4 `SteamTokenStoreSweepTests`, 11 `AuthTokenTests` cases).
- Flagged assumption A5 held: `xunit.runner.visualstudio` 2.5.3 discovers tests built against xunit 2.9.3; no runner bump needed.
- `git diff` of `Program.cs` vs. the phase base shows only the appended `public partial class Program { }`; 7 `AddHostedService`, `ValidAlgorithms`, `RequireExpirationTime` unchanged; no `ClockSkew`/`IsEnvironment` in `Program.cs`.
- `CreateFromIgdbAsync` genre sync runs inside the same transaction as `sp_InsertGame` (no Phase 4 REL-01 regression); the new fact confirms the inserted row rolls back.

### NuGet vulnerability scan (`dotnet list GamePlanner.Tests/GamePlanner.Tests.csproj package --vulnerable --include-transitive`)

```
El proyecto "GamePlanner.Tests" tiene los paquetes vulnerables siguientes
   [net8.0]:
   Paquete transitivo                           Resuelto   Gravedad   Dirección URL de asesoría
   > Microsoft.IdentityModel.JsonWebTokens      7.0.3      Moderate   https://github.com/advisories/GHSA-59j7-ghrg-fj52
   > System.IdentityModel.Tokens.Jwt            7.0.3      Moderate   https://github.com/advisories/GHSA-59j7-ghrg-fj52
```

No High or Critical. The two High findings (`System.Net.Http` 4.3.0, `System.Text.RegularExpressions` 4.3.0) are gone. Two Moderate transitive findings remain, inherited from the API project's `JwtBearer` 8.0.0 chain (IdentityModel 7.0.3); not in this plan's D-07 scope, handed to plan 05-05's dependency audit.

## Deviations from Plan

**1. [Ordering deviation] Task 2 JWT matrix and hermeticity work landed in Task 1's commit**
- **Found during:** Task 1 (tracer)
- **Issue:** `AuthTokenTests.cs` in 0337fd9 already contains the full Task 2 auth work: the 7-case rejection `[Theory]` (each asserting 401 and `WWW-Authenticate` containing `invalid_token`; expired uses `AddHours(-1)`; HS512 uses `ApiFactory.Secret`) and `TestHost_IsHermetic_NoWorkersNoUserSecrets`.
- **Impact:** Task 2's own commit (4423c38) contains only `StubHttpClientFactory.cs` and the `CreateFromIgdbAsync` rollback fact. All Task 2 acceptance criteria are met by the combined commits.
- **Commits:** 0337fd9, 4423c38

Otherwise: plan executed as written.

## Known Stubs

None.

## Threat Flags

None. The only production change is a declaration-only partial type (T-05-04 accepted).

## Issues Encountered

An unrelated pre-existing compiler warning CS8604 in `AuthService.cs` line 69 (`Claim` value maybe null on `user.Email`) was observed; out of scope here (plan 05-04 changes the email claim handling).

## Self-Check: PASSED

- FOUND: GamePlanner.Tests/Support/StubHttpClientFactory.cs, ApiFactory.cs, RecordingHandler.cs, CapturingLoggerProvider.cs, GamePlanner.Tests/AuthTokenTests.cs
- FOUND commits: 0337fd9, 4423c38
