# Phase 2: Credential & Secrets Hardening - Research

**Researched:** 2026-08-30
**Domain:** ASP.NET Core credential handling, .NET HttpClient auth transport, dotnet user-secrets, git secret-scanning hooks (gitleaks), exception-handling hygiene in background sync managers
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**CRED-01/CRED-02 — Credential transport**
- **D-01:** IGDB token request (`IgdbService.cs:30`) moves from URL query string to a form-encoded POST body (`FormUrlEncodedContent`). Straightforward — ROADMAP/REQUIREMENTS already specify this exactly.
- **D-02:** Steam API key stays in the URL query string (Steam's Web API has no body-based auth option — REQUIREMENTS.md explicitly accepts this). CRED-02 is closed by adding explicit `try/catch` around the three Steam HTTP calls in `SteamService.cs` (`ResolveSteamIdAsync`, `GetPlayerSummaryAsync`, `FetchOwnedGamesAsync`) that logs a redacted failure message (status code + context, never the `ApiKey` or full URL) instead of letting an unhandled exception potentially leak the URL through a generic exception logger. — **Reversibility:** reversible.

**CRED-03/CRED-04 — Secret storage split & rotation**
- **D-03:** Values moving out of `appsettings.Development.json` into `dotnet user-secrets`: `IGDB:ClientId`, `IGDB:ClientSecret`, `Steam:ApiKey`, `Jwt:Secret`. `ConnectionStrings:DefaultConnection` stays (uses `Trusted_Connection`, no embedded password) and `HLTB:CsvPath` stays (local file path, not a secret).
- **D-04:** Non-dev ("closer-to-deployed") environments read the same four values from OS environment variables using the ASP.NET Core double-underscore convention (`IGDB__ClientId`, `IGDB__ClientSecret`, `Steam__ApiKey`, `Jwt__Secret`) — picked up automatically by the existing `IConfiguration` setup, no code changes needed beyond removing the values from committed JSON.
- **D-05:** `GamePlanner.API.csproj` currently has no `<UserSecretsId>` — planning/execution needs to run `dotnet user-secrets init` first.
- **D-06:** Rotation (CRED-04) sequencing: Claude sets up the user-secrets/env-var plumbing first with empty placeholders. The user then rotates the IGDB client secret (via Twitch dev console) and Steam API key (via Steam's API key page) themselves — this requires their own account login, which Claude cannot do — and hands the new values back (or pastes them directly into `dotnet user-secrets set` themselves) to finish wiring. — **Reversibility:** one-way for the actual rotation step (old keys become permanently invalid once regenerated) — rationale: this is an external-provider action outside version control; note it as a manual checkpoint in the plan, not something to script.
- **D-07:** Git history is NOT scrubbed of the previously-committed real secrets. Rotation alone is sufficient — once rotated, the old committed values can't authenticate against IGDB/Steam anymore, so their presence in history is harmless. Rewriting history (git filter-repo/BFG) was explicitly rejected as unnecessary disruption for a solo repo.
- **D-08:** JWT secret also rotates as part of this phase. Rotating it invalidates every currently-issued token (all sessions logged out) — user explicitly said no concern, rotate whenever, no special sequencing needed.
- **D-09:** Once `appsettings.Development.json` is gitignored, commit a companion `appsettings.Development.json.example` with the same keys but empty/placeholder values, so local dev setup stays discoverable without exposing real secrets.

**CRED-05 — Secret-scanning pre-commit hook**
- **D-10:** Tool: **gitleaks** (not trufflehog) — single static binary, native Windows support, no Python/Go toolchain needed, fast default ruleset that catches API-key/JWT-secret patterns like the ones in this repo.
- **D-11:** Hook mechanism: a **plain native git hook** (`.git/hooks/pre-commit`) that shells out to the gitleaks binary against staged changes — no husky/pre-commit framework introduced, since the repo doesn't have one and this is a backend-focused concern (the frontend's `package.json` was considered and rejected as the wrong home for it).
- **D-12:** Since native git hooks aren't copied on clone, add a small setup script (e.g. `scripts/install-git-hooks.ps1` or `.sh`) that copies the hook into `.git/hooks/` and makes it executable, plus a one-time setup note in `CLAUDE.md`. (`core.hooksPath` was considered and rejected in favor of the more explicit script + doc approach.)
- **D-13:** If gitleaks isn't installed/found on PATH when the hook runs, **warn and let the commit through** — this is a personal/local single-developer machine; a missing binary should never hard-block the user's own commits. Print a clear "install gitleaks" message.

**REL-03 — Broad catch(Exception) around external API calls**
- **D-14:** In scope: Manager/Service-level `catch (Exception)` blocks that wrap individual external API calls — `GameImageManager.cs` (4 sites, lines 69, 90, 177, 186), `GameEnrichmentManager.cs` (line 77), `GameSyncManager.cs` (line 152), `SteamPlaytimeSyncManager.cs` (line 72), `CsvGameService.cs` (2 sites, lines 77, 84). Out of scope: the 6 Worker-level (`BackgroundService`) top-loop `catch (Exception)` blocks — these are a legitimate, different pattern (prevent one bad sync cycle from crashing the whole background service) and were explicitly excluded, not overlooked.
- **D-15:** Fix depth: **keep `catch (Exception)`**, don't narrow to specific exception types (`HttpRequestException`, `TaskCanceledException`, `JsonException`, etc.) — narrowing was considered and rejected as reshaping more code than needed and risking missed exception types from library internals. Instead: raise log severity where appropriate (`LogWarning` → `LogError` for failures that represent real data loss, not transient blips), and ensure the failure doesn't get silently continued past — the caller/worker needs to know some items failed. — **Reversibility:** reversible.
- **D-16:** "Surfaced rather than silently dropped" means extending the **existing `WorkerStatus` table** (already used for background-worker monitoring per `ARCHITECTURE.md`) to record partial-failure info (failed-item counts/IDs) on a sync run. This reuses the existing `/api/worker/status` endpoint for visibility — no new UI, no new table. Log-only was considered and rejected as insufficient given the ROADMAP's explicit "surfaced" wording.

### Claude's Discretion
- Exact wording/format of the redacted log messages in `SteamService.cs` (D-02) and the raised-severity log messages in the REL-03 Manager/Service sites (D-15) — follow this project's existing structured-logging conventions (named parameters, `ILogger<T>`).
- Exact shape of the `WorkerStatus` schema extension for partial-failure info (D-16) — Claude designs the column(s)/fields needed, following the numbered-SQL-migration convention if a schema change is required.
- Exact content/naming of the git-hook install script (D-12) — PowerShell vs shell, given this is a Windows dev machine with Git Bash available.

### Deferred Ideas (OUT OF SCOPE)

**Reviewed Todos (not folded)**
- **Session planner leaks cross-user game data** (`SessionManager.cs:15`, major severity) — reviewed and explicitly NOT folded into Phase 2. This is a missing `.Where(g => g.UserId == userId)` filter — an access-control/data-isolation bug, not a credential/secrets issue. Remains a pending todo (`.planning/todos/pending/2026-08-29-session-planner-leaks-cross-user-game-data.md`); candidate for its own fix or folding into a future phase.
- **IGDBId upsert silently deactivates manually-added games** (`Database/33_...sql`, `34_...sql`, `GameManager.cs:170`, major severity) — reviewed and explicitly NOT folded into Phase 2, despite already having an in-progress fix in the working tree (uncommitted `GameManager.cs`/`GameImageManager.cs` changes, staged `Database/33_RemoveUniqueIndex_IGDBId.sql` and `34_FixStoredProcedure_UpsertIGDBGame.sql`). This is a data-integrity/upsert-dedup bug, not a credential/secrets issue. Remains a pending todo (`.planning/todos/pending/2026-08-29-igdbid-upsert-deactivates-games-across-users.md`); natural fit for Phase 4 (Data Layer Reliability & Audit Trail) or an earlier standalone fix given the drafted scripts.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-------------------|
| CRED-01 | IGDB token request uses a form-encoded POST body instead of a URL query string | Pattern 1 (Form-encoded OAuth2 token request) gives the exact `FormUrlEncodedContent` + `Dictionary<string,string>` C# syntax to replace `IgdbService.cs:30`'s query-string construction. |
| CRED-02 | Steam API key no longer appears in logs or unhandled exception output (query param usage stays) | Pattern 2 (Redacted logging around Steam URL-embedded key) plus Open Question #2 flags the one ambiguity (catch-type scope) the planner must resolve before task-writing. |
| CRED-03 | Secrets removed from `appsettings.Development.json`, moved to `dotnet user-secrets` (dev) and environment variables (closer-to-deployed) | Pattern 3 gives the exact `dotnet user-secrets init`/`set` CLI workflow and verifies ASP.NET Core's configuration precedence naturally supports the dev/non-dev split with no extra code. Runtime State Inventory documents the exact secret values/locations found. |
| CRED-04 | The IGDB/Steam credentials already exposed in committed config are rotated | Runtime State Inventory confirms rotation is the only viable remediation (no stored-data/OS-state copies to also update) and reaffirms D-06's manual-checkpoint sequencing — no additional research needed beyond what CONTEXT.md already locked. |
| CRED-05 | Secret-scanning pre-commit hook (gitleaks or trufflehog) installed to prevent future leaks | Pattern 4 gives current (2026) gitleaks install commands and hook script syntax, including the `protect --staged` deprecation gotcha (Pitfall 1) and the confirmed-missing-binary Environment Availability finding that makes D-13's soft-fail path load-bearing from day one. |
| REL-03 | Broad silent `catch (Exception)` around external API calls replaced with explicit handling (no silent data loss) | Architecture Pattern diagram + Code Examples show the `WorkerStatusManager.SetCompletedAsync` extension and matching numbered-SQL migration for D-16; Common Pitfall #4 flags a status-field collision risk to avoid during implementation. All 8 D-14 call sites were re-verified live against current line numbers via grep. |
</phase_requirements>

## Summary

This phase is a narrow, well-scoped hardening pass entirely within `GamePlanner.API`. CONTEXT.md already locks 16 concrete decisions (D-01 through D-16) with exact file/line targets, exact env-var names, and exact tool choices — this research does **not** re-litigate any of them. Its job is to fill the implementation-syntax gaps the decisions leave open: the exact C# for switching the IGDB token call from URL-embedded credentials to a form-encoded POST body, the exact `dotnet user-secrets` CLI workflow and how it interacts with ASP.NET Core's configuration precedence, the current (2026) gitleaks install/hook syntax on Windows including a real deprecation gotcha, and the established numbered-SQL-script pattern for extending `WorkerStatus` with partial-failure columns.

Two findings materially affect planning. First, `gitleaks protect --staged` — the command CONTEXT.md's plain-language decisions implicitly assume — was deprecated in gitleaks v8.19.0; it still works but current-syntax guidance is `git diff --cached | gitleaks stdin`. The plan should use the modern form (or explicitly choose the deprecated-but-functional one) rather than assume old blog-post syntax works forever. Second, `gitleaks` is confirmed **not installed** on this machine right now — D-13's soft-fail (warn + allow commit when the binary is missing) is not a hypothetical edge case, it is the actual state execution will start from, so the hook script's "binary not found" branch must be exercised and verified, not just written.

**Primary recommendation:** Implement CRED-01 with `FormUrlEncodedContent` over a `Dictionary<string,string>`, wire `dotnet user-secrets` exactly as Microsoft's tooling expects (colon-notation nested keys, auto-loaded only in Development), install gitleaks via `winget install gitleaks` and hook it with the current `git diff --cached | gitleaks stdin` syntax with a PATH-existence guard for the soft-fail case, and extend `WorkerStatus` with a nullable `FailedItemCount`/`LastPartialFailureDetail` pair following the exact `IF NOT EXISTS` column-add pattern already used in `Database/24_AlterGames_AddRatingFields.sql`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| IGDB token transport (CRED-01) | API / Backend (Service) | — | `IgdbService` is a singleton HTTP client wrapper; token acquisition is pure backend-to-external-API concern |
| Steam error redaction (CRED-02) | API / Backend (Service) | — | `SteamService` HTTP call sites; no client-facing surface |
| Secret storage split (CRED-03/04) | API / Backend (Configuration) | Database (`ConnectionStrings` stays as-is) | `IConfiguration` composition root in `Program.cs`; per-environment secret source is a backend concern only |
| Secret-scanning hook (CRED-05) | Dev tooling / Git | — | Outside the app's runtime tiers entirely — a repo-level pre-commit gate |
| External-API failure surfacing (REL-03) | API / Backend (Manager) | Database (`WorkerStatus` table) | Managers own the try/catch; `WorkerStatus` is the existing persistence tier for run-state visibility |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Net.Http.FormUrlEncodedContent` | Built into .NET 8 BCL (no package) | Form-encode OAuth2 client_credentials body | This is the canonical .NET type for `application/x-www-form-urlencoded` POST bodies — no third-party OAuth client needed for a single token endpoint call |
| `Microsoft.Extensions.Configuration.UserSecrets` | Implicit via `dotnet user-secrets` tooling (SDK-bundled, no explicit PackageReference needed for `WebApplication.CreateBuilder`) | Dev-only secret storage outside source control | Built into the ASP.NET Core hosting model; `WebApplication.CreateBuilder` auto-registers it when `IHostEnvironment.EnvironmentName == "Development"` [CITED: learn.microsoft.com/aspnet/core/security/app-secrets] |
| gitleaks | ~8.30.1 (current, confirmed via winget listing) [CITED: winstall.app/apps/Gitleaks.Gitleaks] | Secret-scanning pre-commit gate | Single static binary, no runtime dependency (Go binary) — matches D-10's "no Python/Go toolchain needed" rationale exactly |

**Version verification:** `FormUrlEncodedContent`/`UserSecrets` are BCL/SDK components, not NuGet packages — no `npm view`/`pip index` equivalent applies; version is tied to the installed .NET 8 SDK already in use (confirmed `net8.0` target in `GamePlanner.API.csproj`). gitleaks version verified via WebSearch against a package-listing site (winstall.app), not the ecosystem's own registry — no `npm view`/`cargo search` equivalent exists for a standalone Go binary; tag `[CITED]` not `[VERIFIED]`.

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| none | — | — | This phase installs no new NuGet or npm packages — see Package Legitimacy Audit below |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `FormUrlEncodedContent` | Manual `StringContent` with `"application/x-www-form-urlencoded"` media type and hand-built query string | `FormUrlEncodedContent` correctly URL-encodes each key/value pair; hand-building risks re-introducing the exact bug this phase is fixing (secrets embedded in a raw string) |
| gitleaks | trufflehog | Explicitly rejected in CONTEXT.md D-10 — trufflehog needs Python/Go toolchain, gitleaks is a single binary |
| `dotnet user-secrets` | `.env` file + `DotNetEnv` package | Rejected implicitly by D-03 — user-secrets is the ASP.NET Core-native mechanism already gitignored by convention (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`, outside the repo entirely, not just gitignored) |

