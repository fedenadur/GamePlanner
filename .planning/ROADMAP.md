# Roadmap: GamePlanner — Security Hardening Milestone

## Overview

This milestone closes the concrete security and reliability gaps already identified in GamePlanner's codebase (`.planning/codebase/CONCERNS.md`) without breaking existing functionality. Work proceeds in risk-isolated stages: the Angular 13 EOL upgrade goes first in isolation against the unchanged, currently-permissive backend so any regression can only be attributed to the frontend; external credential handling (IGDB/Steam) is fixed next since it's the most self-contained backend change; auth/CORS/rate-limiting hardening follows once the upgraded frontend's real contract is known; the `GameManager` transaction fix and audit trail land together since they touch the same write path; and a final combined phase verifies the whole hardened system end-to-end. This is a horizontal-layers milestone (technical hardening, not new user-facing capability), so phases are organized by technical concern rather than vertical feature slices.

## Phases

**Phase Numbering:**

- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Angular EOL Upgrade** - Move the Angular SPA from the unpatched 13.3.0 to a current LTS, sequentially, with no functional regressions (completed 2026-08-29)
- [ ] **Phase 2: Credential & Secrets Hardening** - Stop leaking IGDB/Steam credentials via URLs/logs/committed config, rotate exposed secrets, and stop swallowing external-API failures silently
- [ ] **Phase 3: Auth, CORS & Rate Limiting Hardening** - Tighten JWT lifetime/validation, lock down CORS to the real frontend contract, extend rate limiting to data-mutation endpoints, and log security-relevant rejections
- [ ] **Phase 4: Data Layer Reliability & Audit Trail** - Make `GameManager` writes atomic, add `ModifiedAt`/`DeletedAt` audit fields, and stop unbounded `SteamTokenStore` growth
- [ ] **Phase 5: Regression Testing & Full-Stack Verification** - Add targeted tests for every hardening-touched path and validate the combined frontend+backend contract end-to-end

## Phase Details

### Phase 1: Angular EOL Upgrade

**Goal**: The frontend runs on a current, supported Angular LTS instead of the unpatched Angular 13.3.0, with all existing functionality intact.
**Depends on**: Nothing (first phase; isolated against the unchanged, currently-permissive backend so regressions can only be attributed to the upgrade)
**Requirements**: DEP-01, DEP-02
**Success Criteria** (what must be TRUE):

  1. The Angular version (per `package.json`/`ng version`) is at the target current LTS, reached by applying each major-version hop sequentially with no version skipped
  2. TypeScript, RxJS, zone.js, Jasmine, and Karma are all at versions compatible with the upgraded Angular version
  3. Every existing user workflow (login, backlog CRUD, session tracking, journal entries, IGDB/Steam/HLTB sync views) behaves identically in the browser after the upgrade, confirmed by manual walkthrough
  4. `npm run build` and the existing test command both complete without errors on the upgraded stack

**Plans:** 6/6 plans complete

Plans:
**Wave 1**

- [x] 01-01-PLAN.md — Baseline verification + tracer hop 13 -> 14

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 01-02-PLAN.md — Hops 14 -> 15 -> 16 -> 17 (first application-builder prompt)

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 01-03-PLAN.md — Hops 17 -> 18 -> 19 -> 20 (Karma default shift)

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 01-04-PLAN.md — Hop 20 -> 21 + Node.js runtime bump to satisfy Angular 22

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 01-05-PLAN.md — Final hop 21 -> 22 (TypeScript 6.0) + Jasmine/Karma/RxJS bump

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 01-06-PLAN.md — npm audit, DEP-01/DEP-02 evidence record, manual UAT walkthrough

**Cross-cutting constraints:**

- npm run build exits 0 after each of the three hops
- npm test -- --watch=false exits 0 after each of the three hops (0 specs discovered is a PASS)
- The dev server boots and serves an app-root shell after each of the three hops (D-04)
- Exactly three new commits exist, one per hop, in ascending version order (D-05)

**UI hint**: yes

### Phase 2: Credential & Secrets Hardening

**Goal**: External API credentials (IGDB, Steam) are no longer exposed via URLs, logs, or committed config, previously-leaked credentials are rotated, and external-API failures are handled explicitly instead of swallowed.
**Depends on**: Nothing (independent of Phase 1; the most self-contained backend change, confined to `IgdbService.cs`/`SteamService.cs`/config)
**Requirements**: CRED-01, CRED-02, CRED-03, CRED-04, CRED-05, REL-03
**Success Criteria** (what must be TRUE):

  1. IGDB token requests send credentials in a form-encoded POST body — a live token-fetch call succeeds and no credential appears in the outgoing request URL
  2. The Steam API key never appears in application logs or unhandled exception output, confirmed by triggering a Steam API error and inspecting the log output
  3. `appsettings.Development.json` contains no secrets; running the app locally reads them from `dotnet user-secrets`, and non-dev environments read them from environment variables
  4. The IGDB and Steam credentials previously committed to source control are rotated and the old values no longer authenticate against the respective provider
  5. A secret-scanning pre-commit hook (gitleaks or trufflehog) blocks a test commit containing a fake secret pattern
  6. External API calls (IGDB/Steam/HLTB) no longer use a broad silent `catch (Exception)` — failures are logged with severity and surfaced rather than silently dropped

