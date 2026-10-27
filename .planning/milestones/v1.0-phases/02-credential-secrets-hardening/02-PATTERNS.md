# Phase 2: Credential & Secrets Hardening - Pattern Map

**Mapped:** 2026-08-30
**Files analyzed:** 13 (7 code edits, 2 config edits, 1 new config, 1 csproj edit, 1 new SQL script, 2 new git-tooling files)
**Analogs found:** 13 / 13 (all edits to existing files have themselves as the "analog" — this phase is a hardening pass, not new-feature scaffolding; the 2 brand-new files use a documented convention as their analog)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `GamePlanner.API/Services/IgdbService.cs` (edit) | service (external API) | request-response | itself — `GetTokenAsync()` (lines 24-43) | exact (self-edit) |
| `GamePlanner.API/Services/SteamService.cs` (edit) | service (external API) | request-response | `IgdbService.PostQueryAsync` structured-logging + error branch (lines 141-161) | role-match, same file's own 3 call sites are self-edits |
| `GamePlanner.API/Managers/GameImageManager.cs` (edit, 4 sites) | manager | batch / event-driven (per-item sync) | itself, lines 64-94 and 172-190 | exact (self-edit) |
| `GamePlanner.API/Managers/GameEnrichmentManager.cs` (edit, 1 site) | manager | batch | `GameImageManager.cs` per-item try/catch (lines 85-93) | exact pattern match, cross-file |
| `GamePlanner.API/Managers/GameSyncManager.cs` (edit, 1 site) | manager | batch | `GameEnrichmentManager.cs` lines 152-155 | exact pattern match, cross-file |
| `GamePlanner.API/Managers/SteamPlaytimeSyncManager.cs` (edit, 1 site) | manager | batch | `GameSyncManager.cs` lines 69-75 (top-level per-user try/catch) | exact pattern match, cross-file |
| `GamePlanner.API/Services/CsvGameService.cs` (edit, 2 sites) | service (file I/O) | file-I/O / batch | itself, lines 77-88 (nested try/catch: inner per-row, outer whole-file) | exact (self-edit) |
| `GamePlanner.API/Managers/WorkerStatusManager.cs` (edit — new `SetCompletedAsync` overload) | manager | CRUD (single-row upsert) | itself, `SetCompletedAsync`/`SetFailedAsync` (lines 21-36) | exact (self-edit) |
| `GamePlanner.API/Entities/WorkerStatus.cs` (edit — new columns) | model | CRUD | itself (lines 1-19) | exact (self-edit) |
| `Database/35_AlterWorkerStatus_AddFailureTracking.sql` (new) | migration | batch (DDL) | `Database/24_AlterGames_AddRatingFields.sql` | exact |
| `GamePlanner.API/appsettings.Development.json` (edit) + new `.example` companion | config | config | `GamePlanner.API/appsettings.json` (already-correct empty-placeholder pattern) | exact |
| `GamePlanner.API/GamePlanner.API.csproj` (edit — add `<UserSecretsId>`) | config | config | n/a — single-line SDK-standard addition, no in-repo analog needed | n/a |
| `.git/hooks/pre-commit` (new) + `scripts/install-git-hooks.ps1`/`.sh` (new) | utility (git tooling) | event-driven (git hook trigger) | none in repo — first hook/script of its kind | no analog (use RESEARCH.md Pattern 4 verbatim) |

## Pattern Assignments

### `GamePlanner.API/Services/IgdbService.cs` (service, request-response) — CRED-01

**Analog:** itself, `GetTokenAsync()` lines 24-43 (in-place edit)

**Current (to replace), lines 29-30:**
```csharp
var client = httpFactory.CreateClient();
var url = $"{TokenUrl}?client_id={ClientId}&client_secret={ClientSecret}&grant_type=client_credentials";

var response = await client.PostAsync(url, null);
```

**Imports pattern** (lines 1-4) — add `System.Net.Http` (for `FormUrlEncodedContent`, already implicitly available via `System.Net.Http.Headers` already imported):
```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GamePlanner.API.Domain;
```

**Target pattern** — replace URL-embedded query string with `FormUrlEncodedContent` body, keep everything else in the method (caching, expiry, logging) untouched:
```csharp
var client = httpFactory.CreateClient();
var form = new Dictionary<string, string>
{
    ["client_id"]     = ClientId,
    ["client_secret"] = ClientSecret,
    ["grant_type"]    = "client_credentials",
};

var response = await client.PostAsync(TokenUrl, new FormUrlEncodedContent(form));
response.EnsureSuccessStatusCode();
```