**Installation:**
```bash
# No NuGet package install required for CRED-01/03/04 (BCL + SDK tooling only).
# One-time per-project setup (D-05):
dotnet user-secrets init --project GamePlanner.API/GamePlanner.API.csproj

# gitleaks (Windows, D-10):
winget install --id Gitleaks.Gitleaks --exact
# or: scoop install gitleaks
```

**Version verification:** No `npm view`/`pip index`/`cargo search` equivalent applies to this phase — no packages are added to `GamePlanner.API.csproj` or `package.json`. gitleaks is a standalone binary verified via WebSearch (see Package Legitimacy Audit).

## Package Legitimacy Audit

**No new NuGet or npm packages are installed in this phase.** CRED-01, CRED-03, and CRED-04 use existing .NET 8 BCL types (`FormUrlEncodedContent`) and SDK-bundled tooling (`dotnet user-secrets`) already implicit in the `Microsoft.NET.Sdk.Web` project type — no `<PackageReference>` addition needed. CRED-05 installs **gitleaks**, a standalone Go binary distributed via winget/scoop/direct GitHub release, not a package-manager dependency of the application itself — it never appears in `GamePlanner.API.csproj` or any lockfile, so the npm/NuGet legitimacy-check tooling (`package-legitimacy check --ecosystem npm|pypi|crates`) does not apply to it. Its provenance was instead verified via WebSearch against the official `gitleaks/gitleaks` GitHub repository and a package-listing mirror (winstall.app) — both confirm it as the actively maintained, canonical tool referenced by D-10. No `[SLOP]` or `[SUS]` disposition applies since there is no registry entry to falsify; risk is bounded to "install the wrong binary from an untrusted mirror," mitigated by using `winget`/`scoop`'s official package definitions rather than a raw downloaded `.exe`.

