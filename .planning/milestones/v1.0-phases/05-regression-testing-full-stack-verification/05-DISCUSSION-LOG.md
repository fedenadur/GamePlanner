# Phase 5: Regression Testing & Full-Stack Verification - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-24
**Phase:** 05-regression-testing-full-stack-verification
**Areas discussed:** Pending todo, IGDB/Steam tests, Auth tests, Frontend tests, Full-stack UAT, Steam UAT steps, Vulnerabilities, Half-Life fix, Steam OpenID review

---

## Pending todo (Half-Life IGDBId)

| Option | Description | Selected |
|--------|-------------|----------|
| No, leave for later | Phase 5 is tests/verification only | |
| Yes, fold in | Investigate and fix in Phase 5 | ✓ |

## IGDB/Steam tests

| Option | Description | Selected |
|--------|-------------|----------|
| Stubbed, offline | Fake HTTP responses; fast, deterministic | ✓ |
| Stubbed + 1 live test | Plus an optional real-API test | |
| Live APIs only | More faithful, flaky | |

## Auth tests

| Option | Description | Selected |
|--------|-------------|----------|
| Token + validation | 24h lifetime; reject expired / bad signature / wrong issuer-audience; in-process host | ✓ |
| Also rate limit + CORS | Adds Phase 3 limiter and CORS checks | |

## Frontend tests

| Option | Description | Selected |
|--------|-------------|----------|
| Few, targeted | Interceptor, guard, delete-failure message (5-8 tests) | ✓ |
| None, backend only | Rely on Playwright UAT | |

## Full-stack UAT

| Option | Description | Selected |
|--------|-------------|----------|
| Playwright script in repo | Re-runnable, uses uat-test account | ✓ |
| One-off Playwright pass | Results only in UAT.md | |

## Steam UAT steps

| Option | Description | Selected |
|--------|-------------|----------|
| User does only Steam steps | Claude verifies via SQL/logs | ✓ |
| Skip | Mark as untested | |

## Vulnerabilities

| Option | Description | Selected |
|--------|-------------|----------|
| Fix high/critical | Unfixable ones documented as accepted risk | ✓ |
| Fix everything possible | Includes moderate/low | |
| Document only | No updates | |

## Half-Life fix

| Option | Description | Selected |
|--------|-------------|----------|
| Fix data + scan for others | Numbered SQL script, check other shared-IGDBId pairs | ✓ |
| Fix only those two rows | | |

## Steam OpenID review

| Option | Description | Selected |
|--------|-------------|----------|
| Defer to another phase | Keep Phase 5 test-only | |
| Fold into Phase 5 | Review callback validation + tests | ✓ |

## Claude's Discretion

- Test project layout, in-process host helper, HTTP stub style
- Playwright server startup and dev-cert handling
- Exact UAT steps and assertions; plan ordering

## Deferred Ideas

- Rate-limit/CORS automated tests
- Moderate/low vulnerability fixes
- `/workers` UTC-as-local display bug
