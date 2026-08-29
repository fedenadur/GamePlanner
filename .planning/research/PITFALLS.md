# Pitfalls Research

**Domain:** Retrofit security hardening on a brownfield ASP.NET Core 8 + Angular SPA with zero test coverage
**Researched:** 2026-08-28
**Confidence:** MEDIUM-HIGH (backend/general findings HIGH — based on documented ASP.NET Core behavior and this codebase's own CONCERNS.md; Angular version specifics MEDIUM — web-sourced, verify exact version numbers at execution time since Angular ships a new major every ~6 months)

## Critical Pitfalls

### Pitfall 1: Credential migration breaks integrations because the "fix" changes more than the credential

**What goes wrong:**
Moving `ClientSecret`/API keys from URL query strings to headers or POST body is framed as a pure security fix, but in `IgdbService.cs` and `SteamService.cs` it also touches the HTTP call shape (verb, content-type, whether query params for non-secret args like `vanityurl`/`steamid` stay in the URL or move too). A common failure: someone rewrites the token request as `FormUrlEncodedContent` correctly, but then also "cleans up" the adjacent `GetPlayerSummaries`/`GetOwnedGames` calls and accidentally drops a required query parameter (e.g., `format=json`, `include_appinfo=true`) or double-encodes `steamId`. Since IGDB's OAuth2 token endpoint truly requires form-body per Twitch spec, but the Steam Web API only supports the API key via `key` query param or `x-webapi-key` header (not body) — treating both services identically produces a subtly broken Steam integration that still returns 200 with empty/partial data rather than erroring, because Steam data responses degrade gracefully in this codebase's silent-catch pattern.

**Why it happens:**
Two different external APIs get bundled into one "move creds out of URLs" task, but they have different correct target locations (IGDB: body; Steam: header, since Steam's API has no body-based key mechanism). Developers pattern-match "security fix = same shape everywhere" instead of reading each API's actual auth contract.

**How to avoid:**
- Treat IGDB and Steam as two separate, independently verified changes — do not batch them into one commit/PR.
- For IGDB: move `client_id`/`client_secret`/`grant_type` into `FormUrlEncodedContent` in the POST body (matches Twitch/IGDB OAuth2 spec exactly).
- For Steam: move the `key` parameter to the `x-webapi-key` header; leave non-secret params (`steamids`, `vanityurl`, `format`) in the query string since Steam's API expects them there.
- After each change, make one real live call against the actual external API (not just a mock) before merging — this is the one place in the milestone where hitting the real IGDB/Steam sandbox is worth the manual step, since a mocked test would validate the wrong contract if the mock assumptions are wrong.
- Grep for every call site of the changed method (`IgdbService`, `SteamService`) to confirm no caller still expects the credential to be embedded in a returned URL (e.g., logging code that prints `url` for debugging).

**Warning signs:**
- Steam sync worker logs show successful runs (200 OK, no exceptions) but `SteamPlaytimeSyncWorker` / library import silently stops updating data — the silent `catch (Exception)` pattern in this codebase (see CONCERNS.md) will hide a 401/403 as a "no data" outcome, not a visible error.
- IGDB token requests start returning 400 instead of a token — usually a body-encoding mismatch (form vs JSON) rather than a bad secret.

**Phase to address:**
Dedicated phase for "external credential migration" separate from CORS/rate-limiting work, with IGDB and Steam as distinct verification steps within it, sequenced early (before CORS/rate limiting) since it's the most isolated, highest-confidence-of-correctness change and unblocks confident testing of everything downstream.

---

### Pitfall 2: Tightening CORS/rate limiting silently breaks the frontend with no test to catch it

**What goes wrong:**
With zero test coverage and a hardcoded `AllowAnyHeader().AllowAnyMethod()` CORS policy today, the app currently "works" partly *because* CORS is wide open. Restricting to specific headers/methods (e.g., only `Content-Type, Authorization` and `GET,POST,PUT,DELETE`) can break a request that relies on a header the Angular `HttpClient` interceptor adds (e.g., a custom loading/tracing header, or `Accept`) without anyone realizing it, because the failure mode is a silent CORS preflight rejection in the browser console — not a server-side error, not a 500, not something a backend-only smoke test would ever catch. Similarly, adding rate limiting to data-modification endpoints (game create/update/delete, journal, sessions) with an aggressive per-user limit can break legitimate bulk operations this app already does internally — e.g., the IGDB/Steam sync workers or bulk journal imports may fire many rapid authenticated requests that now get 429'd, and because workers use `CancellationToken.None` and broad catch blocks, a 429 from a self-inflicted rate limit could be swallowed as "sync failed, will retry" indefinitely.

**Why it happens:**
CORS failures are invisible server-side (they never reach ASP.NET Core logs — the browser blocks the response client-side), so a backend developer hardening CORS has no signal that they broke something unless they manually exercise the actual SPA in a real browser after each change. Rate limiting failures look like normal, recoverable errors (429) and get absorbed by existing retry/silent-catch logic, delaying discovery.

**How to avoid:**
- For CORS: derive the exact allowed headers from the actual Angular `HttpClient` interceptor/service code (grep for `.set(` / `HttpHeaders` usage) instead of guessing a "reasonable" list. Test by running the real SPA against the hardened API manually (not curl) — curl bypasses browser CORS preflight entirely and will falsely appear to work.
- For rate limiting: audit which endpoints the 6 background workers and any bulk-import/sync flows call as authenticated requests, and either exempt worker-originated calls (e.g., a separate policy/partition keyed by a service-account claim) or set limits comfortably above observed worker call volume — do not pick a round number without checking.
- Add the "targeted tests" called for in PROJECT.md specifically as regression nets for these two changes: an integration test asserting a preflight `OPTIONS` request succeeds with the exact headers the SPA sends, and a test asserting N rapid requests from one worker-simulated identity do not trip the limiter while N+1 from a normal user does trip it at the intended threshold.
- Roll out CORS/rate-limit changes with a manual full-app walkthrough (login → browse → create/edit/delete a game → check sync workers still complete) before considering the phase done — this substitutes for missing E2E coverage.

**Warning signs:**
- Browser console (not server logs) shows `has been blocked by CORS policy` — must be checked manually since it won't show up in server-side monitoring.
- Background worker sync intervals show games/images/playtime silently going stale (last-synced timestamps not advancing) after the rate-limiting change ships, with no corresponding error in application logs.

**Phase to address:**
CORS and rate-limiting should be split into two separate phases (or at minimum two separately-verified plan items), each ending with a manual full-SPA regression pass, not just a unit test — because the failure mode for both is specifically invisible to server-side automated tests without deliberately simulating browser preflight and worker call patterns.

---

### Pitfall 3: Bundling Angular 13→current-LTS upgrade into the same milestone as backend hardening compounds risk instead of isolating it

**What goes wrong:**
Angular enforces sequential major-version upgrades — going from Angular 13 to current LTS is not one `ng update`, it's a chain through every intermediate major (13→14→15→16→17→18→19→20→21+), each with its own breaking changes: Ivy-related renderer changes, standalone components becoming the default schematic output, RxJS 7 API changes and eventual RxJS 8 interop concerns, zone.js becoming optional (zoneless change detection introduced as opt-in in later versions), TypeScript major bumps at multiple steps, and `HttpClientModule` deprecation in favor of `provideHttpClient()`. Doing this in the same milestone as backend CORS/auth hardening means that when something breaks in the app (e.g., login stops working, or the game detail image carousel misbehaves), there are now two independent, unrelated categories of simultaneous change (frontend framework internals + backend security policy) that could each explain it — with zero tests on either side to bisect blame. A CORS-preflight failure and an Angular 17 standalone-bootstrap misconfiguration produce visually similar symptoms (blank page / failed API calls) to someone without instrumentation.

**Why it happens:**
Both look like "hardening" in a PROJECT.md sense (EOL dependency = latent security risk), so they get scoped into one milestone under one banner. But they are orthogonal risk surfaces: one is "make existing behavior more restrictive" (backend), the other is "keep behavior identical while swapping the framework underneath it" (frontend). Conflating them removes the ability to isolate a regression's cause.

**How to avoid:**
- Sequence Angular upgrade as its own phase, completed and manually verified end-to-end (login, CRUD, image carousel, session planner, journal) *before* backend CORS/rate-limiting/credential changes land, OR fully after — never interleaved. See Pitfall 6 for the recommended order and why.
- Within the Angular upgrade itself, do not attempt to jump straight to the newest major via manual `package.json` edits — run `ng update @angular/core @angular/cli` one major version at a time as Angular's own tooling requires, committing after each successful major-version step so a regression can be bisected to a specific version bump.
- Treat RxJS and zone.js updates as part of the Angular update (they're pinned dependencies of it), not a separate manual bump — mismatched RxJS/zone.js versions relative to the Angular major in use is a common source of "phantom" change-detection or subscription bugs that look like application bugs.
- After the full upgrade chain, do a dedicated pass for deprecated API usage the upgrade may have silently left in place but only warns about (e.g., `HttpClientModule` still works for several majors after `provideHttpClient()` is introduced, so nothing forces the migration — but leaving it increases exposure to a future breaking removal).
- Because this app is `skipTests: true` today, this is also the natural moment to enable test generation, since new/upgraded components can get specs from the start rather than retrofitted.

**Warning signs:**
- Any regression discovered mid-milestone where root cause requires guessing between "backend policy change" and "framework upgrade" — if this happens even once, it confirms the sequencing risk materialized and the two workstreams should have been separated.
- Console warnings about deprecated APIs (`HttpClientModule`, old RxJS operators, `ViewChild` static resolution changes) accumulating without being triaged — signals the upgrade was done superficially (version bump only) rather than adopting the new patterns the version expects.

**Phase to address:**
Dedicated Angular-upgrade phase, isolated from backend hardening phases (see Pitfall 6 for ordering rationale). Verification for this phase should be a full manual UAT pass of every screen, since there is no Angular test suite yet to catch regressions automatically.

---

### Pitfall 4: "Fixing" SqlConnection/Dapper lifecycle without addressing the mixed-access-pattern root cause reintroduces the bug in a different form

**What goes wrong:**
The CONCERNS.md finding is that `GameManager.CreateAsync/UpdateAsync/DeleteAsync` create a fresh `SqlConnection` per call inside a `using` block — technically disposed correctly, but repeated pattern suggests no shared connection-pooling strategy and mixes Dapper (stored-proc writes) with EF Core (LINQ reads) against the same underlying tables. A narrow "fix" that just double-checks `using`/`await using` and adds explicit `Open()` calls treats the symptom, not the risk: EF Core's `DbContext` and a manually-opened `SqlConnection` are two independent connections to the same database with no coordinated transaction scope. If a stored-proc write (Dapper) and a related EF read/write need to be atomic (e.g., create game via stored proc, then update a related EF-tracked entity), there is no ambient transaction linking them — a partial failure leaves inconsistent state. Fixing connection disposal without also deciding whether Dapper calls should share the EF `DbContext`'s underlying connection (via `context.Database.GetDbConnection()`) or stay fully separate can make people believe the resource-leak class of bug is "solved" when the more important consistency risk (no shared transaction) remains untouched.

**Why it happens:**
The reported symptom (connection lifecycle) is narrower and easier to grep-fix than the underlying architectural mix (EF + Dapper against the same tables), so a time-boxed hardening task naturally gravitates to the visible, literal issue in CONCERNS.md rather than the structural one it points at.

**How to avoid:**
- Scope this fix explicitly to what's in PROJECT.md: fix disposal/handling in the four flagged `GameManager` methods, and do not expand into a full EF/Dapper consolidation (that's explicitly out of scope per PROJECT.md — "Mixed Data Access Patterns" is a Medium-priority item deferred to a future milestone).
- Within that narrow scope, still confirm each Dapper call explicitly opens the connection before use (`await conn.OpenAsync()`) since Dapper does not auto-open, and verify no method leaves a connection open across an `await` boundary that calls back into EF Core against the same physical connection pool — this is the specific way "connection pool exhaustion" as named in CONCERNS.md actually manifests under load.
- Add the targeted regression test called for in PROJECT.md here: a test that runs Create/Update/Delete repeatedly (e.g., 50–100 iterations) against a real or test SQL Server instance and asserts no connection pool growth / no `InvalidOperationException: Timeout expired` — this is the only way to actually verify a leak is gone, since a single successful call proves nothing about pooling.
- Do not silently "fix" this by switching these methods to EF Core entirely — CLAUDE.md's convention (stored procs only for writes) is a hard constraint for this milestone; the fix must stay within Dapper + stored procedures.

**Warning signs:**
- Load-testing the fixed endpoints reveals connection count climbing under sustained rapid calls (visible via SQL Server's `sys.dm_exec_connections` or `sp_who2`) — the correctness fix without a pooling regression test would ship undetected.
- Any place a Dapper call and an EF Core call touch the same `GameId` in the same logical operation — flag it as a latent consistency gap even if out of scope for this milestone; document it rather than silently leaving it.

**Phase to address:**
Backend hardening phase covering `GameManager` — keep tightly scoped to connection disposal per PROJECT.md, but require a load-style regression test (not just a single happy-path unit test) as the phase's done-criterion, and explicitly note the EF/Dapper consistency gap as a documented follow-up rather than pretending it's resolved.

---

### Pitfall 5: Adding audit fields / soft-delete tracking to tables with existing rows produces NULL-filled history that looks like real data

**What goes wrong:**
Adding `CreatedBy`, `ModifiedBy`, `ModifiedAt`, `DeletedAt` columns to `Game`, `NowPlayingGame`, `GameJournalEntry` (etc.) via a new numbered SQL script is straightforward for new rows going forward, but every existing row has no real history for these fields — there is no way to know who actually created or last modified a game that already exists. Two failure modes follow: (1) the migration script backfills these columns with a plausible-looking but fabricated value (e.g., `CreatedBy = UserId` from the row itself, `ModifiedAt = GETUTCDATE()` at migration time) that looks like legitimate audit data but actually just encodes "when the migration ran," silently corrupting the audit trail's meaning from day one; or (2) the columns are left `NULL` for pre-existing rows, but the application code written against the new schema assumes `ModifiedAt`/`CreatedBy` are always populated (e.g., sorts by `ModifiedAt`, displays "last edited by X"), causing null-reference exceptions or nonsensical UI ("last modified by: (empty)") the first time an old, un-migrated row is touched.
For soft-delete specifically: `IsActive` already exists and is presumably already used in `WHERE` clauses across `GameManager` and elsewhere. Adding `DeletedAt` without auditing every existing query path that filters on `IsActive` risks two independent flags (`IsActive = 0` set without `DeletedAt`, or vice versa via a code path that misses one) drifting out of sync — e.g., if any query is later written keying off `DeletedAt IS NOT NULL` instead of `IsActive = 0`, previously-deleted rows won't be found as deleted.

**Why it happens:**
Audit/soft-delete fields feel like a pure additive schema change ("just add a column"), so the backfill-vs-null decision and the "who sets these fields and where" question get skipped in favor of just running an `ALTER TABLE`. The stored-procedure convention (writes only via sproc) means every existing `sp_Insert*`/`sp_Update*`/`sp_Delete*` procedure must be updated to actually populate the new columns — missing even one write path (e.g., `sp_UpsertIGDBGame`, already flagged in CONCERNS.md as having its own data-integrity bug) means that path continues silently writing `NULL` or stale audit data forever.

**How to avoid:**
- Decide explicitly, per column, whether pre-existing rows get `NULL` (honest — "we don't know") or a clearly-sentinel backfill value (e.g., a reserved "system/unknown" user ID and a fixed backfill timestamp, not `GETUTCDATE()` at migration time, so it's visually distinguishable from real audit data) — document the choice in the migration script's comment header.
- Make the application layer explicitly null-tolerant for these fields wherever pre-existing data might surface them (UI: show "unknown" rather than crashing or showing blank).
- Enumerate every stored procedure that inserts/updates the affected tables (including `sp_UpsertIGDBGame`, `sp_UpsertGameImage`, and any others touching `Game`/`NowPlayingGame`/`GameJournalEntry`) and update all of them together in the same phase — a partial rollout where only `GameManager`'s direct CRUD sprocs get the new columns but sync-worker-driven upsert procs don't is the single most likely way this pitfall manifests, since IGDB/HLTB sync writes are a large fraction of total writes to `Game`.
- For soft-delete: keep `IsActive` as the single source of truth for "is this deleted," and treat `DeletedAt` purely as a timestamp annotation set at the same time `IsActive` flips to 0 in the same sproc call — never introduce a second independent code path that can set one without the other.
- Since a numbered SQL script here is a one-way door (per CLAUDE.md, applied manually, no rollback tooling), write and dry-run the migration against a copy of production-shaped data (or the real local DB, given this is a personal-use app) before considering the phase done, and confirm row counts/column population match expectations post-migration.

**Warning signs:**
- Any UI screen suddenly showing "Modified by: (blank)" or a crash on an old game record after the audit-field feature ships — indicates a null-handling gap the migration didn't account for.
- A soft-deleted-then-restored item (if that flow exists) leaves `DeletedAt` populated while `IsActive` is back to 1, or vice versa — indicates a write path was missed.

**Phase to address:**
Dedicated phase for audit fields/soft-delete, sequenced after the SqlConnection/Dapper fix (since it touches the same stored procedures) and cross-checked against the full list of write sprocs (including the two already-flagged buggy ones in `Database/34_...sql` and `Database/32_...sql`) so those known bugs aren't propagated into the new audit columns.

---

### Pitfall 6: Getting the Angular-upgrade-vs-backend-hardening sequencing wrong in either direction

**What goes wrong:**
There are two plausible orderings and each has a distinct failure mode if done carelessly:

- **Backend hardening first, Angular upgrade second:** The credential migration, CORS restriction, and rate limiting all get verified against the *current* Angular 13 SPA. Then the Angular upgrade lands afterward and, per Pitfall 3, introduces its own regressions (standalone bootstrap, `provideHttpClient()` migration, RxJS operator changes). If the upgraded SPA's `HttpClient` now sends a different header set (e.g., `provideHttpClient(withInterceptors(...))` behaves differently than the old `HttpClientModule` + class-based interceptor, or Angular's new fetch-based backend changes header casing/order), the already-hardened CORS allow-list can silently reject the *upgraded* app even though it was verified correct against the old one — reopening a problem that was supposedly closed.
- **Angular upgrade first, backend hardening second:** The upgrade is verified against the *existing* permissive CORS/rate-limit/credential setup, which is safer for isolating upgrade-specific regressions (per Pitfall 3's reasoning — fewer simultaneous variables). But if the upgrade is verified only against the old backend and the team assumes "frontend is done, move on," backend hardening changes later can still break the newly-upgraded frontend in ways that get misattributed to "leftover Angular upgrade bugs" instead of the real cause (a CORS/rate-limit change), since the upgrade is fresh in everyone's mind as the likely suspect.

Either order carries risk; the actual mistake is not the order itself but **treating either phase as "done and forgotten"** before the other lands, and not re-verifying the CORS/header allow-list against the *final* state of the Angular HTTP client once both changes are in place.

**Why it happens:**
Milestone planning naturally wants to parallelize or sequence-and-forget independent-seeming workstreams to save time, but CORS/header allow-lists are a *contract* between exactly these two systems — any change to either side invalidates prior verification of that contract.

**How to avoid:**
- Sequence Angular upgrade **before** backend CORS/rate-limiting/credential hardening. Rationale: the Angular upgrade is the more mechanically complex, higher-regression-count change (multiple major versions, schematics, RxJS/zone.js) and benefits most from being isolated against a completely unchanged, known-permissive backend (today's wide-open CORS) — this removes CORS as a variable while debugging what are otherwise pure frontend regressions. Doing backend hardening first would mean every Angular-upgrade regression has to be triaged against two moving targets.
- Once the Angular upgrade is complete and manually verified (full UAT pass across all screens), proceed to backend hardening — and explicitly re-derive the CORS allowed-headers list from the *upgraded* Angular app's actual outgoing requests (via browser devtools), not from the pre-upgrade app's behavior, since `provideHttpClient()` and any interceptor rewrites during the upgrade can change what's sent.
- After backend hardening lands, do one final full-stack UAT pass with both changes in place together — this is the only point that actually validates the end-to-end contract, since each phase's own verification only covers its half.
- If schedule pressure forces overlap, at minimum keep the credential-migration (Pitfall 1) and rate-limiting (Pitfall 2) work decoupled from CORS specifically, since CORS is the piece with the tightest coupling to Angular's HTTP layer — CORS should be the very last backend change made, after the Angular upgrade is fully settled.

**Warning signs:**
- A CORS or 401/403 issue reappears after backend hardening was already "verified working" earlier in the milestone — this is the specific signal that the contract was re-broken by the other workstream landing afterward.
- Bug reports/observations get labeled "probably an Angular upgrade leftover" without checking whether a backend change shipped in between the upgrade and the observation.

**Phase to address:**
Explicit phase ordering in the roadmap: Angular upgrade phase first (fully isolated, verified against unchanged backend), then backend hardening phases (credential migration → SqlConnection/Dapper fix → audit fields → rate limiting → CORS last), with a final combined full-stack UAT phase or step that re-verifies the CORS/header contract against the final state of both sides.


## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|-----------------|------------------|
| Backfilling audit columns with `GETUTCDATE()`/self-referential values instead of `NULL` | Migration "looks complete," no null-handling code needed | Fabricated audit history indistinguishable from real data — defeats the purpose of adding audit fields | Never — use explicit `NULL` or a clearly-marked sentinel instead |
| Bumping Angular via direct `package.json` version edits instead of `ng update` per major | Faster, skips schematics | Misses automated migration schematics (e.g., standalone conversion, deprecated API codemods), leaves silent breakage that only surfaces at runtime | Never for a 13→current jump; only acceptable for patch/minor bumps within a major |
| Treating CORS/rate-limit changes as backend-only and verifying via curl/Postman | Fast to test, no browser needed | Curl bypasses CORS preflight entirely — "passing" tests give false confidence while the real SPA silently breaks | Never as the sole verification; fine as a quick sanity check alongside a real browser pass |
| Leaving `SteamTokenStore` cleanup as "redeem removes it, that's good enough" | No new background task needed | Unbounded memory growth from abandoned OAuth flows (user starts Steam login, never completes it) — slow leak, not urgent | Acceptable only if a periodic cleanup task is explicitly tracked as a near-term follow-up, not silently dropped |
| Skipping tests for background workers because "they're hard to test" | Saves time in a milestone already adding hardening-only scope | Workers are exactly where credential-migration and rate-limiting regressions surface first (per Pitfall 1/2) and are the least observable in production | Acceptable only if manual verification (checking sync timestamps/logs after each hardening change) substitutes as a deliberate, tracked step |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|-----------------|-------------------|
| IGDB (Twitch OAuth2 token endpoint) | Treating query-string-to-header migration as identical to Steam's; using JSON body instead of form-encoded | Use `FormUrlEncodedContent` with `client_id`, `client_secret`, `grant_type` in the POST body — this is Twitch/IGDB's actual documented contract |
| Steam Web API | Assuming the key must move to a body since "that's what secure APIs do" | Steam only supports `key` query param or `x-webapi-key` header — no body option; move to the header, leave other params in the query string |
| Steam OpenID login (`SteamAuthService`) | Hardening JWT/auth broadly and accidentally tightening validation on the OpenID callback trust boundary in the same pass, without isolated testing of the login flow specifically | Treat the Steam OpenID callback validation as its own verification item within the "review JWT/auth flow" work — log in via the real Steam OpenID flow after any change here, since it can't be meaningfully unit-tested without mocking Steam's entire OpenID provider |
| HowLongToBeat (`GameHLTBManager`/sync worker) | Assuming it uses a documented, stable API (HLTB has no official public API; most integrations scrape or use an unofficial endpoint) — hardening changes (rate limiting, header changes) applied uniformly to "all external services" may not even apply the same way here | Verify what `GameHLTBManager` actually calls before applying credential/header changes designed for IGDB/Steam — it may need different handling entirely (e.g., no credentials to move, since scraping-style integrations often don't authenticate the same way) |
| Angular `HttpClient` → hardened CORS backend | Assuming the Angular HTTP interceptor doesn't send any headers beyond `Authorization` | Grep the actual interceptor/service code for every header set on outgoing requests before finalizing the CORS allow-list — a missed header (e.g., a custom trace/version header) causes silent preflight failures |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| In-memory rate limiter counters reset on app restart/redeploy | Rate limits appear inconsistent across deploys; not a concern for this single-instance personal app, but worth noting if deployment model ever changes | Document that the built-in ASP.NET Core rate limiter is process-local and would need a Redis-backed limiter if ever scaled horizontally (already noted in CONCERNS.md scaling section) | If the app is ever deployed across multiple instances/replicas |
| Rate-limiting keyed by `RemoteIpAddress` behind any reverse proxy (even a local dev proxy or future deployment) | All requests appear to come from one IP (the proxy), collapsing every user into a single rate-limit bucket | Key rate-limit partitions by authenticated user ID (JWT claim) for data-modification endpoints, not IP, since this app already has auth on those routes | The moment any reverse proxy, load balancer, or even `ng serve --proxy-config` sits in front of the API during dev/test |
| Fresh `SqlConnection` per Dapper call under load-tested/bulk operations (sync workers processing many games) | Connection pool exhaustion under worker-driven bulk writes specifically, not everyday interactive use | Load-test the fixed `GameManager` methods under worker-like call volume, not just single manual clicks | Once background sync volume grows (more games tracked, more frequent sync) even without user-facing traffic growth |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Fixing credential-in-URL for IGDB/Steam but leaving the JWT secret or Steam OpenID realm/return-URL validation unreviewed in the same milestone | Partial hardening gives false sense of completeness while the JWT/OpenID trust boundary (explicitly called out as "Active" in PROJECT.md) remains unexamined | Treat "review JWT/auth flow end-to-end" as a first-class phase item, not an afterthought bundled into credential migration |
| Restricting CORS to `AllowAnyHeader()`/`AllowAnyMethod()` → specific lists, but leaving `AllowCredentials()` combined with a wildcard origin somewhere (invalid combination that browsers reject, or a leftover permissive fallback for "just in case") | CORS misconfiguration that either silently fails (browser rejects wildcard+credentials) or, worse, is fixed by someone widening it back to `AllowAnyOrigin()` to "make it work again" under time pressure, undoing the hardening | Set the exact allowed origin(s) explicitly (not a wildcard) from the start when `AllowCredentials()` is used, since JWT-bearer + cookie-based Steam OpenID flows likely need credentialed requests |
| Adding rate limiting only to volume/abuse concerns and not to the Steam OpenID callback or JWT refresh/login endpoints specifically | Auth endpoints already have rate limiting per PROJECT.md, but if refresh-token or Steam-callback routes were added since the original `/auth` rate limiting was set up, they might not be covered by the existing policy | Explicitly enumerate every auth-adjacent route (including Steam OpenID callback and any token-refresh endpoint) and confirm each is covered by a rate-limit policy, not just the original `/auth/login` |
| Treating "no production users" (per PROJECT.md constraints) as license to skip verifying the credential-migration and CORS fixes actually work end-to-end | Both IGDB and Steam integrations could be silently broken by the security fix itself and go unnoticed for a long time in a personal-use app with no error monitoring/alerting | Manual smoke test each integration end-to-end after its specific hardening change ships, precisely because there's no user base to surface the breakage via bug reports |

## "Looks Done But Isn't" Checklist

- [ ] **Credential migration (IGDB/Steam):** Often missing a real end-to-end call against the live external API — a code review that "looks correct" (right method, right content-type) can still be wrong about the exact field names/format the external API expects. Verify: trigger an actual sync (manual worker run or admin action) and confirm real data returns, not just "no exception thrown."
- [ ] **CORS hardening:** Often missing verification via the actual browser-served SPA — passing `curl`/Postman tests is not sufficient since they don't enforce preflight. Verify: open the real running Angular app in a browser, open devtools Network tab, and confirm zero CORS errors across login, CRUD, and image-loading flows.
- [ ] **SqlConnection/Dapper fix:** Often missing a load/repetition test — a single successful manual call proves disposal works once, not that pooling behaves correctly under the sync workers' actual call volume. Verify: run the fixed methods 50-100+ times in a row (simulating worker batch behavior) and confirm no connection-pool timeout.
- [ ] **Audit fields/soft-delete migration:** Often missing a full sweep of every write stored procedure (not just the ones in `GameManager`) — sync-worker upsert procs are easy to miss since they're triggered by background jobs, not user action. Verify: grep every `sp_Insert*`/`sp_Update*`/`sp_Upsert*`/`sp_Delete*` touching the affected tables and confirm each populates the new audit columns.
- [ ] **Angular upgrade:** Often missing verification of deprecated-but-still-functional APIs left in place (e.g., `HttpClientModule` still works after `provideHttpClient()` exists, so nothing forces migration) — looks done because the app builds and runs, but carries forward technical debt the upgrade was partly meant to resolve. Verify: check for build warnings about deprecated APIs and confirm they were addressed, not just silenced.
- [ ] **Rate limiting on data-modification endpoints:** Often missing coverage of routes added or renamed after the original `/auth`-only rate limiter was configured — verify by enumerating every POST/PUT/DELETE route in the API and confirming each maps to an active rate-limit policy, not just spot-checking the ones named in PROJECT.md.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|----------------|------------------|
| Steam/IGDB integration silently broken by credential migration | LOW | Revert the specific service's HTTP call to the last-known-working shape, re-verify against the live API with a manual test call, then reapply the fix incrementally (one call site at a time) instead of all at once |
| CORS hardening breaks the SPA in production/local use | LOW | Temporarily widen the allow-list back to match what the browser devtools Network tab shows the SPA actually sending, ship that as a corrected version, then re-tighten with the now-accurate header list |
| Rate limiting throttles background workers | MEDIUM | Identify the worker's calling identity/pattern, add an explicit exemption or higher-limit partition keyed to it, and add the worker-volume regression test called out in Pitfall 2 before re-enabling the general limit |
| Angular upgrade regression discovered after several version jumps already committed | MEDIUM-HIGH | Because each major version was committed separately (per Pitfall 3's guidance), bisect by checking out the last known-good major-version commit and re-applying the upgrade one step at a time with manual UAT after each, rather than debugging the fully-upgraded state blind |
| Audit-field migration backfilled with fabricated timestamps/users | HIGH | No automatic recovery — the true history is unrecoverable. Best available fix is to re-run the migration with `NULL` in place of the fabricated values (if not yet relied upon by application logic) and clearly document in the schema/README that pre-migration rows have no reliable audit history |
| Soft-delete `IsActive`/`DeletedAt` drift (one set without the other) | MEDIUM | Write a one-off SQL script (numbered per project convention) to reconcile: set `DeletedAt = ModifiedAt` (or a documented sentinel) wherever `IsActive = 0 AND DeletedAt IS NULL`, then audit all write sprocs to ensure both fields are always set together going forward |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|-------------------|----------------|
| Credential migration breaks integrations (Pitfall 1) | Credential-migration phase (IGDB and Steam as separate verified steps) | Manual live-API call per service after each change; sync worker produces real data, not silent empty results |
| CORS/rate limiting silent breakage (Pitfall 2) | Separate CORS phase and rate-limiting phase, each ending in full-SPA manual walkthrough | Browser devtools shows zero CORS errors; worker sync timestamps continue advancing after rate limits ship |
| Angular upgrade compounding risk (Pitfall 3) | Dedicated, isolated Angular-upgrade phase | Full manual UAT of every screen after each major-version step; no unexplained regressions attributable to ambiguous cause |
| SqlConnection/Dapper lifecycle fix (Pitfall 4) | Backend hardening phase scoped to `GameManager` connection handling | Repetition/load test (50-100+ calls) shows no connection pool growth or timeout |
| Audit fields/soft-delete on existing data (Pitfall 5) | Dedicated audit-fields phase, sequenced after the SqlConnection fix, before/alongside rate limiting | Every write sproc touching affected tables confirmed to populate new columns; pre-existing rows handled per an explicit NULL-vs-sentinel decision documented in the migration script |
| Sequencing risk between Angular upgrade and backend hardening (Pitfall 6) | Roadmap-level ordering: Angular upgrade phase first, then backend hardening phases, CORS last, final combined full-stack UAT step | Final full-stack UAT pass after both workstreams complete re-verifies the CORS/header contract against the upgraded app's actual traffic |

## Sources

- `.planning/codebase/CONCERNS.md` (2026-08-28 codebase audit) — primary source for file-specific findings (credential URLs, CORS, SqlConnection disposal, SteamTokenStore growth, missing audit trail, EOL dependencies)
- `.planning/codebase/TESTING.md` (2026-08-28) — confirms zero existing test coverage across backend and frontend, informing the "no automated safety net" framing throughout
- Steam Web API documentation (steamapi.xpaw.me; partner.steamgames.com) — confirms key can be passed via `key` query param or `x-webapi-key` header; no body-based option
- Angular version history and upgrade guidance (HeroDevs Angular version history; general Angular `ng update` documentation) — confirms sequential major-version upgrade requirement and current LTS cadence as of mid-2026; exact target version should be re-confirmed at execution time given Angular's ~6-month major release cadence
- ASP.NET Core rate limiting documentation and community guidance (Microsoft Learn; general ASP.NET Core rate-limiting writeups) — confirms in-memory, per-instance counter behavior and reverse-proxy partitioning pitfalls (RemoteIpAddress collapsing behind a proxy)
- General OAuth2 client-credentials grant conventions (Twitch/IGDB developer docs pattern) — confirms form-encoded POST body is the standard transport for `client_id`/`client_secret`/`grant_type`

---
*Pitfalls research for: Retrofit security hardening on brownfield ASP.NET Core + Angular app*
*Researched: 2026-08-28*