**Packages removed due to [SLOP] verdict:** none (no packages evaluated — none installed)
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│  Developer machine (git commit)                                     │
│                                                                       │
│   git add <files>                                                    │
│        │                                                              │
│        ▼                                                              │
│   .git/hooks/pre-commit  ──(binary found?)──► gitleaks git diff      │
│        │                        │                --cached | stdin     │
│        │                        │                        │            │
│        │                   NO (warn, exit 0)        findings? ───┐    │
│        │                        │                        │      │    │
│        │                        ▼                   YES (exit≠0)│NO  │
│        │                  commit proceeds                │      │    │
│        │                        ▼                        ▼      ▼    │
│        │                   commit blocked          commit proceeds   │
└────────┼──────────────────────────────────────────────────────────────┘
         │  (commit succeeds — code reaches runtime)
         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  GamePlanner.API (ASP.NET Core, runtime)                             │
│                                                                        │
│  Configuration composition (Program.cs, WebApplication.CreateBuilder) │
│     appsettings.json → appsettings.Development.json (no secrets)      │
│        → User Secrets (Development only) → Env vars (IGDB__*, etc.)   │
│                            │                                           │
│                            ▼                                           │
│  IgdbService.GetTokenAsync()                                          │
│     builds Dictionary{client_id, client_secret, grant_type}           │
│     → FormUrlEncodedContent → POST body (not URL) → id.twitch.tv      │
│                            │                                           │
│  SteamService.*Async()                                                │
│     URL still carries ?key=... (Steam has no body-auth option)        │
│     wrapped in try/catch → redacted LogWarning (no key/URL in log)    │
│                                                                        │
│  Manager sync loops (GameImageManager, GameEnrichmentManager, etc.)   │
│     per-item catch(Exception) → LogWarning/LogError (severity raised) │
│     → failure count accumulated → returned to calling Worker           │
│                            │                                           │
│                            ▼                                           │
│  WorkerStatusManager.SetCompletedAsync(name, failedCount, detail)     │
│     → WorkerStatus row (FailedItemCount, LastPartialFailureDetail)    │
│                            │                                           │
│                            ▼                                           │
│  GET /api/worker/status  (existing endpoint, now surfaces partial     │
│                            failures — no new UI/table)                 │
└─────────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure
No new directories — all changes are edits to existing files:
```
GamePlanner.API/
├── Services/
│   ├── IgdbService.cs          # D-01: token request body change
│   └── SteamService.cs         # D-02: redacted try/catch around 3 call sites
├── Managers/
│   ├── GameImageManager.cs         # D-14/15: 4 catch sites, severity + failure tracking
│   ├── GameEnrichmentManager.cs    # D-14/15: 1 catch site
│   ├── GameSyncManager.cs          # D-14/15: 1 catch site
│   ├── SteamPlaytimeSyncManager.cs # D-14/15: 1 catch site
│   └── WorkerStatusManager.cs      # D-16: extended Set*Async signatures
├── Services/CsvGameService.cs      # D-14/15: 2 catch sites
├── Entities/WorkerStatus.cs        # D-16: new nullable columns
├── appsettings.Development.json    # D-03: secrets removed
├── appsettings.Development.json.example  # D-09: new, placeholder values
├── GamePlanner.API.csproj          # D-05: <UserSecretsId> added
└── Workers/*.cs                    # call sites updated to pass failure info to WorkerStatusManager

Database/
└── 35_AlterWorkerStatus_AddFailureTracking.sql  # D-16: next numbered script

.git/hooks/
└── pre-commit                      # D-11: new native hook (not tracked by git itself)

scripts/
└── install-git-hooks.ps1 (or .sh)  # D-12: copies hook into .git/hooks/
```