**Logging pattern to preserve** (line 41, existing, unchanged):
```csharp
logger.LogInformation("IGDB token acquired, expires in {Seconds}s", token.ExpiresIn);
```

**Error handling pattern to preserve/reuse for CRED-02** (lines 152-157) — this is the project's existing "log status+body, then EnsureSuccessStatusCode" convention referenced by RESEARCH.md Pattern 2:
```csharp
if (!response.IsSuccessStatusCode)
{
    var body = await response.Content.ReadAsStringAsync(ct);
    logger.LogError("IGDB error {Status}: {Body}", response.StatusCode, body);
    response.EnsureSuccessStatusCode();
}
```

---

### `GamePlanner.API/Services/SteamService.cs` (service, request-response) — CRED-02

**Analog:** `IgdbService.PostQueryAsync` structured-logging convention (above) — SteamService currently has zero try/catch around its 3 HTTP call sites.

**3 call sites to wrap** (imports unchanged — file already has `System.Text.Json`, `GamePlanner.API.Domain`):

1. `ResolveSteamIdAsync`, lines 40-43:
```csharp
var client = httpFactory.CreateClient();
var url = $"{BaseUrl}/ISteamUser/ResolveVanityURL/v1/?key={ApiKey}&vanityurl={vanity}";
var response = await client.GetAsync(url, ct);
response.EnsureSuccessStatusCode();
```

2. `GetPlayerSummaryAsync`, lines 58-61:
```csharp
var client   = httpFactory.CreateClient();
var url      = $"{BaseUrl}/ISteamUser/GetPlayerSummaries/v2/?key={ApiKey}&steamids={steamId}";
var response = await client.GetAsync(url, ct);
if (!response.IsSuccessStatusCode) return null;
```

3. `FetchOwnedGamesAsync`, lines 75-81:
```csharp
var client = httpFactory.CreateClient();
var url = $"{BaseUrl}/IPlayerService/GetOwnedGames/v1/?key={ApiKey}&steamid={steamId}&include_appinfo=true&format=json";
logger.LogInformation("Fetching owned games for SteamID: {SteamId}", steamId);
var response = await client.GetAsync(url, ct);
response.EnsureSuccessStatusCode();
```

