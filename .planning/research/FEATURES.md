# Feature Research

**Domain:** Security hardening pass — JWT-authenticated ASP.NET Core 8 + Angular SPA, personal/local use, external API integrations (IGDB, Steam, HowLongToBeat)
**Researched:** 2026-08-28
**Confidence:** HIGH (credential handling, CORS, JWT validation, rate limiting — official Microsoft docs + OWASP consensus) / MEDIUM (specific lifetime/rotation numbers — community sources, judgment-adjusted for personal-use context)

> Framing note: this is a hardening-only milestone. "Features" below means hardening **capabilities/practices**, not user-facing product features. Categorization is filtered through the project's actual context: single user / a few trusted users, not publicly deployed, no production SLA. Table stakes here means "skip this and you have a real, exploitable gap regardless of deployment scale" — not "skip this and users complain."

## Feature Landscape

### Table Stakes (Non-Negotiable Baseline Hygiene)

These apply to any app handling credentials and auth, public-facing or not. Skipping them isn't "acceptable for personal use" — it's a live vulnerability whether or not anyone but the owner ever sees the app. All items below directly map to an Active requirement already listed in PROJECT.md.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Move IGDB/Steam credentials from URL query strings to headers/body | Query-string secrets get logged by HTTP clients, reverse proxies, and browser/tool history regardless of who's using the app. `IgdbService.cs` already does this correctly for the *data* calls (Bearer header) but not the *token* call; Steam calls (`SteamService.cs` lines 41/59/76) still embed the key in the URL. | LOW | Use `FormUrlEncodedContent` for IGDB token request; Steam Web API has no header auth option — mitigate by ensuring the key never lands in logs (see below) since Valve's API mandates the query-string key. |
| Stop logging full request URLs for outbound HTTP calls to IGDB/Steam | If credentials must transit as query params (Steam has no alternative), the mitigation is ensuring `HttpClient`/logging middleware doesn't record full URLs at Information level. | LOW | Check `HttpClientFactory` logging handlers; scrub or lower log level for outbound Steam URLs. |
| Get the JWT secret and Steam/IGDB API keys out of `appsettings.*.json` | `appsettings.Development.json` currently has a real-looking IGDB `ApiKey` value checked into a JSON file alongside source. Any secret in a committed config file is a leak regardless of who deploys the app. | LOW | Move to `dotnet user-secrets` for local dev, environment variables for anything closer to "deployed." Official Microsoft guidance: User Secrets for dev-only, env vars for anything containerized/shared, secrets manager only for production — this app needs the first two, not the third. |
| Explicit JWT `TokenValidationParameters` (issuer, audience, lifetime, signing key) | `Program.cs` already sets `ValidateIssuer`/`ValidAudience`/`IssuerSigningKey`; confirm `ValidateLifetime` and `RequireExpirationTime` are both explicitly true rather than relying on defaults. | LOW | Quick review + explicit flags, not new code. |
| Reduce JWT lifetime from current 7 days to something bounded and intentional | `AuthService.GenerateToken` hardcodes `expires: DateTime.UtcNow.AddDays(7)`. A week-long bearer token is a large blast radius if it ever leaks (browser storage, log, screenshare). No refresh-token infra needed to fix this — just picking a deliberately shorter, documented value. | LOW | See "Feature Dependencies" — full refresh-token rotation is a differentiator, not required to fix this specific gap. Even dropping to 24h–on par with a "session" meaningfully shrinks exposure without adding a token-refresh flow. |
| Environment-based CORS configuration, restricted headers/methods | Hardcoded `localhost:4200` with `AllowAnyHeader().AllowAnyMethod()` is a footgun the moment this ever runs anywhere else, and `AllowAnyHeader/AllowAnyMethod` is broader than the SPA needs today. | LOW | Read allowed origin(s) from config per environment; restrict methods to GET/POST/PUT/DELETE and headers to `Authorization`/`Content-Type`. |
| Rate limiting extended to data-mutation endpoints | Rate limiting today only guards `/auth`. Any POST/PUT/DELETE endpoint without a limiter can be hammered (accidentally by a buggy client, or by a compromised token) with no backpressure. | LOW–MEDIUM | ASP.NET Core 8's built-in `Microsoft.AspNetCore.RateLimiting` middleware supports named policies applied via `[EnableRateLimiting("policy")]`; `UseRateLimiter()` must run **after** `UseRouting()` when using endpoint-specific (not just global) policies — this is the most common integration mistake. Fixed-window or token-bucket is sufficient for a single-instance, low-traffic app; no distributed/Redis limiter needed. |
| Fix `SqlConnection` disposal pattern in `GameManager` | Resource leaks eventually exhaust the connection pool and hang the app — this is a reliability issue with security-adjacent blast radius (denial of service against yourself). | LOW–MEDIUM | Already scoped in PROJECT.md; standard `using`/`await using` + explicit `OpenAsync()` fixes it, or route through DbContext instead of raw Dapper connections. |
| Periodic cleanup of `SteamTokenStore` | Unbounded in-memory growth of unredeemed OpenID tokens is a slow, self-inflicted DoS; also, ambient exposure window if an old token entry sat around indefinitely and got reused unexpectedly. | LOW | A `BackgroundService` sweeping expired entries every N minutes; no external dependency needed for a single-instance store. |
| Explicit exception handling on external API calls (no silent swallow) | Broad `catch (Exception)` blocks around IGDB/Steam calls currently only log a warning and continue. From a security-monitoring angle, this means auth-adjacent failures (e.g., a sudden spike of IGDB 401s suggesting a leaked/rotated credential) are invisible. | LOW–MEDIUM | Doesn't require Polly/circuit-breakers (that's reliability polish, out of scope) — just: catch narrower exception types where feasible, log at appropriate severity, and don't let external-API failures masquerade as "everything's fine." |
| Basic audit fields (who/when) on data-modifying operations | No way to answer "who changed this and when" today. Even for a single-user app this matters the moment there's more than one trusted user, or when debugging "why did this record change." | LOW–MEDIUM | Add `ModifiedBy`/`ModifiedAt` (and `CreatedBy` where missing) columns; since writes go through stored procedures per project convention, this means updating the relevant SPs (`sp_UpdateGame`, `sp_DeleteGame`, etc.) to accept and persist a user identifier — not a new logging subsystem. |
| Dependency EOL remediation (Angular 13 → current LTS + related packages) | Angular 13 has been unpatched since April 2023. This is a security fix, not routine maintenance — unpatched framework-level CVEs are exploitable regardless of who's using the app or how "personal" it is. | HIGH | Largest single item in this milestone. `ng update` path from 13→18 crosses multiple major versions; expect RxJS 6→7 operator changes, Ivy-related template stricter checks, and TypeScript 4.6→5.x breaking changes. Do incrementally (13→14→15→16→17→18), not in one jump. |
| `npm audit` / `dotnet list package --vulnerable` clean (or explicitly triaged) at milestone end | Baseline hygiene check that nothing else shipped alongside the Angular upgrade reintroduces a known-vulnerable transitive dependency. | LOW | One-time check + fix, not ongoing tooling. |
| Targeted tests for hardening-touched code paths | Zero test coverage today means every fix above ships with no regression safety net. This isn't "add tests as a feature" — it's the minimum verification that the fixes actually work and stay working. | MEDIUM | Scope: auth token generation/validation, credential-no-longer-in-URL assertions (mock `HttpMessageHandler`, assert request body/headers not query string), CORS policy behavior, rate-limiter triggering on mutation endpoints, `GameManager` CRUD paths touched by the connection-disposal fix, `SteamTokenStore` expiry/cleanup. Not a coverage-percentage target — one meaningful test per fixed vulnerability class. |