### Pattern 1: Form-encoded OAuth2 token request (CRED-01)
**What:** Replace URL query-string credential embedding with `FormUrlEncodedContent` as the HTTP request body.
**When to use:** Any OAuth2 `client_credentials` grant call — this is the standard OAuth2 RFC 6749 §4.4.2 transport (`application/x-www-form-urlencoded` body), which Twitch's IGDB token endpoint (`id.twitch.tv/oauth2/token`) expects.
**Example:**
```csharp
// Source: WebSearch-verified pattern [CITED: multiple .NET OAuth2 client_credentials examples]
private async Task<string> GetTokenAsync()
{
    if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
        return _cachedToken;

    var client = httpFactory.CreateClient();

    var form = new Dictionary<string, string>
    {
        ["client_id"]     = ClientId,
        ["client_secret"] = ClientSecret,
        ["grant_type"]    = "client_credentials",
    };

    var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(form));
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var token = JsonSerializer.Deserialize<IgdbTokenResponse>(json)!;

    _cachedToken = token.AccessToken;
    _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60);

    logger.LogInformation("IGDB token acquired, expires in {Seconds}s", token.ExpiresIn);
    return _cachedToken;
}
```
Verification for the phase's success criterion ("no credential appears in the outgoing request URL") — the `TokenUrl` constant itself must no longer be built with `$"{TokenUrl}?client_id=...`; it should be passed to `PostAsync` unmodified (bare `https://id.twitch.tv/oauth2/token`).

### Pattern 2: Redacted logging around Steam URL-embedded key (CRED-02)
**What:** Wrap each Steam HTTP call in try/catch, log status/context only, never the URL or the `ApiKey`.
**When to use:** Any call site where the auth secret is unavoidably in the URL (no body-based auth alternative) and a generic/unhandled-exception logger could otherwise print the full request URI (e.g., via `HttpRequestException.Message`, which on some BCL versions includes the request URI).
**Example:**
```csharp
// Pattern follows existing IgdbService.PostQueryAsync structured-logging convention
private async Task<string> ResolveSteamIdAsync(string input, CancellationToken ct)
{
    // ... vanity parsing unchanged ...
    var client = httpFactory.CreateClient();
    var url = $"{BaseUrl}/ISteamUser/ResolveVanityURL/v1/?key={ApiKey}&vanityurl={vanity}";

    try
    {
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<SteamVanityResponse>(json);

        if (result?.Response?.Success != 1 || result.Response.SteamId is null)
            throw new InvalidOperationException($"Could not resolve Steam vanity URL: \"{vanity}\". Make sure the profile is public.");

        return result.Response.SteamId;
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Steam vanity resolution failed for '{Vanity}' with status {Status}.", vanity, ex.StatusCode);
        throw;
    }
}
```
Note: D-15 says keep `catch (Exception)` for the REL-03 Manager/Service sites, but CRED-02's redaction wrapper is a *different, narrower* concern than REL-03 — it exists specifically to prevent the URL/key from leaking through exception message text, so catching `HttpRequestException` specifically (which is what `EnsureSuccessStatusCode`/`SendAsync` throw) is appropriate here without contradicting D-15's Manager-tier guidance. Confirm this reasoning with the planner since CONTEXT.md doesn't explicitly resolve the tension between "CRED-02 needs try/catch" and "D-15 says don't narrow exception types" — CRED-02's three call sites are Service-tier, not the Manager-tier sites D-14/D-15 scope to, so no actual conflict exists, but the plan should state this explicitly to avoid an implementer assuming CRED-02 must also use bare `catch (Exception)`.

