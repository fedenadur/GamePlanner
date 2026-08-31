# GamePlanner — Security Hardening Milestone

## What This Is

GamePlanner is a personal game-tracking web app (Angular SPA + ASP.NET Core 8 API + SQL Server) that lets a user manage their game backlog, track now-playing sessions, journal entries, and sync metadata from IGDB, Steam, and HowLongToBeat. This milestone is a dedicated security hardening pass over the existing app before any further feature work.

## Core Value

Close the concrete security and reliability gaps already identified in the codebase (credential handling, CORS/rate limiting, resource leaks, missing audit trail) without breaking existing functionality.

## Requirements

### Validated

- ✓ Game backlog tracking (CRUD via `GameManager`, EF Core reads + stored-proc writes) — existing
- ✓ Now-playing / session tracking (`SessionPlannerManager`) — existing
- ✓ Backlog prioritization logic (`BacklogPrioritizerManager`) — existing
- ✓ Game journal entries (`GameJournalEntry`) — existing
- ✓ IGDB metadata sync (game info, cover images) via `IgdbService` / `GameImageManager` — existing
- ✓ Steam library import + playtime sync via `SteamService` / Steam OpenID login — existing
- ✓ HowLongToBeat playtime sync via `GameHLTBManager` / `GameHLTBSyncWorker` — existing
- ✓ JWT-based authentication with rate limiting on `/auth` endpoints — existing
- ✓ Background sync workers (game, image, Steam playtime, HLTB) — existing
- ✓ Upgrade Angular from 13.3.0 (EOL) to current LTS, plus related EOL deps (TypeScript, RxJS, zone.js, Jasmine/Karma) — eliminates unpatched frontend vulnerabilities — validated Phase 1 (angular-eol-upgrade), 2026-08-29
- ✓ Move IGDB and Steam API credentials out of URL query strings into headers/body, off committed config, and rotate previously-leaked values (`IgdbService.cs`, `SteamService.cs`, `dotnet user-secrets`, gitleaks pre-commit hook) — validated Phase 2 (credential-secrets-hardening), 2026-08-30
- ✓ Replace broad silent `catch (Exception)` patterns around external API calls with explicit handling (logging severity + failure counts surfaced via `WorkerStatus`) — validated Phase 2 (credential-secrets-hardening), 2026-08-30
- ✓ Environment-based CORS configuration (replace hardcoded `localhost:4200` policy) with restricted headers/methods — validated Phase 3 (auth-cors-rate-limiting-hardening), 2026-08-31
- ✓ Rate limiting extended to data-modification endpoints (game create/update/delete, journal, sessions), not just `/auth` — validated Phase 3 (auth-cors-rate-limiting-hardening), 2026-08-31
- ✓ JWT lifetime bounded to 24h and `TokenValidationParameters` explicitly hardened (issuer/audience/lifetime/algorithm) — validated Phase 3 (auth-cors-rate-limiting-hardening), 2026-08-31
- ✓ Security-relevant rejections (failed logins, CORS mismatches, rate-limit rejections) visible in a central log stream — validated Phase 3 (auth-cors-rate-limiting-hardening), 2026-08-31

### Active

- [ ] Steam OpenID trust boundary review (not covered by Phase 3's JWT/CORS/rate-limiting scope)
- [ ] Fix `SqlConnection` disposal/connection handling in `GameManager` (Create/Update/Delete paths)
- [ ] Add periodic cleanup for `SteamTokenStore` (currently grows unbounded until redeemed)
- [ ] Add basic audit fields (who/when) for data-modifying operations, at least on soft-delete (`IsActive`) paths
- [ ] Targeted unit/integration tests covering the areas touched by hardening (auth, `GameManager`, `IgdbService`, `SteamService`, `SteamTokenStore`) — not full coverage, just enough to catch regressions from these fixes

### Out of Scope

- New feature development or improvements to existing features (backlog prioritizer, session planner, journal UX, etc.) — deferred to a follow-up milestone; this milestone is hardening-only
- Migrating SQL scripts to EF Core migrations — reliability/process improvement, not a security fix; not blocking hardening
- Distributed/scaled deployment concerns (Redis-backed token store, distributed job scheduler for workers) — app runs single-instance for personal use; no current scaling need
- Full test suite / high coverage target — only tests directly verifying hardening changes are in scope
- Production deployment hardening (TLS termination, WAF, secrets manager, etc.) — app is used personally/locally, not publicly deployed

## Context

- Brownfield project; full codebase map exists at `.planning/codebase/` (ARCHITECTURE.md, STACK.md, CONCERNS.md, CONVENTIONS.md, INTEGRATIONS.md, STRUCTURE.md, TESTING.md), refreshed 2026-08-28.
- `.planning/codebase/CONCERNS.md` already contains a structured security/reliability audit (credentials in URLs, CORS, resource leaks, missing tests, EOL dependencies, missing audit logging, no rate limiting on writes). This milestone's "identify problems" phase builds on and deepens those findings rather than re-auditing from scratch.
- App is used personally/locally, not deployed publicly with real users — lowers urgency of production-grade concerns (WAF, secrets manager, distributed scaling) but does not reduce urgency of fixing credential-in-URL and dependency EOL issues, since those are exploitable regardless of deployment scale.
- Stack: ASP.NET Core 8.0 API (EF Core for reads, stored procedures for writes, Dapper in places), Angular 13.3.0 SPA (EOL), SQL Server, JWT Bearer auth, BCrypt password hashing, external integrations with IGDB, Steam, HowLongToBeat.
- No existing test coverage anywhere in the codebase (backend or frontend) prior to this milestone.

## Constraints

- **Security**: No production users currently, but credential-handling and dependency-EOL fixes still apply regardless of deployment scale — fix as if it will be exposed
- **Scope**: Hardening only — no feature work in this milestone, per explicit user decision
- **Tech stack**: EF LINQ for reads, stored procedures only for insert/update/delete (existing project convention, from CLAUDE.md) — hardening fixes must respect this pattern
- **Testing**: Add only targeted tests for hardening-touched code paths, not a full suite

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Hardening-only milestone; feature improvements deferred | User wants security prioritized before any feature work | — Pending |
| Build on existing CONCERNS.md audit rather than re-auditing from scratch | Prior codebase mapping already surfaced concrete, file-specific security findings | — Pending |
| Include Angular EOL upgrade in this milestone | Angular 13 has been unpatched for 2+ years; treated as a security fix, not a feature/maintenance task | — Pending |
| Add targeted tests alongside hardening fixes | Auth/external-service code has zero existing test coverage; changes here risk silent regressions without tests | — Pending |
| Scope limited to personal/local use, not production-hardening | App is not publicly deployed; distributed/scaling concerns and infra-level hardening excluded | — Pending |
| Fixed cross-user session-planner data leak inside Phase 3 rather than deferring | Folded todo's reproduction traced the leak to both `SessionManager` and `SessionPlannerManager`; fixing while already in the auth/session trust-boundary code was lower-risk than a separate follow-up phase | ✓ Closed — Phase 3 (03-02), live-verified no cross-user leak |
| Rate-limit backstop (concurrent-load atomicity) and CORS browser-walkthrough closed via Playwright MCP live testing at end-of-phase human verification, not left as open gaps | Both were marked `verification: backstop`/deferred-to-human in the phase's own plans; automated sequential testing during the phase couldn't prove concurrent-safety or real browser CORS enforcement | ✓ Closed — Phase 3 UAT, 2026-08-31 |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-08-31 after Phase 3 (auth-cors-rate-limiting-hardening) completion*