### Differentiators (Extra Safety Margin — Valuable, Not Required Here)

These raise the security bar further but are the kind of thing a personal/local project can defer without leaving a live exploitable gap. Worth doing if cheap; not worth blocking the milestone on.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Refresh-token rotation (short-lived access token + long-lived rotating refresh token, single-use with reuse detection) | Lets you shrink the access-token lifetime to minutes instead of days without forcing re-login every few minutes. Industry-standard pattern: store only a hash (SHA-256) of the refresh token server-side so a DB leak doesn't yield usable tokens. | HIGH | Real feature work (new endpoint, new storage, rotation/revocation logic, frontend interceptor changes) — disproportionate to a single/few-user personal app's threat model. Reasonable to defer to a follow-up milestone if login-frequency friction becomes annoying after shortening the JWT lifetime. |
| JWT signing key rotation with `kid` header support | Limits blast radius if the signing key is ever compromised; lets you rotate without invalidating every issued token at once. | MEDIUM | Only pays off once there's a real rotation cadence/process; for a personal app, "rotate manually if you ever suspect leakage" is adequate. |
| Structured audit log (append-only table capturing who/what/before/after per sensitive mutation) | Goes beyond "who/when" fields to a queryable history — useful if you ever want to answer "what did this record look like last Tuesday." | MEDIUM | The Active requirement only asks for basic who/when fields; a full audit trail is a genuine upgrade but is new schema + write-path plumbing across every mutating manager. |
| Secret-scanning pre-commit hook (gitleaks/trufflehog) | Prevents the exact class of mistake already found in this codebase (a real-looking API key in `appsettings.Development.json`) from recurring. | LOW | Cheap to add (a git hook + CI check), high leverage for a solo/small-team repo. Good candidate to actually include given it's low effort and directly prevents recurrence of a finding already in CONCERNS.md. |
| Retry + circuit breaker (Polly) for IGDB/Steam/HLTB calls | Turns "external API flaky → silent data gap" into a controlled, observable degradation. | MEDIUM | Reliability polish more than security; only marginally relevant to hardening (prevents a slow-external-API from tying up threads/connections). Reasonable to defer. |
| Centralized logging of security-relevant events (failed logins, rate-limit rejections, CORS rejections) | Turns "something's wrong" into "here's exactly what and when," useful even for a single user debugging their own access issues. | LOW–MEDIUM | Straightforward with `ILogger` + structured logging; not urgent without anyone actively monitoring logs, but cheap enough to bundle if time allows. |
| CI step running `npm audit` / `dotnet list package --vulnerable` on every push (not just once at milestone end) | Catches new EOL/vulnerable dependencies going forward instead of re-discovering them in the next hardening pass. | LOW–MEDIUM | Valuable ongoing hygiene; deferred here to "single check at milestone end" per Table Stakes since standing up CI infra is arguably its own project, not a hardening fix. |