### Pattern 3: dotnet user-secrets setup and precedence (CRED-03/CRED-04)
**What:** Move dev secrets out of `appsettings.Development.json` into the per-developer user-secrets store; non-dev environments read the same config keys from env vars.
**When to use:** Standard ASP.NET Core local-dev secret pattern.
**Example:**
```bash
# One-time, from the GamePlanner.API/ directory (D-05):
dotnet user-secrets init

# Set each secret (colon-notation nested keys) [CITED: learn.microsoft.com/aspnet/core/security/app-secrets]:
dotnet user-secrets set "IGDB:ClientId" "<value>"
dotnet user-secrets set "IGDB:ClientSecret" "<value>"
dotnet user-secrets set "Steam:ApiKey" "<value>"
dotnet user-secrets set "Jwt:Secret" "<value>"

# Verify:
dotnet user-secrets list
```
```bash
# Non-dev / "closer-to-deployed" environments (D-04) — double-underscore convention,
# picked up automatically by the Environment Variables configuration provider, no code change:
setx IGDB__ClientId "<value>"
setx IGDB__ClientSecret "<value>"
setx Steam__ApiKey "<value>"
setx Jwt__Secret "<value>"
```
**Configuration precedence (verified):** `WebApplication.CreateBuilder(args)` registers providers in this order: `appsettings.json` → `appsettings.{Environment}.json` → **User Secrets (only when `IHostEnvironment.EnvironmentName == "Development"` AND the assembly has a `UserSecretsId`)** → Environment Variables → Command-line args [CITED: learn.microsoft.com/aspnet/core/security/app-secrets, learn.microsoft.com/aspnet/core/fundamentals/configuration]. Later providers override earlier ones for the same key. Practically: in Development, user-secrets values are present and env vars are typically absent, so user-secrets wins by being the only source; in Production, user-secrets is never registered at all (no `UserSecretsId` lookup happens outside Development), so env vars are the only source. **This means D-03/D-04's dev/non-dev split works correctly without any explicit ordering code** — it falls out naturally from ASP.NET Core's environment-gated user-secrets registration. No `builder.Configuration.AddUserSecrets(...)` call needs to be added manually — it's automatic for `WebApplication.CreateBuilder` once `<UserSecretsId>` exists in the `.csproj` (confirmed via `dotnet user-secrets init`, D-05).