**Target redaction pattern** (per RESEARCH.md Pattern 2 + Open Question #2 recommendation — use `catch (Exception)` for consistency with project style, but the log message itself is what must be redacted regardless of caught type):
```csharp
try
{
    var response = await client.GetAsync(url, ct);
    response.EnsureSuccessStatusCode();
    // ... existing deserialization logic unchanged ...
}
catch (Exception ex)
{
    logger.LogError(ex, "Steam vanity resolution failed for '{Vanity}'.", vanity);
    throw;
}
```
**Critical rule:** never interpolate `ApiKey` or the full `url` variable into the log message — only named, non-secret context (`vanity`, `steamId`, `response.StatusCode` if available before the exception). Never call `logger.LogError(ex.ToString())` or `logger.LogError(ex.Message)` directly since `HttpRequestException.Message` can echo the request URI on some BCL versions.

---

### `GamePlanner.API/Managers/GameImageManager.cs` (manager, batch) — REL-03

**Analog:** itself — 4 existing sites, all following the same `try { await external-call } catch (Exception ex) { logger.LogWarning(ex, "..., skipping/will retry.", ...); }` shape.

**Current sites** (lines 64-94, 172-190) — pattern to preserve (catch type), but per D-15 raise severity where the failure represents real data loss (not a transient/retryable blip) and track failure count for D-16:
```csharp
try
{
    var fetched = await igdb.FetchCoversByIdsAsync(withId.Select(g => g.IGDBId!.Value), ct);
    igdbResults = fetched.ToDictionary(r => r.Id);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Batch cover fetch failed, will retry individually."); // retryable — keep LogWarning
}
```
```csharp
try
{
    var result = await igdb.SearchCoverByNameAsync(game.Name, ct);
    await SaveCoverAsync(conn, game.Id, game.Name, game.IGDBUpdatedAt, result);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Cover search failed for '{Name}', skipping.", game.Name);
    failedCount++; // NEW: accumulate for WorkerStatus surfacing (D-16)
}
```
Apply the same `failedCount++`/`failedNames.Add(...)` accumulation to the screenshot/artwork sites (lines 172-190) — these represent permanent skips of a whole batch, a stronger candidate for `LogError` per D-15's "raise severity for real data loss" guidance:
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Screenshot fetch failed for batch, skipping screenshots."); // raised from LogWarning — whole batch lost, not per-item retry
    failedCount += igdbIds.Count();
}
```
The manager's public sync method must then return (or accept an accumulator for) `failedCount` so the calling Worker can pass it to `WorkerStatusManager.SetCompletedAsync(name, failedCount, detail)`.

---

### `GamePlanner.API/Managers/GameEnrichmentManager.cs`, `GameSyncManager.cs`, `SteamPlaytimeSyncManager.cs` (manager, batch) — REL-03

**Analog:** `GameImageManager.cs` per-item try/catch shape (above) — identical structure, just different call/log text:
```csharp
// GameEnrichmentManager.cs line 77
catch (Exception ex)
{
    logger.LogWarning(ex, "Enrichment failed for '{Name}', skipping.", item.Name);
    failedCount++;
}
```
```csharp
// GameSyncManager.cs line 152
catch (Exception ex)
{
    logger.LogWarning(ex, "Failed to enrich '{Name}', skipping.", name);
    failedCount++;
}
```
```csharp
// SteamPlaytimeSyncManager.cs line 72 — per-user, whole-sync failure (not per-item) — this one is a stronger LogError candidate since it drops an entire user's sync, not a single item
catch (Exception ex)
{
    logger.LogError(ex, "Failed to sync Steam playtime for user {UserId}.", userId); // raised from LogWarning
}
```

---

### `GamePlanner.API/Services/CsvGameService.cs` (service, file-I/O) — REL-03

**Analog:** itself, lines 77-88 — nested try/catch (inner: per-row `LogDebug` + `skipped++` counter already exists; outer: whole-file `LogError` + empty-list return already exists). This file is already closest to the D-16 target shape since it already tracks a `skipped` counter.
```csharp
try
{
    // ... row parsing ...
}
catch (Exception ex)
{
    logger.LogDebug(ex, "Skipping invalid row at line {Line}.", lineNumber);
    skipped++; // already exists — reuse this exact counter for D-16's failedItemCount
}
```
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Failed to read HLTB CSV file.");
    return [];
}
```
Action: thread the existing `skipped` count out through the method's return value (or an `out`/tuple) so `GameHLTBSyncWorker` can pass it to `WorkerStatusManager.SetCompletedAsync(name, skipped, ...)`.

---

### `GamePlanner.API/Managers/WorkerStatusManager.cs` (manager, CRUD) — REL-03/D-16

**Analog:** itself — `SetCompletedAsync`/`SetFailedAsync` (lines 21-36), and `SetFailedAsync`'s existing truncation pattern (line 34).

**Current:**
```csharp
public async Task SetCompletedAsync(string name)
{
    var w = await GetOrCreateAsync(name, CancellationToken.None);
    w.Status        = WorkerStatusValue.Completed;
    w.LastSuccessAt = DateTime.UtcNow;
    w.LastError     = null;
    await db.SaveChangesAsync();
}

public async Task SetFailedAsync(string name, string error)
{
    var w = await GetOrCreateAsync(name, CancellationToken.None);
    w.Status    = WorkerStatusValue.Failed;
    w.LastError = error[..Math.Min(error.Length, 500)];
    await db.SaveChangesAsync();
}
```

**Target — extend `SetCompletedAsync` with optional params, do NOT route partial failures through `SetFailedAsync`** (Pitfall 4 in RESEARCH.md — status must stay `"Completed"`):
```csharp
public async Task SetCompletedAsync(string name, int failedItemCount = 0, string? failureDetail = null)
{
    var w = await GetOrCreateAsync(name, CancellationToken.None);
    w.Status                   = WorkerStatusValue.Completed;
    w.LastSuccessAt            = DateTime.UtcNow;
    w.LastError                = null;
    w.FailedItemCount          = failedItemCount;
    w.LastPartialFailureDetail = failureDetail?[..Math.Min(failureDetail.Length, 500)]; // reuse existing truncation convention from SetFailedAsync line 34
    await db.SaveChangesAsync();
}
```
This is a backward-compatible overload via default params — all 5 existing call sites (`UpcomingGamesSyncWorker.cs:51`, `GameImageSyncWorker.cs:41`, `GameSyncWorker.cs:41`, `GameHLTBSyncWorker.cs:48,53`, `SteamPlaytimeSyncWorker.cs:41`) keep compiling unchanged; only the in-scope 4 workers (GameImageSyncWorker, GameSyncWorker, GameHLTBSyncWorker, SteamPlaytimeSyncWorker) need updating to pass the new args once their Manager methods return failure counts.

