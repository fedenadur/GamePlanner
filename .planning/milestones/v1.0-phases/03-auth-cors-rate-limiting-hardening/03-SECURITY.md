---
phase: 03
slug: auth-cors-rate-limiting-hardening
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-08-31
---

# Phase 03 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| browser → API | Cross-origin JS sends an attacker-controllable `Origin` header and an attacker-controllable `Authorization` bearer token | Origin header, bearer token |
| unauthenticated client → `/api/auth/*` | Credentials cross here before any identity exists | Email, password |
| authenticated client → mutation endpoints | Request volume crosses here; one tenant's volume must not consume another's allowance | Request volume, JWT claims |
| authenticated client → session endpoints | A valid token for user A must not be able to read user B's library through a recommendation response | Game library data |
| application → log stream | Whatever is written here is read by the operator and may be retained; secret material must not cross | Log lines (email, reason, origin, user id) |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-03-01 | Spoofing | `TokenValidationParameters` (`Program.cs`) | high | mitigate | `ValidAlgorithms = [HmacSha256]`, `RequireExpirationTime = true` — tampered/alg-confused tokens rejected | closed |
| T-03-02 | Elevation of Privilege | `AuthService.GenerateToken` | high | mitigate | JWT lifetime bounded to 24h (`AddHours(24)`, `AddDays(7)` absent) | closed |
| T-03-03 | Information Disclosure | CORS policy `AllowAngular` (`Program.cs`) | medium | mitigate | Origin sourced from `App:FrontendUrl` (fail-fast if absent/empty, no permissive fallback), headers/methods restricted to exactly 2/4 | closed |
| T-03-04 | Denial of Service | `"mutations"` partition key (`Program.cs`) | high | mitigate | Per-user `FixedWindowRateLimiter` (100/hour), `UseAuthentication()` ordered before `UseRateLimiter()` | closed |
| T-03-05 | Repudiation | Rate-limit rejections | medium | mitigate | `OnRejected` writes one structured warning naming user/IP, path, policy | closed |
| T-03-06 | Tampering | JWT clock-skew tolerance | low | accept | Library default (5 min) kept — single-server deployment, no clock-drift attack surface | closed |
| T-03-SC | Tampering | npm/pip/cargo installs (03-01 scope) | low | accept | No package-manager install occurred in this plan's scope | closed |
| T-03-07 | Information Disclosure | `SessionManager`/`SessionPlannerManager` `db.Games` queries | high | mitigate | Both predicates scoped with `g.UserId == userId` — closes reproduced cross-user leak | closed |
| T-03-08 | Denial of Service | Un-attributed mutation endpoints (Games/Journal/Session controllers) | medium | mitigate | `[EnableRateLimiting("mutations")]` on all 9 in-scope POST/PUT/DELETE actions | closed |
| T-03-09 | Spoofing | `SessionController` actions with no user-id extraction | high | mitigate | `GetUserId()` + `Unauthorized()` guard before manager calls | closed |
| T-03-14 | Information Disclosure | `db.GamesHLTB` reference lookup in `SessionPlannerManager` | low | accept | Shared, non-user-scoped reference data with no `UserId` column — exposes nothing account-specific | closed |
| T-03-SC | Tampering | npm/pip/cargo installs (03-02 scope) | low | accept | No package-manager install occurred in this plan's scope | closed |
| T-03-10 | Repudiation | `AuthService.LoginAsync` failure branches | medium | mitigate | `LogWarning` on both failure branches with distinct reasons | closed |
| T-03-11 | Information Disclosure | Two failure branches leaking into the HTTP response | medium | mitigate | Single `Unauthorized("Invalid email or password.")` for both branches | closed |
| T-03-12 | Repudiation | CORS origin mismatches invisible server-side | medium | mitigate | `CorsRejectionLoggingMiddleware` registered ahead of `UseCors`, logs origin/method/path | closed |
| T-03-15 | Information Disclosure | Secret material reaching the log stream | high | mitigate | No log line references `Password`/`PasswordHash`/`Token`; middleware logs only origin/method/path | closed |
| T-03-13 | Information Disclosure | Log noise from non-browser clients masking real signal | low | accept | Origin-presence guard removes the dominant false-positive source; residual case accepted for a single-origin personal app | closed |
| T-03-16 | Denial of Service | Log-volume amplification from a scripted cross-origin flood | low | accept | One short warning per rejection; app not publicly deployed; existing rate limiters bound authenticated volume | closed |
| T-03-SC | Tampering | npm/pip/cargo installs (03-03 scope) | low | accept | No package-manager install occurred in this plan's scope | closed |

*Status: open · closed · open — below {block_on} threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on (high) count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-01 | T-03-06 | JWT `ClockSkew` left at library default (5 min) — single-server deployment has no clock-drift attack surface that tightening would close | Phase 03 plan (03-01) | 2026-08-31 |
| AR-02 | T-03-14 | `GamesHLTB` is shared reference data with no `UserId` column; deliberately left unfiltered | Phase 03 plan (03-02) | 2026-08-31 |
| AR-03 | T-03-13 | Residual log-noise case from a hypothetical non-browser client accepted for a single-origin personal app with no such client | Phase 03 plan (03-03) | 2026-08-31 |
| AR-04 | T-03-16 | Log-volume amplification from a scripted CORS flood accepted — app not publicly deployed, existing rate limiters bound authenticated volume | Phase 03 plan (03-03) | 2026-08-31 |
| AR-05 | T-03-SC | No package-manager installs occurred in any of the three plans' scopes — package legitimacy gate not applicable | Phase 03 plans (03-01/02/03) | 2026-08-31 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-31 | 19 | 19 | 0 | Claude (gsd-secure-phase, orchestrator — register authored at plan time, ASVS L1 short-circuit; independently re-confirmed live via 03-VERIFICATION.md's curl-based re-proof and this session's own Playwright MCP live testing of the JWT bound/no-sliding-refresh, CORS allow-list enforcement, CORS fail-fast-on-missing-config, mutation-partition atomicity/isolation under real concurrent load, and identical failed-login response shape) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-31
