# Project Research Summary

**Project:** GamePlanner
**Domain:** Security hardening milestone on an existing ASP.NET Core 8 API plus Angular 13.3.0 SPA (brownfield, personal/local-use game tracking app with IGDB/Steam/HowLongToBeat integrations)
**Researched:** 2026-08-28
**Confidence:** MEDIUM-HIGH

## Executive Summary

This is a retrofit security-hardening pass on a working personal-use app, scoped tightly by PROJECT.md Active requirements and CONCERNS.md prior audit findings. The app has a specific, enumerable set of concrete gaps: credentials in URL query strings, secrets committed to appsettings.Development.json, a 7-day JWT with no explicit validation review, wide-open CORS, rate limiting only on /auth, a transactionally-unsafe SqlConnection/Dapper pattern in GameManager, unbounded SteamTokenStore growth, no audit trail, and an Angular 13 frontend unpatched since April 2023. Every fix is low-to-medium complexity individually. The main risk is not technical difficulty but compounding regressions from bundling unrelated changes together with zero pre-existing test coverage on either side of the stack.

The recommended approach uses almost entirely in-box tooling: ASP.NET Core 8 built-in RateLimiting middleware (already partially wired), Microsoft.Extensions.Http.Resilience only if reliability polish is desired (not required for security), dotnet user-secrets and env vars for secret management, and an IDbContextTransaction wrap around the existing Dapper calls in GameManager. No new abstractions, distributed caches, secrets manager, or MFA/RBAC/WAF are warranted. Architecturally, six independent changes map cleanly onto isolated component boundaries: the Services layer for credentials, Program.cs for CORS and rate-limiting, a new BackgroundService for token-store cleanup, GameManager for the transaction fix, and a stored-proc plus optional EF interceptor combo for audit fields. This gives the roadmap a natural phase structure.

The single biggest risk across all four research files is sequencing: treating IGDB and Steam credential fixes as identical when their auth contracts differ (IGDB needs form-body, Steam only supports query-param or header, never body); tightening CORS and rate-limiting with no test coverage, where failures are invisible server-side; and bundling the multi-major-version Angular 13-to-21 upgrade into the same milestone as backend policy changes, making regressions impossible to bisect. The mitigation for all three: isolate the Angular upgrade as its own phase verified against the unchanged permissive backend, then sequence backend hardening phases with CORS last, each phase ending in a manual full-SPA walkthrough.

## Key Findings

### Recommended Stack

Nearly the entire hardening effort uses existing, in-box dotNET 8 and Angular CLI capabilities rather than new dependencies. The one real addition is optional: Microsoft.Extensions.Http.Resilience 10.9.0 (Polly v8-based) for retry and circuit-breaker around IGDB, Steam, and HLTB calls, which is reliability polish rather than a security requirement and is a deferred item. The Angular upgrade must proceed sequentially through every major version (13 to 21) using ng update schematics; no version can be skipped and hand-editing package.json will not apply the code-mod schematics that make the upgrade safe.

Core technologies:
- Microsoft.AspNetCore.RateLimiting (built-in, ASP.NET Core 8) to extend the existing auth-only rate limiter to data-mutation endpoints, partitioned by JWT user-id claim, not IP
- dotnet user-secrets for dev, plus environment variables for anything closer to deployed, to replace committed secrets in appsettings.Development.json
- IDbContextTransaction (already part of the referenced EF Core package) to wrap the Dapper stored-proc call and EF SaveChangesAsync call in GameManager into one atomic transaction
- Angular CLI ng update schematics as the only supported path from Angular 13 to current LTS (20 or 21)

### Expected Features

Framed as hardening capabilities rather than user-facing features. All Table Stakes items map directly to an Active PROJECT.md requirement.

Must have (table stakes):
- Move IGDB token credentials to POST body; stop the Steam key from leaking via logs (Steam API has no body option, only query param or header)
- Remove secrets from committed config files
- Shorten JWT lifetime from 7 days to a deliberate bounded value; confirm explicit TokenValidationParameters
- Environment-based, restricted CORS (no AllowAnyHeader or AllowAnyMethod)
- Rate limiting extended to mutation endpoints, partitioned by user ID
- Fix SqlConnection and Dapper transactional atomicity in GameManager
- Periodic SteamTokenStore cleanup via a BackgroundService
- Explicit exception handling (no silent catch-all) on external API calls
- Basic ModifiedAt and DeletedAt audit fields on mutating operations
- Angular and dependency upgrade off EOL (13 to current LTS)
- Targeted regression tests proving each fix works

Should have (differentiators, worth it if cheap):
- Secret-scanning pre-commit hook (gitleaks or trufflehog)
- Centralized logging of security-relevant events (failed logins, rate-limit and CORS rejections)

