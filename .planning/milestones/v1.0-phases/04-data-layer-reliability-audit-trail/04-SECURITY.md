---
phase: 04
slug: data-layer-reliability-audit-trail
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-10-24
---

# Phase 04 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| API → SQL Server | Dapper stored procedures and EF `SaveChangesAsync` write one logical operation | Game rows, genre/tag junction rows (user data) |
| Authenticated client → game mutation endpoints | Client-supplied name, estimate, genres, tags reach both write mechanisms | Untrusted user input |
| Test harness → SQL Server | `GamePlanner.Tests` opens a real connection to the dev database | Probe rows (`gsd-probe-` prefix) |
| Steam OAuth callback → `SteamTokenStore` | Short-lived code and auth payload (issued JWT) held in process memory | Credentials (high) |
| `SteamTokenStore` → background sweep | Process-internal task mutates the dictionary request threads read | Credentials (high) |
| Worker → `WorkerStatus` / `/workers` dashboard | Worker-run facts persisted and shown to the user | Counts and timestamps only |
| Background sync workers → `Games` | Worker-driven writes into per-user rows | User data |
| API error response → browser UI | Server-side failure detail could reach rendered page text | Error details (medium) |
| Migration script → live database | Irreversible schema change against personal game data | Schema |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-04-01 | Tampering | `GameManager` create/update paths | high | mitigate | `BeginTransactionAsync` shared with Dapper via `tx.GetDbTransaction()` (`GameManager.cs:101,129,165`) | closed |
| T-04-02 | Repudiation | `Games.ModifiedAt` / `DeletedAt` | medium | mitigate | `GETUTCDATE()` in procedures (`Database/36_AddAuditColumns_ModifiedAt_DeletedAt.sql`); no C# assignment found | closed |
| T-04-03 | Tampering | `sp_DeleteGame` repeat invocation | medium | mitigate | `WHERE Id = @Id AND IsActive = 1` guard | closed |
| T-04-04 | Denial of Service | Transaction hold time on `Games` | low | accept | See Accepted Risks | closed |
| T-04-05 | Information Disclosure | Test-harness connection string | medium | mitigate | Env override `ConnectionStrings__DefaultConnection`, fallback is `Trusted_Connection=True` with no credential (`GameManagerAtomicityTests.cs:22-23`) | closed |
| T-04-06 | Tampering | NuGet packages in `GamePlanner.Tests` | medium | mitigate | Only `dotnet new xunit` template packages (xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, coverlet.collector) + `ProjectReference` | closed |
| T-04-07 | Tampering | Test rows in dev database | low | accept | See Accepted Risks | closed |
| T-04-08 | Information Disclosure | `SteamTokenCleanupWorker` logging / `WorkerStatus` | high | mitigate | Only the integer count is logged; `AuthResponse` count in worker file = 0. Live log confirmed count-only (04-UAT test 1) | closed |
| T-04-09 | Denial of Service | Unbounded `SteamTokenStore` growth | high | mitigate | `AddHostedService<SteamTokenCleanupWorker>()` (`Program.cs:53`), 10-min sweep; live removal of 3 expired tokens confirmed (04-UAT test 1) | closed |
| T-04-10 | Denial of Service | Sweep racing a concurrent redeem | medium | mitigate | `Expiry < now` predicate + `ConcurrentDictionary.TryRemove` (`SteamTokenStore.RemoveExpired`) | closed |
| T-04-11 | Spoofing | Optional `lifetime` on `Store` | medium | mitigate | `AuthController.cs:68` calls `Store(result)` with one argument; `lifetime` not in any controller or DTO | closed |
| T-04-12 | Repudiation | Quiet sweeps leave no log line | low | accept | See Accepted Risks | closed |
| T-04-13 | Tampering | `sp_UpsertIGDBGame` row-creating branch | high | mitigate | Live `OBJECT_DEFINITION` has no `INSERT`; script scoped `WHERE IGDBId = @IGDBId AND UserId = @UserId` | closed |
| T-04-14 | Tampering | Cross-user `Games` collision on `IGDBId` | high | mitigate | Live index `IX_Games_IGDBId` `is_unique = 0`; per-user dedup in `CreateFromIgdbAsync` (`GameManager.cs:96`); live cross-user case passed (04-UAT test 3) | closed |
| T-04-15 | Information Disclosure | Delete failure message | medium | mitigate | Parameterless error callback with fixed literal (`game-list.component.ts:135`); live render confirmed (04-UAT test 2) | closed |
| T-04-16 | Denial of Service | `ToDictionary` keyed on `IGDBId` | medium | mitigate | `IsActive && UserId == userId` filter before keying (`CalendarController.cs:62`, `TrackedGamesManager.cs:27`) | closed |
| T-04-17 | Tampering | Irreversible index drop | high | mitigate | Blocking `checkpoint:decision` ran before the drop; user decision recorded in 04-03-SUMMARY key-decisions | closed |
| T-04-18 | Repudiation | Within-user duplicate `IGDBId` rows | low | accept | See Accepted Risks | closed |
| T-04-SC | Tampering | npm/pip/cargo installs | low | accept | See Accepted Risks | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-04-01 | T-04-04 | Single-user personal app; one procedure call + one `SaveChangesAsync` per transaction — no realistic lock-contention attack surface | Plan 04-01 threat model | 2026-08-31 |
| AR-04-02 | T-04-07 | Probe rows use `gsd-probe-` prefix, removed in `finally`, post-run leak assertion | Plan 04-01 threat model | 2026-08-31 |
| AR-04-03 | T-04-12 | Quiet sweeps traded for log-signal quality; `WorkerStatus.LastSuccessAt` records every run | Plan 04-02 threat model | 2026-08-31 |
| AR-04-04 | T-04-18 | DB-level within-user protection removed by design; app-layer dedup covers the only creation path | Plan 04-03 threat model | 2026-08-31 |
| AR-04-05 | T-04-SC | No package-manager install in plans 04-02 / 04-03 | Plans 04-02, 04-03 threat models | 2026-08-31 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-24 | 19 | 19 | 0 | /gsd-secure-phase (L1 orchestrator checks; auditor skipped per short-circuit rule) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-24