### Anti-Features (Deliberately Not Building — Wrong Fit for This App)

These are legitimate security practices for a publicly-deployed, multi-tenant, or compliance-bound product. Building them here would be effort spent on a threat model this app doesn't have.

| Feature | Why It Looks Right | Why It's Wrong Here | Alternative |
|---------|---------------------|----------------------|-------------|
| Distributed/Redis-backed `SteamTokenStore` | "Production apps use shared caches for horizontal scaling" | App runs single-instance for personal use; there's no second instance to share state with. Already explicitly out of scope in PROJECT.md. | In-memory store with a periodic cleanup sweep (already in Table Stakes). |
| Secrets manager (Azure Key Vault / HashiCorp Vault) | "Never store secrets in config files, always use a vault" | True in principle, but a vault adds an external dependency, auth-to-the-vault problem, and ops overhead disproportionate to one developer's local machine and a handful of trusted users. | `dotnet user-secrets` for local dev, environment variables for anything closer to "always-on" — matches official Microsoft guidance for this exact scenario (dev-only app, not containerized production). |
| Full RBAC / multi-tenant permission system | "Proper apps have granular roles and permissions" | There's no tenant boundary or role hierarchy to protect — single user or a few trusted users share one trust level. | Keep the existing simple authenticated/not-authenticated model; add audit fields (who/when) so accountability exists without building an authorization framework. |
| WAF / API gateway / reverse-proxy hardening (rate limiting at the edge, geo-blocking, DDoS mitigation) | "Every production API sits behind a WAF" | App isn't publicly deployed; there is no edge to harden and no internet-facing attack surface to filter. | Application-level rate limiting (already in Table Stakes) is sufficient for the actual exposure. |
| mTLS / client-certificate authentication | "Mutual TLS is stronger than bearer tokens" | Massive operational complexity (cert issuance, rotation, distribution to the SPA) for a threat model where JWT + HTTPS is already more than adequate. | Keep JWT Bearer; focus hardening effort on token lifetime/secret handling instead. |
| SOC2/compliance-grade immutable/tamper-evident audit ledger | "Real audit trails can't be altered" | No compliance obligation exists; tamper-evidence (hash chains, write-once storage) is solving a threat (a malicious insider altering their own audit trail) that doesn't apply to a single trusted operator. | Plain audit columns / an audit table in the same SQL Server database is sufficient. |
| Multi-factor authentication (2FA/TOTP) | "MFA is standard for any login system" | Adds real UX friction (enrollment, recovery codes, backup flows) for a login system with one or a few trusted users on trusted devices. This is new *feature* surface, not a fix to an existing gap — and this milestone is explicitly hardening-only, no new features. | Rely on a strong password (already BCrypt-hashed) + shortened JWT lifetime; revisit only if the app ever gets exposed to untrusted users. |
| CSRF token middleware | "CSRF protection is a security basics checklist item" | This API authenticates via `Authorization: Bearer` header sent explicitly by the SPA, not an ambient cookie — CSRF's precondition (browser auto-attaches credentials) doesn't apply. Adding anti-CSRF tokens here protects against an attack this architecture isn't vulnerable to. | Confirm no endpoint relies on cookie-based auth (Steam OpenID callback should be checked for this specifically); if it's bearer-only, no CSRF middleware needed. |
| 100% / high-percentage test coverage mandate | "Untested code is unsafe code" | Explicitly out of scope in PROJECT.md — zero prior test coverage exists across the whole codebase; retrofitting broad coverage is a multi-week project of its own, unrelated to whether the specific hardening fixes work. | Targeted tests per fixed vulnerability class only (already in Table Stakes). |
| Full E2E/browser automation suite (Playwright/Cypress covering the whole app) | "Security regressions can hide in UI flows" | Same scope problem as above — this is a general test-coverage investment, not a hardening-specific one, and the app has no existing E2E infra to build on cheaply. | Backend-focused unit/integration tests on the specific hardened paths; skip UI automation for this milestone. |
| Migrating SQL scripts to EF Core migrations | "Migrations should be automated and versioned in code" | Legitimate process improvement, but not a security fix — explicitly called out as out of scope in PROJECT.md. Doing it now would expand milestone scope without closing any vulnerability. | Keep the existing numbered-SQL-script convention; revisit in a dedicated infra milestone. |

