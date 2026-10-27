# Milestones

## v1.0 Security Hardening (Shipped: 2026-10-27)

**Delivered:** Closed the identified security and reliability gaps (EOL frontend, leaked credentials, permissive CORS/JWT, unbounded token store, non-atomic writes, no audit trail, no tests) without breaking existing functionality.

**Phases completed:** 5 phases, 23 plans, 55 tasks
**Timeline:** 60 days (2026-08-28 → 2026-10-27), 127 commits
**Code changes:** 95 non-planning files, +13,407 / −17,617 lines
**Closeout type:** override_closeout — no milestone audit run; all 5 phases verified and 20/20 requirements complete. Known deferred items: 4 (see STATE.md Deferred Items)

**Key accomplishments:**

- Angular upgraded 13.3.0 → 22 in nine bisectable `ng update` hops (TypeScript 6.0, RxJS 7.8, Jasmine/Karma bumped), architecture preserved, Playwright UAT approved
- Leaked IGDB/Steam/JWT secrets rotated and proven dead; secrets moved to `dotnet user-secrets`/env vars; IGDB token request uses a form-encoded body; Steam key redacted from logs (including HttpClient logging); gitleaks pre-commit hook installed
- JWT cut to 24h with HS256 pinning and explicit issuer/audience/lifetime validation; CORS config-sourced with explicit header/method allow-lists; per-user `mutations` rate limit on all write endpoints; failed logins, CORS rejections and rate-limit rejections logged
- `GameManager` writes atomic (EF + Dapper share one transaction); SQL-side `ModifiedAt`/`DeletedAt` stamps; `SteamTokenCleanupWorker` bounds `SteamTokenStore`; silent external-API catches now surface failure counts via `WorkerStatus`
- Cross-user bugs closed: session-planner game-library leak and IGDBId upsert deactivating other users' games; Steam OpenID callback now fails closed
- Test coverage from zero: 61 backend xUnit tests, 8 Angular specs, 16-test re-runnable Playwright full-stack UAT

**Known deferred items:**

- Image worker skips cover fetch when `IGDBUpdatedAt` is set (api, minor)
- Portal carries a likely wrong IGDBId (database, minor)
- Steam OpenID follow-ups S5–S7 (auth, minor)
- UI copy and type-scale cleanup (frontend, minor)

**Archives:** [v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md) · [v1.0-REQUIREMENTS.md](milestones/v1.0-REQUIREMENTS.md) · `milestones/v1.0-phases/`

---
