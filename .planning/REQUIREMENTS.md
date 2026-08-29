# Requirements: GamePlanner — Security Hardening Milestone

**Defined:** 2026-08-28
**Core Value:** Close the concrete security and reliability gaps already identified in the codebase without breaking existing functionality.

## v1 Requirements

Requirements for this hardening milestone. Each maps to roadmap phases.

### Credentials & Secrets (CRED)

- [ ] **CRED-01**: IGDB token request uses a form-encoded POST body instead of a URL query string
- [ ] **CRED-02**: Steam API key no longer appears in logs or unhandled exception output (query param usage stays, since Steam's Web API has no body-based auth option)
- [ ] **CRED-03**: Secrets removed from `appsettings.Development.json`, moved to `dotnet user-secrets` (dev) and environment variables (closer-to-deployed)
- [ ] **CRED-04**: The IGDB/Steam credentials already exposed in committed config are rotated
- [ ] **CRED-05**: Secret-scanning pre-commit hook (gitleaks or trufflehog) installed to prevent future leaks

### Auth & JWT Hardening (AUTH)

- [ ] **AUTH-01**: JWT lifetime reduced from 7 days to a deliberate, bounded value
- [ ] **AUTH-02**: `TokenValidationParameters` explicitly reviewed and configured (issuer, audience, lifetime validation)

### CORS & Rate Limiting (NET)

- [ ] **NET-01**: CORS allowed origins load from environment-based configuration (no hardcoded `localhost`)
- [ ] **NET-02**: CORS restricted to explicit headers/methods (no `AllowAnyHeader`/`AllowAnyMethod`)
- [ ] **NET-03**: Rate limiting extended to data-mutation endpoints (game/journal/session POST/PUT/DELETE), partitioned by JWT user ID

### Reliability (REL)

- [ ] **REL-01**: `GameManager` writes wrap the Dapper stored-proc call and EF `SaveChangesAsync` in a single atomic transaction
- [ ] **REL-02**: `SteamTokenStore` has periodic cleanup of expired entries via a background worker
- [ ] **REL-03**: Broad silent `catch (Exception)` around external API calls replaced with explicit handling (no silent data loss)

### Audit Trail (AUDIT)

- [ ] **AUDIT-01**: `ModifiedAt` field added to mutable entities and populated on write
- [ ] **AUDIT-02**: `DeletedAt` field added to soft-deletable entities and populated on delete

### Observability (OBS)

- [ ] **OBS-01**: Security-relevant events logged centrally (failed logins, CORS rejections, rate-limit rejections)

### Dependency Upgrade (DEP)

- [ ] **DEP-01**: Angular upgraded sequentially from 13.3.0 to current LTS (13→...→target, no version skipping)
- [ ] **DEP-02**: Related frontend dependencies (TypeScript, RxJS, zone.js, Jasmine, Karma) upgraded to versions compatible with the target Angular

### Testing (TEST)

- [ ] **TEST-01**: Targeted tests added covering hardening-touched paths (auth, `GameManager`, `IgdbService`, `SteamService`, `SteamTokenStore`)
- [ ] **TEST-02**: Full-stack manual UAT pass validating the combined frontend+backend contract after all hardening phases

## v2 Requirements

Deferred to a future milestone. Tracked but not in current roadmap.

### Feature Improvements

- **FEAT-01**: Improvements to backlog prioritizer, session planner, journal UX, and other existing features (explicitly deferred per user decision — this milestone is hardening-only)

### Advanced Security

- **SEC-01**: Refresh-token rotation and JWT key rotation
- **SEC-02**: Structured/immutable audit log (beyond basic ModifiedAt/DeletedAt fields)

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Distributed/Redis-backed token store | App runs single-instance for personal use; no current scaling need |
| Secrets manager (Vault/Azure Key Vault) | Overkill for personal/local use; user-secrets + env vars sufficient |
| Full RBAC | Single/few-user app, not a multi-tenant system |
| WAF / edge hardening / mTLS | Not publicly deployed; no exposed attack surface to justify infra hardening |
| MFA | Personal/local use; JWT + BCrypt password hashing already in place |
| CSRF middleware | API is bearer-token authenticated (Authorization header), not cookie-based — CSRF doesn't apply |
| Full test-coverage mandate / full E2E suite | Only tests directly verifying hardening changes are in scope, per PROJECT.md constraint |
| Migrating SQL scripts to EF Core migrations | Reliability/process improvement, not a security fix; not blocking hardening |
| Production deployment hardening (TLS termination, secrets rotation infra, etc.) | App is used personally/locally, not publicly deployed |

## Traceability

Populated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| DEP-01 | — | Pending |
| DEP-02 | — | Pending |
| CRED-01 | — | Pending |
| CRED-02 | — | Pending |
| CRED-03 | — | Pending |
| CRED-04 | — | Pending |
| CRED-05 | — | Pending |
| NET-01 | — | Pending |
| NET-02 | — | Pending |
| NET-03 | — | Pending |
| AUTH-01 | — | Pending |
| AUTH-02 | — | Pending |
| REL-01 | — | Pending |
| REL-02 | — | Pending |
| REL-03 | — | Pending |
| AUDIT-01 | — | Pending |
| AUDIT-02 | — | Pending |
| OBS-01 | — | Pending |
| TEST-01 | — | Pending |
| TEST-02 | — | Pending |

**Coverage:**
- v1 requirements: 20 total
- Mapped to phases: 0 (pending roadmap)
- Unmapped: 20 ⚠️ (resolved during roadmap creation)

---
*Requirements defined: 2026-08-28*
*Last updated: 2026-08-28 after initial definition*