## Feature Dependencies

```
[Move credentials out of URLs] ── independent, no dependencies

[JWT lifetime reduction] ──requires nothing new── can ship standalone
    └──enhances──> [Refresh-token rotation] (differentiator; only needed if shortened
                     lifetime creates login-frequency friction worth solving)

[Environment-based CORS config] ── independent, no dependencies

[Rate limiting on mutation endpoints]
    └──builds on──> [existing /auth rate limiter infrastructure] (already present)

[SqlConnection disposal fix] ── independent, no dependencies

[Audit fields (who/when)]
    └──requires──> [Stored procedure updates] (sp_UpdateGame, sp_DeleteGame, etc. must
                     accept + persist a user identifier — per project convention, writes
                     go through SPs, not EF)
    └──enhances──> [Structured audit log] (differentiator; audit columns are a
                     prerequisite foundation, not the same feature)

[Angular EOL upgrade]
    └──should precede──> [Frontend security-relevant tests] (no point testing against
                          a version being replaced; sequence upgrade before any new
                          frontend test infra)
    └──independent of──> all backend hardening items (can proceed in parallel)

[Targeted tests for hardening paths]
    └──requires──> [the corresponding fix already made or being made alongside]
                     (tests should be written to prove the vulnerability is closed,
                     not written blind before the fix exists)

[Secret-scanning pre-commit hook] (differentiator)
    └──should follow──> [Secrets removed from appsettings.Development.json]
                          (add the hook after cleaning current history/working tree,
                          otherwise it flags the existing committed secret on first run)
```