Defer (v2+ or never given current use case):
- Refresh-token rotation, JWT key rotation, structured or immutable audit log, Redis-backed distributed state, secrets manager, full RBAC, WAF or edge hardening, mTLS, MFA, CSRF middleware, full-coverage mandate, full E2E suite, EF Core migration tooling.

### Architecture Approach

Six hardening changes map cleanly onto existing component boundaries with almost no cross-cutting surface, which is the key enabler for phased, low-risk delivery: credential fixes stay entirely inside IgdbService and SteamService; CORS and rate-limiting land in Program.cs plus controller attributes; the SqlConnection fix is isolated to GameManager; the token-store cleanup is a new BackgroundService matching the six that already exist; and audit fields require touching stored procedures plus optionally an EF SaveChangesInterceptor for non-Dapper write paths.

Major components:
1. Services/IgdbService.cs and Services/SteamService.cs: external integration layer; credential transport fix treated as two independently-verified changes
2. Program.cs: composition root for CORS policy, rate-limiter policies, hosted-service registration
3. Managers/GameManager.cs: business logic; the one structurally significant fix, wrapping the existing Dapper stored-proc call and EF SaveChangesAsync into a single IDbContextTransaction
4. Workers directory: background processing; new SteamTokenCleanupWorker follows the existing pattern exactly
5. Entities and Database numbered SQL scripts: audit field schema additions requiring every write-path stored procedure to be updated together

### Critical Pitfalls

1. Treating IGDB and Steam credential migration as identical: IGDB needs form-body, Steam has no body-based auth option at all. Treat as two separate, independently-verified changes with a real live API call after each.
2. CORS and rate-limiting tightened with zero test coverage breaks the SPA invisibly: CORS preflight failures never reach server logs; rate-limit rejections get silently absorbed by existing broad catch blocks in background workers. Derive the CORS header allow-list from the actual Angular interceptor code and manually walk the real browser-served SPA after each change.
3. Bundling the Angular 13-to-21 upgrade into the same milestone as backend hardening compounds risk: any regression becomes impossible to bisect between framework-upgrade cause and security-policy cause. Isolate the Angular upgrade as its own phase.
4. Fixing SqlConnection disposal without addressing the real transactional-atomicity defect: the actual bug is that the Dapper write and the EF write are non-atomic across two separate connections. Fix with a shared IDbContextTransaction and verify with a repetition or load test.
5. Backfilling audit columns on existing rows fabricates history: use explicit NULL or a clearly-marked sentinel, document the choice, and update every write stored procedure touching the affected tables together, including sync-worker upsert procs.

## Implications for Roadmap

Suggested phase structure, ordered per the explicit build-order and sequencing guidance in the architecture and pitfalls research:

### Phase 1: Angular EOL Upgrade
Rationale: highest regression count, most mechanically complex single item. Must be isolated against the currently permissive, unchanged backend so any regression can only be attributed to the frontend upgrade.
Delivers: Angular 13 to current LTS (20 or 21), sequential ng update per major version, one commit per version, full manual UAT pass after each hop.
Addresses: dependency EOL remediation.
Avoids: the upgrade and hardening compounding pitfall, and the sequencing pitfall.

### Phase 2: External Credential Migration (IGDB and Steam)
Rationale: most isolated, self-contained, highest-confidence-of-correctness backend change; unblocks confident testing of everything downstream.
Delivers: IGDB token request moved to a form-encoded POST body; Steam key moved to header and log-suppression; secrets removed from appsettings.Development.json into user-secrets and env vars.
Uses: System.Net.Http.Json, IOptionsMonitor for config-bound CORS and rate-limit values.
Implements: changes confined to IgdbService.cs and SteamService.cs.
Avoids: the identical-treatment credential pitfall.

### Phase 3: CORS and Rate Limiting Hardening
Rationale: both touch the same Program.cs middleware region. Sequenced after the Angular upgrade so the CORS allow-list is derived from the final, upgraded app actual outgoing headers.
Delivers: environment-based CORS config with restricted headers and methods; rate-limit policy extended to data-mutation endpoints, partitioned by JWT user-id claim.
Addresses: environment-based CORS, rate limiting on mutations.
Avoids: the silent-breakage pitfall and the sequencing-contract pitfall.

### Phase 4: GameManager Transaction Fix
Rationale: structurally significant, isolated to one component, needs its own targeted regression test.
Delivers: IDbContextTransaction wrapping the existing Dapper stored-proc call and EF SaveChangesAsync, reusing the DbContext connection instead of opening a second one.
Implements: GameManager.cs write-path fix.
Avoids: the disposal-vs-atomicity pitfall.