---

### `GamePlanner.API/Entities/WorkerStatus.cs` (model, CRUD) — D-16

**Analog:** itself (lines 1-19) — flat POCO, no attributes, `WorkerStatusValue` companion static class for string constants.
```csharp
public class WorkerStatus
{
    public int       Id            { get; set; }
    public string    Name          { get; set; } = string.Empty;
    public string    Status        { get; set; } = WorkerStatusValue.Idle;
    public DateTime? LastRunAt     { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public string?   LastError     { get; set; }
    // NEW (D-16):
    public int?      FailedItemCount          { get; set; }
    public string?   LastPartialFailureDetail { get; set; }
}
```

---

### `Database/35_AlterWorkerStatus_AddFailureTracking.sql` (migration) — D-16

**Analog:** `Database/24_AlterGames_AddRatingFields.sql` (exact convention: `USE`/`GO`, one `IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(...) AND name = ...)` guard per column, each followed by its own `GO`).
```sql
USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkerStatus') AND name = 'FailedItemCount')
    ALTER TABLE WorkerStatus ADD FailedItemCount INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkerStatus') AND name = 'LastPartialFailureDetail')
    ALTER TABLE WorkerStatus ADD LastPartialFailureDetail NVARCHAR(500) NULL;
GO
```
Note: `35` is the next available number (last existing is `34_FixStoredProcedure_UpsertIGDBGame.sql`, currently uncommitted in the working tree per git status — confirm the number is still free at execution time).

---

### `GamePlanner.API/appsettings.Development.json` + new `.example` companion — CRED-03/D-09

**Analog:** `GamePlanner.API/appsettings.json` (production template) — already has the exact target shape (empty-string placeholders for all 4 secrets, real non-secret values like `BatchSize`/`Issuer`/`Audience` untouched).

**Current `appsettings.Development.json` (to strip):**
```json
{
  "ConnectionStrings": { "DefaultConnection": "Server=localhost;Database=GamePlannerDB;Trusted_Connection=True;TrustServerCertificate=True;" },
  "IGDB":  { "ClientId": "39u3iiyyztinhr9l298v4r0nlic6rg", "ClientSecret": "3owuwmgthn1f6qlcolwm80obyeargp" },
  "Steam": { "ApiKey": "93F7A8ABA8333E07B6F17E185F3507EB" },
  "Jwt":   { "Secret": "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_KEY_32CHARS+" },
  "HLTB":  { "CsvPath": "E:\\PROYECTO NO WORK\\GamePlanner\\howlongtobeat_games.csv" }
}
```

**Target shape** — matches `appsettings.json`'s existing empty-placeholder pattern, keeps `ConnectionStrings`/`HLTB:CsvPath` (per D-03, not secrets):
```json
{
  "ConnectionStrings": { "DefaultConnection": "Server=localhost;Database=GamePlannerDB;Trusted_Connection=True;TrustServerCertificate=True;" },
  "IGDB":  { "ClientId": "", "ClientSecret": "" },
  "Steam": { "ApiKey": "" },
  "Jwt":   { "Secret": "" },
  "HLTB":  { "CsvPath": "E:\\PROYECTO NO WORK\\GamePlanner\\howlongtobeat_games.csv" }
}
```
`appsettings.Development.json.example` — identical content, committed as the checked-in reference (per D-09).

**`.gitignore` addition** (current file confirmed has no entry for this path, lines 1-38 read in full):
```gitignore
# Local secrets (D-03) — real values live in dotnet user-secrets instead
GamePlanner.API/appsettings.Development.json
```
Followed by `git rm --cached GamePlanner.API/appsettings.Development.json` (keep on disk, remove from index).

---

### `GamePlanner.API/GamePlanner.API.csproj` (config) — D-05

**Analog:** none needed — single-element addition inside the existing `<PropertyGroup>` (lines 3-7):
```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>GENERATE-A-GUID-HERE</UserSecretsId>
</PropertyGroup>
```
Generated automatically by running `dotnet user-secrets init` from `GamePlanner.API/` — do not hand-write the GUID.