### Dependency Notes

- **Audit fields require stored procedure updates:** per CLAUDE.md convention (SPs only for insert/update/delete, EF LINQ for reads), adding `ModifiedBy`/`ModifiedAt` isn't just a schema change — every write-path SP needs the new parameter threaded through. Scope this as one unit of work per entity (Game, NowPlayingGame, GameJournalEntry), not a single global change.
- **JWT lifetime reduction does not require refresh tokens:** these are often bundled together in generic "JWT best practices" writeups, but for this app's risk profile, simply picking a shorter, deliberate expiry (e.g., 24h instead of 7 days) closes most of the exposure without new endpoints or storage. Only build refresh-token rotation if login friction from the shorter lifetime becomes a real usability problem.
- **Angular upgrade is independent of backend hardening items:** no shared code path forces sequencing between the two; they can be parallelized across phases if the roadmap benefits from it. The one soft dependency is that new frontend tests should target the upgraded Angular version, not v13, to avoid throwaway test infrastructure.
- **Secret-scanning hook must follow secret removal, not precede it:** enabling gitleaks/trufflehog before the current IGDB key is out of `appsettings.Development.json` will immediately fail on the existing committed secret; sequence the cleanup first.

## MVP Definition

Framed as "hardening MVP" — the minimum set of fixes that closes every concrete gap already identified, per PROJECT.md's Active requirements.

### Ship This Milestone (Table Stakes, all of it)

- [ ] Credentials out of URLs (IGDB token call, Steam calls where feasible) — closes a live credential-exposure gap
- [ ] Secrets out of committed config files (`dotnet user-secrets` + env vars) — closes the leaked-key-in-git gap
- [ ] JWT lifetime shortened + validation parameters confirmed explicit — reduces stolen-token blast radius
- [ ] Environment-based, restricted CORS policy — closes the "works by accident" gap
- [ ] Rate limiting on mutation endpoints — closes the write-endpoint abuse gap
- [ ] `SqlConnection` disposal fix — closes the resource-exhaustion gap
- [ ] `SteamTokenStore` periodic cleanup — closes the unbounded-memory-growth gap
- [ ] Explicit exception handling (no silent swallow) on external API calls — restores visibility into failures
- [ ] Basic audit fields (who/when) on mutations — closes the no-accountability gap
- [ ] Angular + related deps upgraded off EOL — closes the unpatched-framework gap
- [ ] Targeted tests proving each fix above actually works — the only verification this milestone gets

### Add After This Milestone, If Login Friction or Time Allows (v1.x)

- [ ] Secret-scanning pre-commit hook — cheap, prevents recurrence of the exact finding already in CONCERNS.md
- [ ] Refresh-token rotation — only if shortened JWT lifetime causes real friction
- [ ] Structured audit log table — only if "who/when" columns prove insufficient in practice
- [ ] Retry/circuit breaker for external APIs — reliability polish, not security-critical

### Explicitly Deferred / Out of Scope (v2+ or never, given current use case)

- [ ] JWT key rotation infra, MFA, distributed token store, secrets manager, WAF/edge hardening, mTLS, full RBAC, immutable audit ledger, CSRF middleware, full test-coverage mandate, E2E suite, EF Core migration tooling — see Anti-Features table for rationale on each. Revisit only if the app's deployment model changes (multiple untrusted users, public exposure, compliance requirement).

## Feature Prioritization Matrix