### Phase 5: Audit Fields and SteamTokenStore Cleanup
Rationale: audit fields depend on Phase 4 being done first since both touch the same write path.
Delivers: ModifiedAt and DeletedAt columns via a new numbered SQL script, all relevant stored procedures updated together, optional EF SaveChangesInterceptor; new SteamTokenCleanupWorker.
Addresses: audit fields, SteamTokenStore cleanup.
Avoids: the fabricated-audit-backfill pitfall.

### Phase 6: Verification and Full-Stack UAT
Rationale: every prior phase own verification only covers its half of the contract; a final combined pass validates the end-to-end system with both the upgraded frontend and hardened backend in place.
Delivers: targeted regression tests for each fixed vulnerability class, final full-SPA manual walkthrough, dependency vulnerability clean check.
Addresses: targeted tests, dependency clean-check.
Avoids: residual CORS and header contract risk after both workstreams land.

### Phase Ordering Rationale

- Angular upgrade goes first because it is the higher-regression-count change and benefits from isolation against an unchanged, known-permissive backend.
- Credential migration goes early among backend changes because it is the most self-contained.
- CORS is last among backend changes because it is the tightest-coupled contract with the Angular HTTP layer.
- The GameManager transaction fix and audit fields are sequenced consecutively because they touch the same write methods.
- A final combined verification phase is required because no single phase own testing validates the end-to-end contract.

### Research Flags

Needs deeper research during planning:
- Phase 1 (Angular upgrade): exact per-hop breaking changes should be re-confirmed via the angular.dev update guide at execution time; verify the Node.js version prerequisite.
- Phase 5 (audit fields): needs a full enumeration of every stored procedure touching Game, NowPlayingGame, and GameJournalEntry before implementation.

Standard patterns (skip research-phase):
- Phase 2 (credential migration): well-documented auth contracts.
- Phase 3 (CORS and rate limiting): standard, well-documented ASP.NET Core 8 built-in middleware patterns.
- Phase 4 (GameManager transaction fix): standard IDbContextTransaction pattern.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM-HIGH | Backend findings cross-checked against Microsoft Learn and NuGet; Angular version data cross-checked against angular.dev and HeroDevs. No Context7 MCP available this session. |
| Features | HIGH / MEDIUM | Credential handling, CORS, JWT validation, rate limiting backed by official docs and OWASP consensus; specific lifetime numbers from community sources, judgment-adjusted for personal-use context. |
| Architecture | HIGH / MEDIUM | Component boundaries and data flow verified by direct codebase reading; framework-pattern recommendations cross-checked against official docs and community sources. |
| Pitfalls | MEDIUM-HIGH | Backend and general findings HIGH based on documented ASP.NET Core behavior and the codebase own CONCERNS.md; Angular version specifics MEDIUM. |

Overall confidence: MEDIUM-HIGH

### Gaps to Address

- Exact target Angular version and precise per-hop breaking changes should be confirmed at the start of Phase 1, not locked in now.
- Full list of stored procedures requiring audit-field updates is not yet enumerated; needs a codebase grep pass during phase planning.
- Whether HowLongToBeat and GameHLTBManager even use credentials worth migrating is unconfirmed; verify before assuming it needs the same treatment as IGDB and Steam.
- Steam OpenID callback trust-boundary review needs isolated verification within the JWT and auth flow work; not yet scoped into a specific phase above.

## Sources

### Primary (HIGH confidence)
- Direct codebase reads: Program.cs, Services/IgdbService.cs, Services/SteamService.cs, Services/SteamTokenStore.cs, Managers/GameManager.cs, Entities/Game.cs, Workers/GameHLTBSyncWorker.cs, appsettings.Development.json
- .planning/codebase/CONCERNS.md, ARCHITECTURE.md, CONVENTIONS.md, TESTING.md, and .planning/PROJECT.md
- Microsoft Learn: Rate limiting middleware in ASP.NET Core; Storing application secrets safely during development
- Microsoft dotNET Blog: Building resilient cloud services with dotNET 8
- NuGet Gallery: Microsoft.Extensions.Http.Resilience and Polly package pages
- angular.dev reference/releases, reference/versions, and migrating-to-vitest guide
- Steamworks Documentation: Authentication using Web API Keys
- RFC 6749 (OAuth 2.0 Authorization Framework)

### Secondary (MEDIUM confidence)
- HeroDevs Blog: Angular Version History
- Twitch Developers OAuth docs and IGDB API docs
- Multiple CORS how-to articles cross-checked across sources
- dotnet-guide.com rate limiting and secrets management articles
- EF Core SaveChangesInterceptor pattern guides
- dotnet/aspnetcore GitHub issue on single-policy-per-endpoint limitation

### Tertiary (LOW confidence)
- None flagged; all findings were cross-verified against at least one primary or two independent secondary sources.

---
Research completed: 2026-08-28
Ready for roadmap: yes