---

### `.git/hooks/pre-commit` + `scripts/install-git-hooks.ps1`/`.sh` (new, git tooling) — CRED-05

**No analog in this repo** — first git hook/tooling script introduced. Use RESEARCH.md Pattern 4 verbatim (already vetted against current gitleaks CLI syntax and this machine's confirmed Git Bash `sh` availability):
```sh
#!/bin/sh
# .git/hooks/pre-commit
if ! command -v gitleaks >/dev/null 2>&1; then
    echo "gitleaks not found on PATH — skipping secret scan (install: winget install gitleaks)"
    exit 0
fi

if git diff --cached | gitleaks stdin; then
    exit 0
else
    echo "gitleaks detected a potential secret in staged changes. Commit blocked."
    echo "Review the findings above, remove the secret, and re-stage before committing."
    exit 1
fi
```
Install script (`scripts/install-git-hooks.ps1` — PowerShell, per repo's primary shell) copies this file into `.git/hooks/pre-commit` and (on Windows, executable bit is not meaningful, but under Git Bash `chmod +x` should still be applied for portability).

## Shared Patterns

### Structured logging (`ILogger<T>`, named parameters)
**Source:** `IgdbService.cs` throughout (e.g., line 41, lines 155), `GameImageManager.cs` per-item catch blocks
**Apply to:** every new/changed log statement in this phase (CRED-02, REL-03 sites) — always named parameters (`{Name}`, `{UserId}`, `{Status}`), never string-interpolated secrets or raw `ex.ToString()`.

### Per-item try/catch with `LogWarning` (retryable) vs `LogError` (data loss) severity split
**Source:** `GameImageManager.cs` (4 sites), `GameEnrichmentManager.cs`, `GameSyncManager.cs`, `SteamPlaytimeSyncManager.cs`, `CsvGameService.cs`
**Apply to:** all 8 D-14 REL-03 sites — keep `catch (Exception)` (D-15), only change severity + add failure-count accumulation, never remove the catch or let it propagate unhandled.

### WorkerStatus surfacing via `SetCompletedAsync` overload (never `SetFailedAsync` for partial failures)
**Source:** `WorkerStatusManager.cs` lines 21-28, extended per D-16
**Apply to:** `GameImageSyncWorker.cs`, `GameSyncWorker.cs`, `GameHLTBSyncWorker.cs`, `SteamPlaytimeSyncWorker.cs` — the 4 Workers whose Managers/Services are in scope for D-14. `UpcomingGamesSyncWorker.cs` is out of scope (its manager isn't in the D-14 list) and needs no change.

### Empty-string placeholder convention for secret config keys
**Source:** `appsettings.json` (already correct) lines 12-21
**Apply to:** `appsettings.Development.json` and its new `.example` companion (CRED-03/D-09).

### Numbered SQL migration convention
**Source:** `Database/24_AlterGames_AddRatingFields.sql`
**Apply to:** `Database/35_AlterWorkerStatus_AddFailureTracking.sql` (D-16).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `.git/hooks/pre-commit` | utility (git tooling) | event-driven | No existing git hook or Node hook framework in this repo (confirmed: only `.sample` files in `.git/hooks/`) — use RESEARCH.md Pattern 4 as the source of truth instead |
| `scripts/install-git-hooks.ps1` (or `.sh`) | utility | file-I/O | No `scripts/` directory or prior setup-script convention exists in this repo |

## Metadata

**Analog search scope:** `GamePlanner.API/Services/`, `GamePlanner.API/Managers/`, `GamePlanner.API/Entities/`, `GamePlanner.API/Workers/`, `Database/`, repo root config files (`.gitignore`, `appsettings*.json`, `.csproj`)
**Files scanned:** `IgdbService.cs`, `SteamService.cs`, `GameImageManager.cs`, `GameEnrichmentManager.cs`, `GameSyncManager.cs`, `SteamPlaytimeSyncManager.cs`, `CsvGameService.cs`, `WorkerStatusManager.cs`, `WorkerStatus.cs`, `Database/24_AlterGames_AddRatingFields.sql`, `appsettings.json`, `appsettings.Development.json`, `GamePlanner.API.csproj`, `.gitignore`, all 5 `Workers/*.cs` files (grep for `WorkerStatusManager` usage)
**Pattern extraction date:** 2026-08-30
