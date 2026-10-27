# Phase 2: Credential & Secrets Hardening - Context

**Gathered:** 2026-08-30
**Status:** Ready for planning

<domain>
## Phase Boundary

External API credentials (IGDB, Steam) stop being exposed via URLs, logs, or committed config. Previously-leaked credentials get rotated. External-API failures in the Manager/Service layer are handled explicitly (logged with proper severity, surfaced via the existing WorkerStatus mechanism) instead of silently swallowed. Confined to `IgdbService.cs`, `SteamService.cs`, config files (`appsettings.Development.json`, `appsettings.json`), a new pre-commit secret-scanning hook, and the Manager/Service-level `catch (Exception)` sites wrapping external API calls (`GameImageManager`, `GameEnrichmentManager`, `GameSyncManager`, `SteamPlaytimeSyncManager`, `CsvGameService`). Requirements: CRED-01 through CRED-05, REL-03.

Two pending todos (session-planner cross-user data leak, IGDB upsert dedup bug) were reviewed against this phase and explicitly kept out — neither is a credential/secrets issue. See Deferred Ideas.

</domain>

<decisions>
## Implementation Decisions

### CRED-01/CRED-02 — Credential transport
- **D-01:** IGDB token request (`IgdbService.cs:30`) moves from URL query string to a form-encoded POST body (`FormUrlEncodedContent`). Straightforward — ROADMAP/REQUIREMENTS already specify this exactly.
- **D-02:** Steam API key stays in the URL query string (Steam's Web API has no body-based auth option — REQUIREMENTS.md explicitly accepts this). CRED-02 is closed by adding explicit `try/catch` around the three Steam HTTP calls in `SteamService.cs` (`ResolveSteamIdAsync`, `GetPlayerSummaryAsync`, `FetchOwnedGamesAsync`) that logs a redacted failure message (status code + context, never the `ApiKey` or full URL) instead of letting an unhandled exception potentially leak the URL through a generic exception logger. — **Reversibility:** reversible.

### CRED-03/CRED-04 — Secret storage split & rotation
- **D-03:** Values moving out of `appsettings.Development.json` into `dotnet user-secrets`: `IGDB:ClientId`, `IGDB:ClientSecret`, `Steam:ApiKey`, `Jwt:Secret`. `ConnectionStrings:DefaultConnection` stays (uses `Trusted_Connection`, no embedded password) and `HLTB:CsvPath` stays (local file path, not a secret).
- **D-04:** Non-dev ("closer-to-deployed") environments read the same four values from OS environment variables using the ASP.NET Core double-underscore convention (`IGDB__ClientId`, `IGDB__ClientSecret`, `Steam__ApiKey`, `Jwt__Secret`) — picked up automatically by the existing `IConfiguration` setup, no code changes needed beyond removing the values from committed JSON.
- **D-05:** `GamePlanner.API.csproj` currently has no `<UserSecretsId>` — planning/execution needs to run `dotnet user-secrets init` first.
- **D-06:** Rotation (CRED-04) sequencing: Claude sets up the user-secrets/env-var plumbing first with empty placeholders. The user then rotates the IGDB client secret (via Twitch dev console) and Steam API key (via Steam's API key page) themselves — this requires their own account login, which Claude cannot do — and hands the new values back (or pastes them directly into `dotnet user-secrets set` themselves) to finish wiring. — **Reversibility:** one-way for the actual rotation step (old keys become permanently invalid once regenerated) — rationale: this is an external-provider action outside version control; note it as a manual checkpoint in the plan, not something to script.
- **D-07:** Git history is NOT scrubbed of the previously-committed real secrets. Rotation alone is sufficient — once rotated, the old committed values can't authenticate against IGDB/Steam anymore, so their presence in history is harmless. Rewriting history (git filter-repo/BFG) was explicitly rejected as unnecessary disruption for a solo repo.
- **D-08:** JWT secret also rotates as part of this phase. Rotating it invalidates every currently-issued token (all sessions logged out) — user explicitly said no concern, rotate whenever, no special sequencing needed.
- **D-09:** Once `appsettings.Development.json` is gitignored, commit a companion `appsettings.Development.json.example` with the same keys but empty/placeholder values, so local dev setup stays discoverable without exposing real secrets.

### CRED-05 — Secret-scanning pre-commit hook
- **D-10:** Tool: **gitleaks** (not trufflehog) — single static binary, native Windows support, no Python/Go toolchain needed, fast default ruleset that catches API-key/JWT-secret patterns like the ones in this repo.
- **D-11:** Hook mechanism: a **plain native git hook** (`.git/hooks/pre-commit`) that shells out to the gitleaks binary against staged changes — no husky/pre-commit framework introduced, since the repo doesn't have one and this is a backend-focused concern (the frontend's `package.json` was considered and rejected as the wrong home for it).
- **D-12:** Since native git hooks aren't copied on clone, add a small setup script (e.g. `scripts/install-git-hooks.ps1` or `.sh`) that copies the hook into `.git/hooks/` and makes it executable, plus a one-time setup note in `CLAUDE.md`. (`core.hooksPath` was considered and rejected in favor of the more explicit script + doc approach.)
- **D-13:** If gitleaks isn't installed/found on PATH when the hook runs, **warn and let the commit through** — this is a personal/local single-developer machine; a missing binary should never hard-block the user's own commits. Print a clear "install gitleaks" message.

### REL-03 — Broad catch(Exception) around external API calls
- **D-14:** In scope: Manager/Service-level `catch (Exception)` blocks that wrap individual external API calls — `GameImageManager.cs` (4 sites, lines 69, 90, 177, 186), `GameEnrichmentManager.cs` (line 77), `GameSyncManager.cs` (line 152), `SteamPlaytimeSyncManager.cs` (line 72), `CsvGameService.cs` (2 sites, lines 77, 84). Out of scope: the 6 Worker-level (`BackgroundService`) top-loop `catch (Exception)` blocks — these are a legitimate, different pattern (prevent one bad sync cycle from crashing the whole background service) and were explicitly excluded, not overlooked.
- **D-15:** Fix depth: **keep `catch (Exception)`**, don't narrow to specific exception types (`HttpRequestException`, `TaskCanceledException`, `JsonException`, etc.) — narrowing was considered and rejected as reshaping more code than needed and risking missed exception types from library internals. Instead: raise log severity where appropriate (`LogWarning` → `LogError` for failures that represent real data loss, not transient blips), and ensure the failure doesn't get silently continued past — the caller/worker needs to know some items failed. — **Reversibility:** reversible.
- **D-16:** "Surfaced rather than silently dropped" means extending the **existing `WorkerStatus` table** (already used for background-worker monitoring per `ARCHITECTURE.md`) to record partial-failure info (failed-item counts/IDs) on a sync run. This reuses the existing `/api/worker/status` endpoint for visibility — no new UI, no new table. Log-only was considered and rejected as insufficient given the ROADMAP's explicit "surfaced" wording.

### Claude's Discretion
- Exact wording/format of the redacted log messages in `SteamService.cs` (D-02) and the raised-severity log messages in the REL-03 Manager/Service sites (D-15) — follow this project's existing structured-logging conventions (named parameters, `ILogger<T>`).
- Exact shape of the `WorkerStatus` schema extension for partial-failure info (D-16) — Claude designs the column(s)/fields needed, following the numbered-SQL-migration convention if a schema change is required.
- Exact content/naming of the git-hook install script (D-12) — PowerShell vs shell, given this is a Windows dev machine with Git Bash available.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & requirements
- `.planning/ROADMAP.md` — Phase 2 section: goal, success criteria, CRED-01..05/REL-03 requirements
- `.planning/REQUIREMENTS.md` — CRED-01 through CRED-05, REL-03 definitions and explicit Steam URL-auth exception (CRED-02)

### Codebase state
- `.planning/codebase/CONCERNS.md` §"IGDB API Credentials in URL Query Strings", §"Steam API Key in Request URLs", §"Broad Exception Handling with Silent Failures" — original findings this phase fixes
- `.planning/codebase/INTEGRATIONS.md` §"APIs & External Services", §"Environment Configuration" — current IGDB/Steam credential config keys and where they're read
- `GamePlanner.API/Services/IgdbService.cs` — token-fetch URL construction (line 30) and existing token-caching pattern to preserve
- `GamePlanner.API/Services/SteamService.cs` — three URL-embedded-key call sites (lines 41, 59, 76)
- `GamePlanner.API/appsettings.Development.json` — currently contains the real, committed IGDB/Steam/JWT secrets to be rotated and removed
- `GamePlanner.API/appsettings.json` — production template already has empty placeholders for IGDB/Steam/Jwt (confirmed correct, no change needed there)
- `GamePlanner.API/GamePlanner.API.csproj` — confirmed no `<UserSecretsId>` present yet; `dotnet user-secrets init` required

### Project state
- `.planning/STATE.md` — records this phase's ordering rationale (independent of Phase 1, most self-contained backend change)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WorkerStatus` table + `WorkerStatusManager` — already tracks background-worker run state; D-16 extends this rather than building new failure-visibility infrastructure.
- Existing structured-logging pattern (`ILogger<T>` injection, named parameters like `{Status}`, `{Body}`) already used in `IgdbService.PostQueryAsync` — follow this for the new/changed log statements in REL-03 and CRED-02 fixes.

### Established Patterns
- `appsettings.json` (production template) already correctly has empty placeholder values for all secrets — only `appsettings.Development.json` has the actual leaked values, confirmed by direct read.
- No existing git hook or Node-based hook framework (no husky, no `.git/hooks/pre-commit`) — D-11/D-12 introduce this from scratch.
- 15 total `catch (Exception)` sites exist codebase-wide; D-14 explicitly scopes REL-03 to 8 of them (Manager/Service tier), leaving the 6 Worker-level ones and 1 unrelated Manager site untouched.

### Integration Points
- IGDB/Steam credential changes are confined to their respective service classes — no controller or frontend changes needed.
- The `WorkerStatus` schema extension (D-16) touches whichever workers call the in-scope Manager/Service methods (`GameSyncWorker`, `GameImageSyncWorker`, `SteamPlaytimeSyncWorker`, `GameHLTBSyncWorker` via `CsvGameService`) — planning should enumerate exact call sites before implementation.

</code_context>

<specifics>
## Specific Ideas

No specific reference implementations mentioned beyond the tool/mechanism choices captured as decisions above (gitleaks, native git hook, user-secrets + env-var split). The user's guidance was entirely about scope boundaries and rejecting heavier alternatives (trufflehog, husky, history-scrubbing, exception-type narrowing) in favor of the simplest mechanism that satisfies each requirement, appropriate for a personal/single-developer project.

</specifics>

<deferred>
## Deferred Ideas

### Reviewed Todos (not folded)
- **Session planner leaks cross-user game data** (`SessionManager.cs:15`, major severity) — reviewed and explicitly NOT folded into Phase 2. This is a missing `.Where(g => g.UserId == userId)` filter — an access-control/data-isolation bug, not a credential/secrets issue. Remains a pending todo (`.planning/todos/pending/2026-08-29-session-planner-leaks-cross-user-game-data.md`); candidate for its own fix or folding into a future phase.
- **IGDBId upsert silently deactivates manually-added games** (`Database/33_...sql`, `34_...sql`, `GameManager.cs:170`, major severity) — reviewed and explicitly NOT folded into Phase 2, despite already having an in-progress fix in the working tree (uncommitted `GameManager.cs`/`GameImageManager.cs` changes, staged `Database/33_RemoveUniqueIndex_IGDBId.sql` and `34_FixStoredProcedure_UpsertIGDBGame.sql`). This is a data-integrity/upsert-dedup bug, not a credential/secrets issue. Remains a pending todo (`.planning/todos/pending/2026-08-29-igdbid-upsert-deactivates-games-across-users.md`); natural fit for Phase 4 (Data Layer Reliability & Audit Trail) or an earlier standalone fix given the drafted scripts.

</deferred>

---

*Phase: 2-Credential & Secrets Hardening*
*Context gathered: 2026-08-30*