### Pattern 4: gitleaks pre-commit hook, current syntax (CRED-05)
**What:** A native git hook that shells out to gitleaks against staged changes and blocks the commit on findings, soft-failing if the binary is missing.
**When to use:** D-11/D-12's exact scope — plain `.git/hooks/pre-commit`, no husky/pre-commit framework.
**Example:**
```bash
#!/bin/sh
# .git/hooks/pre-commit
# Source: gitleaks/gitleaks command migration guide [CITED: github.com/gitleaks/gitleaks]

if ! command -v gitleaks >/dev/null 2>&1; then
    echo "⚠ gitleaks not found on PATH — skipping secret scan (install: winget install gitleaks)"
    exit 0
fi

# gitleaks v8.19.0+ deprecated 'protect --staged' in favor of piping the staged diff to stdin.
if git diff --cached | gitleaks stdin; then
    exit 0
else
    echo "✖ gitleaks detected a potential secret in staged changes. Commit blocked."
    echo "  Review the findings above, remove the secret, and re-stage before committing."
    exit 1
fi
```
**Gotcha to flag for the plan:** `gitleaks protect --staged` (the command name that matches CONTEXT.md's plain-language phrasing "runs gitleaks protect --staged") still functions but is deprecated as of gitleaks v8.19.0 — hidden from `--help` but not removed [CITED: github.com/gitleaks/gitleaks command reference]. Either syntax works today; the `stdin`-based form above is what current gitleaks documentation demonstrates going forward, so it's the safer choice for a hook meant to keep working across future gitleaks upgrades.
**Windows note:** This repo's git hooks run via Git Bash's `sh` interpreter (bundled with Git for Windows, confirmed `git version 2.31.1.windows.1` present) — the `#!/bin/sh` hook above runs correctly with no WSL/Cygwin dependency, matching D-12's "Git Bash available" framing.

### Anti-Patterns to Avoid
- **Hand-building the form-encoded string:** `$"client_id={ClientId}&client_secret={ClientSecret}&grant_type=client_credentials"` as a `StringContent` body reintroduces string-interpolation risk (missing URL-encoding of special characters in the secret) and looks deceptively similar to the bug being fixed. Use `FormUrlEncodedContent` so the BCL handles encoding.
- **Logging `ex.Message` or `ex.ToString()` unmodified for Steam call failures:** `HttpRequestException` messages can include the failed URI in some scenarios; always log an explicitly-constructed message with named parameters (status code, context) instead of dumping the raw exception content when the URL contains a secret.
- **Hard-blocking commits when gitleaks is missing:** Contradicts D-13 explicitly — a missing binary must never prevent the solo developer from committing their own code on their own machine.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Form-encoding a POST body | Custom string-concatenation + URL-encoding helper | `System.Net.Http.FormUrlEncodedContent` | BCL type exists precisely for this; hand-rolling risks the exact under-encoding bugs OAuth2 form bodies are prone to |
| Dev secret storage | Custom encrypted-file-on-disk scheme or `.env` parser | `dotnet user-secrets` (SDK-bundled) | Already the ASP.NET Core-blessed mechanism, stores outside the repo entirely (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`), zero new dependencies |
| Secret pattern detection | Custom regex secret-scanner | gitleaks (D-10) | Regex-based secret detection has a large surface of known false-negative patterns (entropy tuning, vendor-specific key formats) that gitleaks's community-maintained ruleset already covers far better than a first pass would |

**Key insight:** Every mechanism this phase needs (form-encoded POST, dev secret storage, secret scanning) is already a solved problem with a first-party or community-standard tool — there is no part of CRED-01 through CRED-05 that benefits from a custom implementation.

## Runtime State Inventory

> Included because CRED-04 involves rotating previously-committed credentials — a "state that lives outside git" audit is warranted even though this isn't a rename/refactor phase in the traditional sense.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — IGDB/Steam credentials are not stored as data rows anywhere (no `Credentials` table); they're read from `IConfiguration` only, at call time, in `IgdbService`/`SteamService`. Verified by reading both service classes in full. | none |
| Live service config | The actual secret **values** live at the IGDB (Twitch dev console) and Steam (Steam API key page) provider dashboards — rotating them (D-06) is an external, out-of-band action the user must perform themselves; nothing in this repo can automate it. | Manual checkpoint (already flagged in D-06) |
| OS-registered state | None found — no Windows Task Scheduler entries, no pm2/launchd/systemd units reference these credential values by name. This phase does not touch process registration. | none |
| Secrets/env vars | `IGDB:ClientId`/`IGDB:ClientSecret`/`Steam:ApiKey`/`Jwt:Secret` currently exist ONLY in the committed `appsettings.Development.json` (confirmed by direct read — real, non-placeholder values present). No `.env` file, no CI/CD secret store currently references these keys (repo has no CI pipeline configured — confirmed no `.github/workflows/` directory exists). After this phase, the same *key names* move to user-secrets/env vars — code that reads `config["IGDB:ClientId"]` etc. needs **zero changes**, since `IConfiguration`'s colon-notation keys are provider-agnostic. | Code: none. Config: remove from JSON, add to user-secrets + env var docs. |
| Build artifacts | None relevant — no installed package or compiled binary embeds these secret values; they're read at runtime from configuration, never baked into a build output. | none |

**Nothing found in categories "Stored data," "OS-registered state," "Build artifacts":** confirmed by direct code/directory inspection, not merely assumed.

## Common Pitfalls

### Pitfall 1: Assuming `gitleaks protect --staged` is still the documented command
**What goes wrong:** Following older blog posts/tutorials (which dominate search results) leads to writing a hook using `gitleaks protect --staged` and being confused when a fresh gitleaks install's `--help` output doesn't mention it.
**Why it happens:** The command was deprecated in v8.19.0 but kept functional for backward compatibility — it works, but isn't documented anymore.
**How to avoid:** Use `git diff --cached | gitleaks stdin` (current syntax) in the hook script, or explicitly acknowledge in the plan that `gitleaks protect --staged` is being used deliberately as a still-functional legacy form.
**Warning signs:** `gitleaks --help` doesn't list `protect` as a subcommand on a freshly-installed version.

### Pitfall 2: `[Required]`/EF validation on `Jwt:Secret` masking a truly-empty secret at runtime
**What goes wrong:** After removing the real value from `appsettings.Development.json` and before running `dotnet user-secrets set`, the app may start with an empty `Jwt:Secret`, producing a cryptic `IDX10703`-style JWT signing-key error at first authenticated request rather than at startup.
**Why it happens:** ASP.NET Core doesn't validate JWT signing-key configuration eagerly by default — the failure surfaces lazily on first token validation.
**How to avoid:** Sequence the plan so `dotnet user-secrets set "Jwt:Secret" ...` happens in the same task/commit that removes the value from `appsettings.Development.json`, and smoke-test a login immediately after.
**Warning signs:** 500 errors specifically on `/api/auth/login` or any `[Authorize]` endpoint after the config change, with no compile-time or startup-time error.

### Pitfall 3: `FormUrlEncodedContent` double-disposal or content reuse
**What goes wrong:** `HttpContent`-derived objects like `FormUrlEncodedContent` are single-use; passing the same instance to a retried `PostAsync` call throws `ObjectDisposedException` or `InvalidOperationException` on the second attempt.
**Why it happens:** `HttpClient.PostAsync` disposes/reads the content stream after the first send.
**How to avoid:** Not currently an issue in `IgdbService.GetTokenAsync()` since there's no retry loop around the token call — but flag this if REL-03 work later adds retry logic to this specific call site.
**Warning signs:** `ObjectDisposedException: Cannot access a disposed object` on a second identical HTTP call.

### Pitfall 4: `WorkerStatus.LastError` truncation already exists — new failure-count column must not silently collide with it
**What goes wrong:** `WorkerStatusManager.SetFailedAsync` already truncates `LastError` to 500 chars (`error[..Math.Min(error.Length, 500)]`) for the *total sync failure* case (worker-level catch). D-16's new partial-failure fields describe a *different* case (sync completed, but N individual items failed) — if the new `FailedItemCount`/detail column reuses the existing `SetFailedAsync` path rather than a new/extended `SetCompletedAsync` overload, a partial failure will incorrectly flip `Status` to `"Failed"` when the sync as a whole succeeded.
**Why it happens:** The natural-seeming shortcut is to call `SetFailedAsync` whenever *any* item fails, conflating "the whole run failed" with "the run succeeded with some skipped items."
**How to avoid:** Extend `SetCompletedAsync(string name, int failedCount = 0, string? failureDetail = null)` as a new overload/parameter rather than routing partial failures through `SetFailedAsync`. Status stays `"Completed"`; the new columns carry the nuance.
**Warning signs:** `/api/worker/status` shows `Status: "Failed"` for a sync that actually processed 495/500 games successfully.

## Code Examples

### Extending WorkerStatusManager for partial-failure tracking (D-16)
```csharp
// Source: follows existing WorkerStatusManager.cs conventions (this file, read directly)
public async Task SetCompletedAsync(string name, int failedItemCount = 0, string? failureDetail = null)
{
    var w = await GetOrCreateAsync(name, CancellationToken.None);
    w.Status              = WorkerStatusValue.Completed;
    w.LastSuccessAt        = DateTime.UtcNow;
    w.LastError            = null;
    w.FailedItemCount      = failedItemCount;
    w.LastPartialFailureDetail = failureDetail?[..Math.Min(failureDetail.Length, 500)];
    await db.SaveChangesAsync();
}
```

### Numbered SQL script pattern for the WorkerStatus extension (D-16)
```sql
-- Source: follows Database/24_AlterGames_AddRatingFields.sql exactly (this file, read directly)
-- Database/35_AlterWorkerStatus_AddFailureTracking.sql
USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkerStatus') AND name = 'FailedItemCount')
    ALTER TABLE WorkerStatus ADD FailedItemCount INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkerStatus') AND name = 'LastPartialFailureDetail')
    ALTER TABLE WorkerStatus ADD LastPartialFailureDetail NVARCHAR(500) NULL;
GO
```
This mirrors the established convention exactly: `USE GamePlannerDB; GO`, an `IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('<Table>') AND name = '<Column>')` guard per column, each followed by its own `GO` batch separator. Next available number is `35` (last existing script is `34_FixStoredProcedure_UpsertIGDBGame.sql`).

### `.gitignore` addition required for D-03/D-09
```gitignore
# Local secrets (D-03) — real values live in dotnet user-secrets instead
GamePlanner.API/appsettings.Development.json
```
**Confirmed gap:** the current `.gitignore` (read directly) has no entry for `appsettings.Development.json` — it is currently tracked and committed with real secrets (confirmed via `git log`, single "Initial commit"). This line must be added as part of the CRED-03/CRED-09 work, and the file must be removed from git's index (`git rm --cached GamePlanner.API/appsettings.Development.json`) without deleting it from disk, since the local dev copy (once secrets are stripped from tracked content and replaced via user-secrets) is still needed locally — actually once secrets move to user-secrets, this file may lose its purpose or keep only non-secret keys like `HLTB:CsvPath`/`ConnectionStrings`. Flag for planner: decide whether `appsettings.Development.json` still needs to exist post-migration (containing only non-secret keys) or whether it's removed entirely in favor of `appsettings.json` + user-secrets. D-09 implies it still exists (a `.example` companion is created "once `appsettings.Development.json` is gitignored"), so the file remains, gitignored, containing the non-secret keys (`ConnectionStrings`, `HLTB:CsvPath`) plus empty placeholders for the moved secrets (for local discoverability/IDE intellisense), while the `.example` companion is the checked-in reference.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| `gitleaks protect --staged` | `git diff --cached \| gitleaks stdin` (or `gitleaks git --staged` variants) | v8.19.0 | Old command still works (deprecated, not removed) — plans/tutorials written before this version use outdated-but-functional syntax |
| `gitleaks detect --no-git -s .` | `gitleaks dir -s .` | v8.19.0 | Not directly relevant to this phase's staged-only hook, but confirms the broader CLI restructuring |

**Deprecated/outdated:**
- `gitleaks protect`/`gitleaks detect`: hidden from `--help` since v8.19.0 but not removed; safe to use either form, current docs favor `git`/`dir`/`stdin`.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | gitleaks current version is ~8.30.1 | Standard Stack | Low — version drifts naturally with `winget upgrade`; doesn't affect hook script correctness since both old and new command forms are supported across recent versions |
| A2 | `HttpRequestException` (not bare `Exception`) is precisely what `EnsureSuccessStatusCode`/`SendAsync` throw on Steam HTTP failures, making the CRED-02 example's narrower catch type safe | Pattern 2 | Medium — if an unrelated exception type (e.g., `JsonException` from a malformed response body) also needs redacted-logging treatment at these same 3 call sites, a bare `HttpRequestException` catch would miss it; verify against actual Steam API failure modes during implementation, or catch `Exception` here too and rely on the redacted message content alone (not narrowed catch type) to satisfy CRED-02 |
| A3 | The WorkerStatus schema field names (`FailedItemCount`, `LastPartialFailureDetail`) are reasonable defaults — CONTEXT.md explicitly delegates exact field naming to Claude's discretion (D-16) | Code Examples, Common Pitfalls #4 | Low — purely a naming choice, doesn't affect functional correctness; planner/implementer free to rename |

**If this table is empty:** N/A — see entries above.

## Open Questions

1. **Does `appsettings.Development.json` still exist post-migration, or is it deleted in favor of `appsettings.json` + user-secrets entirely?**
   - What we know: D-09 says a `.example` companion is created "once `appsettings.Development.json` is gitignored," implying the real file persists locally (gitignored) with non-secret keys (`ConnectionStrings`, `HLTB:CsvPath`) plus empty placeholders.
   - What's unclear: Whether the plan should have the implementer keep the file with placeholder secret values (defense-in-depth/discoverability) or genuinely empty/absent secret keys (forcing 100% reliance on user-secrets).
   - Recommendation: Keep the file, gitignored, with the four secret keys set to empty strings (matching `appsettings.json`'s existing pattern) plus the real `ConnectionStrings`/`HLTB:CsvPath` values — this preserves local discoverability without any real secret ever being trackable again.

2. **Does CRED-02's `try/catch` at the 3 Steam call sites use `catch (Exception)` or `catch (HttpRequestException)`?**
   - What we know: D-15 explicitly scopes "keep `catch (Exception)`, don't narrow" to the REL-03 Manager/Service sites (D-14's 8 listed sites), which do NOT include `SteamService.cs`'s 3 call sites (those are the CRED-02 sites, a separate requirement).
   - What's unclear: CONTEXT.md doesn't explicitly state whether CRED-02's new try/catch should also stay broad for consistency, or can be narrower since its purpose (prevent URL/key leakage) is different from REL-03's purpose (surface failures).
   - Recommendation: Flag this explicitly for the planner to decide in the PLAN.md task description — either choice is defensible, but the plan must pick one to avoid an ambiguous implementation. A safe default: use `catch (Exception)` at these 3 sites too (consistent style, and since Steam call failure modes beyond `HttpRequestException` — e.g., JSON deserialization — could also expose sensitive context if logged raw), while ensuring the log message itself is always redacted regardless of caught type.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build/run `GamePlanner.API` | ✓ | 9.0.317 (project targets `net8.0`; SDK 9 builds net8.0 projects without issue) | — |
| git | Pre-commit hook (CRED-05) | ✓ | 2.31.1.windows.1 | — |
| gitleaks | Pre-commit hook (CRED-05) | ✗ | — | D-13's soft-fail path (warn + allow commit) — this is the **actual current state**, not a hypothetical; installing it (`winget install gitleaks`) is part of the phase's execution, and the soft-fail branch must be verified to work correctly *before* installation too |
| Node.js | Unrelated to this phase (frontend not touched) | ✓ | v24.19.0 | — |

**Missing dependencies with no fallback:** none — gitleaks' absence has an explicit designed fallback (D-13).

**Missing dependencies with fallback:** gitleaks (fallback: soft-fail warn-and-allow, per D-13, until the developer runs `winget install gitleaks` as part of executing this phase).

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No (this phase doesn't touch login/password flow) | — |
| V3 Session Management | No (JWT lifetime is Phase 3's AUTH-01, not this phase) | — |
| V4 Access Control | No | — |
| V5 Input Validation | No new user input surfaces introduced | — |
| V6 Cryptography | No (no new crypto primitives; `Jwt:Secret` rotation is a value change, not an algorithm/library change) | — |
| V7 Error Handling and Logging (ASVS 4.0.3 §7) | **Yes** | REL-03/CRED-02's entire purpose maps directly here: "the application does not log credentials or payment details" (§7.1) and "a mechanism exists to block the same user from generating another log event for the same action within a short time" is not applicable, but the core control — never write secrets to logs, and never let generic exception handlers leak sensitive request data — is exactly what CRED-02/REL-03 implement. |
| V14 Configuration | **Yes** | CRED-03/CRED-04 map to ASVS §14.1/§2.10-adjacent guidance on not storing secrets in source-controlled config; `dotnet user-secrets` + environment-variable split is the standard control for this exact gap. |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Secrets committed to source control (already occurred — the exact finding this phase fixes) | Information Disclosure | Rotate the leaked credential at the provider (D-06); remove from tracked files going forward (D-03/D-09); add a pre-commit scanning gate (CRED-05) to prevent recurrence |
| Credentials in URL query strings, logged by proxies/access logs/exception messages | Information Disclosure | Move to POST body where the provider supports it (CRED-01); redact logging where it doesn't (CRED-02) |
| Silent exception swallowing masking a security-relevant failure (e.g., an auth token request silently failing and falling back to a stale/incorrect state) | Denial of Service (partial), Repudiation (no record failure occurred) | REL-03's explicit logging-with-severity + `WorkerStatus` surfacing ensures failures are auditable, not silently absorbed |

## Sources

### Primary (HIGH confidence)
- `E:/PROYECTO NO WORK/GamePlanner/GamePlanner.API/Services/IgdbService.cs` — direct read, confirms line 30 token-URL construction and existing structured-logging pattern
- `E:/PROYECTO NO WORK/GamePlanner/GamePlanner.API/Services/SteamService.cs` — direct read, confirms 3 call sites at lines 41, 59, 76 (actual: `ResolveSteamIdAsync` line 41, `GetPlayerSummaryAsync` line 59, `FetchOwnedGamesAsync` line 76)
- `E:/PROYECTO NO WORK/GamePlanner/GamePlanner.API/appsettings.Development.json` — direct read, confirms real committed secrets
- `E:/PROYECTO NO WORK/GamePlanner/GamePlanner.API/GamePlanner.API.csproj` — direct read, confirms no `<UserSecretsId>`
- `E:/PROYECTO NO WORK/GamePlanner/.gitignore` — direct read, confirms no entry for `appsettings.Development.json`
- `E:/PROYECTO NO WORK/GamePlanner/GamePlanner.API/Entities/WorkerStatus.cs`, `Managers/WorkerStatusManager.cs` — direct read, existing schema/manager pattern
- `E:/PROYECTO NO WORK/GamePlanner/Database/19_CreateWorkerStatus.sql`, `24_AlterGames_AddRatingFields.sql` — direct read, established numbered-script column-add convention
- Grep across `GamePlanner.API/` for `catch (Exception` — confirms all 8 D-14 line numbers still match current code exactly (verified 2026-08-30)
- Live environment probe (this session): `dotnet --version` (9.0.317), `git --version` (2.31.1.windows.1), `command -v gitleaks` (not found), `.git/hooks/` (no existing pre-commit, only `.sample` files)

### Secondary (MEDIUM confidence)
- [ASP.NET Core Safe storage of app secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0) — WebSearch-surfaced, official Microsoft Learn doc — `dotnet user-secrets init`/`set` syntax, colon-notation nested keys, Development-only auto-registration
- [Configuration in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0) — WebSearch-surfaced, official doc — provider precedence order
- [gitleaks/gitleaks GitHub repository](https://github.com/gitleaks/gitleaks) — WebSearch-surfaced, official repo — command deprecation (`detect`/`protect` → `git`/`dir`/`stdin`), v8.19.0 changeover
- [Install Gitleaks with WinGet | winstall](https://winstall.app/apps/Gitleaks.Gitleaks) — WebSearch-surfaced package listing — current version ~8.30.1, winget package ID
- [gitleaks generic-api-key false positives issue #1578](https://github.com/gitleaks/gitleaks/issues/1578) — WebSearch-surfaced, official issue tracker — default ruleset entropy threshold (3.5) and false-positive characteristics of the catch-all rule

### Tertiary (LOW confidence)
- Various Medium/blog OAuth2 `FormUrlEncodedContent` examples (not from an official .NET/Microsoft source) — pattern is standard/uncontroversial C# but tagged `[CITED: multiple .NET OAuth2 client_credentials examples]` rather than `[VERIFIED]` since no single authoritative Microsoft doc was matched for this exact snippet shape

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all core mechanisms (FormUrlEncodedContent, user-secrets, gitleaks) are either BCL/SDK built-ins directly verified in the codebase, or confirmed via official-source WebSearch results
- Architecture: HIGH — every file/line target was independently re-verified via grep against the live codebase, not just trusted from CONTEXT.md
- Pitfalls: MEDIUM — the `WorkerStatus` status-field collision pitfall (#4) and the CRED-02/REL-03 catch-scope ambiguity (Open Question #2) are original analysis, not externally sourced; flagged for explicit planner attention rather than presented as settled

**Research date:** 2026-08-30
**Valid until:** 2026-09-13 (14 days — gitleaks CLI syntax and version move fast enough that a 30-day default is too long; ASP.NET Core configuration/user-secrets behavior is stable for the full 30 days if this estimate needs splitting by claim)
