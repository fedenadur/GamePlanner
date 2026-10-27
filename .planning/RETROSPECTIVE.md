# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.0 — Security Hardening

**Shipped:** 2026-10-27
**Phases:** 5 | **Plans:** 23 | **Sessions:** not tracked

### What Was Built

- Angular 13.3.0 → 22 upgrade in nine bisectable hops, supporting deps aligned
- Secrets out of source control, rotated, and guarded by a gitleaks pre-commit hook; credential leaks via URLs and logs closed
- Hardened JWT, CORS, per-user write rate limiting, and security event logging
- Atomic `GameManager` writes, SQL-side audit stamps, bounded `SteamTokenStore`, failure counts surfaced via `WorkerStatus`
- First automated tests: 61 xUnit, 8 Karma specs, 16-test Playwright full-stack UAT

### What Worked

- Risk-isolated phase ordering (frontend upgrade first against an unchanged backend) kept regressions attributable
- One commit per Angular hop made every step bisectable
- Playwright-driven UAT replaced manual walkthroughs and is now re-runnable
- Live proof of fixes (old credentials returning 400/403, a live 429, injected-failure rollback) rather than code-only claims

### What Was Inefficient

- 7-week gap between Phase 3 (2026-08-31) and Phase 4 (2026-10-24)
- Pre-existing WIP in the working tree (`game-detail.component.*`, `GameImageManager.cs`) forced repeated isolate/restore manoeuvres around codemods and merges
- Several SUMMARY.md files lack a one-liner, so the milestone-close accomplishment extraction picked up deviation lines
- sqlcmd defaults (`QUOTED_IDENTIFIER OFF`) and `SET NOCOUNT ON` caused latent stored-procedure bugs found only under test

### Patterns Established

- Pin `SET ANSI_NULLS ON` / `SET QUOTED_IDENTIFIER ON` before every `CREATE OR ALTER PROCEDURE`
- EF-owned transaction shared with Dapper via `tx.GetDbTransaction()` for write paths
- Out-of-scope findings filed as todos, not fixed inline, unless they sit on the same trust boundary
- Guarded, reversible data-fix scripts with a dry run and a user decision checkpoint

### Key Lessons

1. Commit or stash unrelated WIP before a milestone starts; it taxed every phase that touched the same files.
2. Always write the SUMMARY.md one-liner; tooling depends on it.
3. Test against a real SQL Server: both stored-procedure bugs were invisible to code review.
4. Security reviews of a trust boundary (Steam OpenID) surface more than the planned scope; budget for it.

### Cost Observations

- Model mix: not tracked
- Sessions: not tracked
- Notable: Phases 2–4 plans mostly ran 5–55 min; Phase 1 hops and the Phase 5 data fix were the long poles

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Sessions | Phases | Key Change |
|-----------|----------|--------|------------|
| v1.0 | n/a | 5 | First GSD milestone; Playwright UAT adopted |

### Cumulative Quality

| Milestone | Tests | Coverage | Zero-Dep Additions |
|-----------|-------|----------|-------------------|
| v1.0 | 85 (61 xUnit + 8 Karma + 16 Playwright) | not measured | n/a |

### Top Lessons (Verified Across Milestones)

1. (Needs a second milestone to cross-validate)