| Feature | Security Value | Implementation Cost | Priority |
|---------|-----------------|----------------------|----------|
| Credentials out of URLs (IGDB) | HIGH | LOW | P1 |
| Secrets out of committed config | HIGH | LOW | P1 |
| JWT lifetime reduction + explicit validation | HIGH | LOW | P1 |
| Environment-based CORS | MEDIUM | LOW | P1 |
| Rate limiting on mutations | MEDIUM | LOW–MEDIUM | P1 |
| SqlConnection disposal fix | MEDIUM | LOW–MEDIUM | P1 |
| SteamTokenStore cleanup | LOW–MEDIUM | LOW | P1 |
| Explicit exception handling | MEDIUM | LOW–MEDIUM | P1 |
| Audit fields (who/when) | MEDIUM | LOW–MEDIUM | P1 |
| Angular EOL upgrade | HIGH | HIGH | P1 |
| Targeted hardening tests | HIGH (verification) | MEDIUM | P1 |
| Secret-scanning pre-commit hook | MEDIUM | LOW | P2 |
| Refresh-token rotation | MEDIUM | HIGH | P3 |
| Structured audit log | LOW–MEDIUM | MEDIUM | P3 |
| Retry/circuit breaker (Polly) | LOW (reliability, not security) | MEDIUM | P3 |

**Priority key:**
- P1: In scope for this milestone — matches an Active requirement in PROJECT.md
- P2: Worth adding if time allows — cheap, high-leverage, directly related to a finding already surfaced
- P3: Defer to a future milestone unless a specific trigger (login friction, more users, public deployment) makes it necessary

## Sources

- [Storing application secrets safely during development — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0) — HIGH confidence, official docs. Confirms User Secrets (dev-only) → environment variables (containers/shared) → Key Vault (production) progression; this app needs only the first two tiers.
- [.NET Secrets Management: User Secrets vs Environment Variables vs Azure Key Vault](https://www.dotnet-guide.com/articles/dotnet-secrets-management/) — MEDIUM confidence, community source cross-checked against Microsoft Learn above.
- [Rate Limiting in ASP.NET Core 8: Fixed Window, Sliding Window & Concurrency Policies With Built-In Middleware](https://www.dotnet-guide.com/articles/aspnet-core-rate-limiting/) — MEDIUM confidence. Confirms `UseRateLimiter()` middleware-ordering requirement (after `UseRouting()` for endpoint-specific policies) and algorithm selection guidance.
- [Mastering ASP.NET Core Rate Limiting: From Middleware to Distributed Redis Strategies](https://developersvoice.com/blog/dotnet/mastering-distributed-rate-limiting-asp-net-core/) — MEDIUM confidence. Confirms distributed/Redis rate limiting is a production-scaling concern this single-instance app doesn't need.
- [How to Implement Refresh Tokens and Token Revocation in ASP.NET Core](https://antondevtips.com/blog/how-to-implement-refresh-tokens-and-token-revocation-in-aspnetcore) — MEDIUM confidence. Source for refresh-token rotation / single-use / hash-storage pattern, categorized here as a differentiator, not table stakes.
- [Refresh Tokens in ASP.NET Core - A Complete Guide](https://codewithmukesh.com/blog/refresh-tokens-in-aspnet-core/) — MEDIUM confidence, cross-checked against the above for JWT short-lifetime + refresh-rotation consensus.
- Direct codebase inspection (`GamePlanner.API/Program.cs`, `Services/AuthService.cs`, `Services/IgdbService.cs`, `Services/SteamService.cs`, `appsettings.Development.json`) — HIGH confidence, primary source. Confirmed: 7-day fixed JWT expiry, no refresh-token infrastructure present, IGDB token endpoint still uses query-string credentials while data endpoints correctly use Bearer headers, and a real-looking IGDB API key value present in `appsettings.Development.json`.
- `.planning/codebase/CONCERNS.md` (2026-08-28 audit) — primary source for the existing findings this document builds on.
- `.planning/PROJECT.md` — primary source for the Active requirements this categorization is scoped against.

---
*Feature research for: Security hardening — ASP.NET Core 8 / Angular personal-use app*
*Researched: 2026-08-28*