**Plans:** 1/5 plans executed

Plans:
**Wave 1**

- [x] 02-01-PLAN.md — Credential transport tracer: user-secrets/env-var migration + form-encoded IGDB token body + redacted Steam errors (CRED-01/02/03)
- [ ] 02-02-PLAN.md — WorkerStatus partial-failure contract: nullable columns, SetCompletedAsync overload, numbered migration (REL-03)

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] 02-03-PLAN.md — Instrument the 8 Manager/Service catch sites and forward failure counts from 4 workers (REL-03)
- [ ] 02-04-PLAN.md — Rotate the committed IGDB/Steam credentials and the JWT signing secret, prove the old values are dead (CRED-04)

**Wave 3** *(blocked on Wave 2 completion)*

- [ ] 02-05-PLAN.md — gitleaks pre-commit hook, install script, and one-time setup docs (CRED-05)

### Phase 3: Auth, CORS & Rate Limiting Hardening

**Goal**: The JWT auth flow, CORS policy, and rate limiting are deliberately configured for the app's real trust boundary, and security-relevant rejections are visible in logs.
**Depends on**: Phase 1 (the CORS allow-list must be derived from the final upgraded Angular app's actual outgoing request headers, per research sequencing)
**Requirements**: AUTH-01, AUTH-02, NET-01, NET-02, NET-03, OBS-01
**Success Criteria** (what must be TRUE):

  1. Issued JWTs expire after a deliberately short, documented lifetime instead of 7 days, confirmed by inspecting a decoded token's `exp` claim
  2. `TokenValidationParameters` explicitly validates issuer, audience, and lifetime — a tampered or expired token is rejected
  3. CORS allowed origins load from environment-based configuration (no hardcoded `localhost:4200`), restricted to explicit headers/methods (no `AllowAnyHeader`/`AllowAnyMethod`)
  4. The real Angular SPA (post Phase 1 upgrade) can still call every endpoint it needs with no CORS errors, confirmed by a full manual browser walkthrough
  5. Rate limiting rejects excessive requests on data-mutation endpoints (game/journal/session POST/PUT/DELETE), partitioned per JWT user ID, so one user's rejections don't affect another user's requests
  6. Failed logins, CORS rejections, and rate-limit rejections all appear in a central log stream, confirmed by triggering each and inspecting the log

**Plans**: TBD

### Phase 4: Data Layer Reliability & Audit Trail

**Goal**: Game data writes are atomic and auditable, and the Steam token store no longer grows unbounded.
**Depends on**: Phase 3 (sequenced after auth/CORS hardening per research risk-isolation ordering; no hard technical dependency)
**Requirements**: REL-01, REL-02, AUDIT-01, AUDIT-02
**Success Criteria** (what must be TRUE):

  1. Creating, updating, or deleting a game succeeds or fails as a single atomic unit — a forced failure mid-write leaves neither the stored-proc write nor the EF write applied
  2. `ModifiedAt` is populated with the current timestamp whenever a mutable entity (game, journal entry, session) is updated
  3. `DeletedAt` is populated whenever a soft-deletable entity is marked `IsActive = false`
  4. Expired entries in `SteamTokenStore` are automatically removed by a background worker without manual intervention, confirmed by observing store size stay bounded over time

**Plans**: TBD

### Phase 5: Regression Testing & Full-Stack Verification

**Goal**: The hardening changes across frontend and backend are proven correct together, not just individually.
**Depends on**: Phases 1-4 (validates the combined result of all prior phases)
**Requirements**: TEST-01, TEST-02
**Success Criteria** (what must be TRUE):

  1. Automated tests exist and pass for each hardening-touched area: auth (JWT lifetime/validation), `GameManager` (transaction atomicity), `IgdbService`, `SteamService`, and `SteamTokenStore` (cleanup)
  2. A full manual UAT pass through every major user workflow (login, backlog CRUD, session tracking, journal, IGDB/Steam/HLTB sync) succeeds with the upgraded Angular client and hardened backend running together
  3. A dependency vulnerability scan (`npm audit` / `dotnet list package --vulnerable`) shows no known-EOL or high-severity vulnerable packages remaining from the pre-hardening baseline

**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Angular EOL Upgrade | 6/6 | Complete    | 2026-08-29 |
| 2. Credential & Secrets Hardening | 1/5 | In Progress|  |
| 3. Auth, CORS & Rate Limiting Hardening | 0/TBD | Not started | - |
| 4. Data Layer Reliability & Audit Trail | 0/TBD | Not started | - |
| 5. Regression Testing & Full-Stack Verification | 0/TBD | Not started | - |
