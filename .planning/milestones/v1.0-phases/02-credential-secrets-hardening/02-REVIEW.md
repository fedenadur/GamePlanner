---
phase: 02-credential-secrets-hardening
reviewed: 2026-08-30T00:00:00Z
depth: standard
files_reviewed: 20
files_reviewed_list:
  - Database/35_AlterWorkerStatus_AddFailureTracking.sql
  - GamePlanner.API/Entities/WorkerStatus.cs
  - GamePlanner.API/GamePlanner.API.csproj
  - GamePlanner.API/Managers/GameEnrichmentManager.cs
  - GamePlanner.API/Managers/GameImageManager.cs
  - GamePlanner.API/Managers/GameImportManager.cs
  - GamePlanner.API/Managers/GameSyncManager.cs
  - GamePlanner.API/Managers/SteamPlaytimeSyncManager.cs
  - GamePlanner.API/Managers/WorkerStatusManager.cs
  - GamePlanner.API/Services/CsvGameService.cs
  - GamePlanner.API/Services/IgdbService.cs
  - GamePlanner.API/Services/SteamService.cs
  - GamePlanner.API/Workers/GameHLTBSyncWorker.cs
  - GamePlanner.API/Workers/GameImageSyncWorker.cs
  - GamePlanner.API/Workers/GameSyncWorker.cs
  - GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs
  - GamePlanner.API/appsettings.Development.json
  - GamePlanner.API/appsettings.Development.json.example
  - scripts/pre-commit
  - scripts/install-git-hooks.ps1
  - CLAUDE.md
findings:
  critical: 0
  warning: 6
  info: 5
  total: 11
status: issues_found
---

# Phase 02: Code Review Report

**Reviewed:** 2026-08-30
**Depth:** standard
**Files Reviewed:** 20
**Status:** issues_found

## Summary

Reviewed the credential/secrets hardening phase: IGDB/Steam credential transport and storage (`IgdbService`, `SteamService`, `appsettings.Development.json*`), the new `WorkerStatus` partial-failure tracking plumbing (`WorkerStatusManager`, `WorkerStatus` entity, migration `35_...sql`, and the four `*SyncManager`/`*SyncWorker` pairs that now surface `(FailedCount, FailureDetail)`), and the gitleaks pre-commit hook (`scripts/pre-commit`, `scripts/install-git-hooks.ps1`).

Note per reviewer instructions: `GameImageManager.cs` was reviewed at its committed state (`git show HEAD:...`), not the current working tree, which carries unrelated uncommitted WIP (grouping multiple local games under one IGDB ID) that is out of scope for this phase.

The core credential-transport fix (moving IGDB client_id/client_secret from a URL query string into a POST body) and the Steam redaction wrapper are correctly implemented and match the phase's stated intent. No hardcoded secrets, empty catch blocks, or SQL/command injection were found in the reviewed files. However, several places where this phase added new failure-tracking/comments do not match the behavior they claim, and one new field (`FailedItemCount`/`LastPartialFailureDetail`) is not consistently reset across all `WorkerStatusManager` transitions, which will produce misleading operator-facing state. Full details below.

## Warnings

### WR-01: `GameImageManager` cover-fetch retry comment does not match the code

**File:** `GamePlanner.API/Managers/GameImageManager.cs:87-98, 100-105` (as committed in `8858bef`)
**Issue:** When the batch call `igdb.FetchCoversByIdsAsync(...)` throws for the `withId` games, the catch block (comment added in this phase) says:
```csharp
// Retried per-game immediately below — not a permanent loss, keep Warning and don't count.
logger.LogWarning(ex, "Batch cover fetch failed, will retry individually.");
```
But the loop immediately below only does this:
```csharp
foreach (var game in withId)
{
    ct.ThrowIfCancellationRequested();
    igdbResults.TryGetValue(game.IGDBId!.Value, out var result);
    await SaveCoverAsync(conn, game.Id, game.Name, game.IGDBUpdatedAt, result);
}
```
`igdbResults` is empty (the batch call failed), so `TryGetValue` always misses and every `withId` game in the batch is passed `result = null` into `SaveCoverAsync`, which logs `"No cover found for '{Name}'."` and returns — there is no per-game IGDB call for this path (unlike `withoutId`, which does call `igdb.SearchCoverByNameAsync` per game in its own try/catch). The batch is silently treated as "no cover" rather than retried, and — because this path doesn't hit the `catch` in the `withoutId` loop — none of it is counted toward the new `failedCount`/`failedNames` this phase introduces, understating real failures in `WorkerStatus.FailedItemCount`.
**Fix:** Either add a genuine per-game fallback (e.g. call `igdb.FetchCoverByIdAsync(game.IGDBId!.Value, ct)` inside a try/catch for each `withId` game when the batch call fails, mirroring the `withoutId` path and incrementing `failedCount`/`failedNames` on individual failure), or correct the comment to say the batch will simply be re-attempted on the next scheduled worker run (since these games still have `CoverImageUrl == null` and remain in the query), and count the whole batch as failed here for accurate metrics.

### WR-02: Screenshot/artwork batch failures still mark games as "synced," undermining the fix's own stated goal

**File:** `GamePlanner.API/Managers/GameImageManager.cs:205-256` (as committed in `8858bef`)
**Issue:** The diff raises the log level to `LogError` and adds a comment acknowledging the problem it does not fix:
```csharp
// Raised from Warning: the batch's games are still marked synced below, so a
// failed fetch here means up to BatchSize games silently go two days without images.
logger.LogError(ex, "Screenshot fetch failed for batch, skipping screenshots.");
batchFailed = true;
```
Despite `batchFailed = true` (and the batch being correctly counted toward `failedCount`/`failedNames`), the code unconditionally continues to call `sp_UpdateGameLastImageSync` for every game in the batch a few lines later, so those up-to-50 games' `LastImageSyncDate` is bumped to "now" even though they received zero images. On the next worker run (within the 2-day `cutoff`), these games will be excluded by the freshness filter and will not be retried until the cutoff window passes again — this is a real, currently-unaddressed reliability gap that the new failure-counting only makes *visible*, not *fixed*.
**Fix:** Skip the `sp_UpdateGameLastImageSync` call for games belonging to a failed batch (or only call it for games that received at least one successfully-saved image), so a failed fetch doesn't cause a 2-day silent gap.

### WR-03: `WorkerStatusManager.SetFailedAsync`/`SetIdleAsync` leave stale `FailedItemCount`/`LastPartialFailureDetail`

**File:** `GamePlanner.API/Managers/WorkerStatusManager.cs:32-45`
**Issue:** `SetCompletedAsync` correctly overwrites `FailedItemCount`/`LastPartialFailureDetail` on every call (including to `null` on a fully clean run), but `SetFailedAsync` and `SetIdleAsync` do not touch these two fields at all:
```csharp
public async Task SetFailedAsync(string name, string error)
{
    var w = await GetOrCreateAsync(name, CancellationToken.None);
    w.Status    = WorkerStatusValue.Failed;
    w.LastError = error[..Math.Min(error.Length, 500)];
    await db.SaveChangesAsync();
}
```
Scenario: run N completes with 5 partial failures (`FailedItemCount = 5`, `LastPartialFailureDetail = "..."`). Run N+1 throws before any per-item work happens and hits `SetFailedAsync`. The `WorkerStatus` row now shows `Status = Failed`, a fresh `LastError`, but a stale `FailedItemCount = 5` / `LastPartialFailureDetail` from the previous (different) run — an operator reading this row would incorrectly attribute run N's partial-failure detail to run N+1's total failure.
**Fix:** Clear both fields in `SetFailedAsync` (and arguably `SetRunningAsync`/`SetIdleAsync`) so stale values never survive into an unrelated run:
```csharp
w.Status                   = WorkerStatusValue.Failed;
w.LastError                = error[..Math.Min(error.Length, 500)];
w.FailedItemCount          = null;
w.LastPartialFailureDetail = null;
```

### WR-04: Total HLTB read failures are reported as "Completed" with a failure detail instead of "Failed"

**File:** `GamePlanner.API/Workers/GameHLTBSyncWorker.cs:43-60`, `GamePlanner.API/Services/CsvGameService.cs:9-51`
**Issue:** `CsvGameService.ReadGames()` now returns `(records, skipped, failureDetail)`, but for total failures — `HLTB:CsvPath` not configured, file missing, empty file, or missing `game_name` header — it returns `skipped = 0` alongside a non-null `failureDetail` (e.g. `"HLTB CSV file not found"`). `GameHLTBSyncWorker.RunAsync` then calls:
```csharp
await statusManager.SetCompletedAsync(WorkerName, skipped, failureDetail);
if (skipped > 0)
    logger.LogWarning("{Worker}: {FailedCount} item(s) failed.", WorkerName, skipped);
```
Because `skipped == 0` in these total-failure cases, the worker never logs the warning, yet persists `Status = Completed`, `FailedItemCount = 0`, `LastPartialFailureDetail = "HLTB CSV file not found"` — a monitoring dashboard reading `Status`/`FailedItemCount` alone would see a "clean Completed run," and only close reading of `LastPartialFailureDetail` reveals that zero work was actually done because of a configuration/file error.
**Fix:** Distinguish "read succeeded with N skipped rows" (→ `SetCompletedAsync`) from "read failed entirely" (→ `SetFailedAsync(WorkerName, failureDetail)`), e.g. by returning a dedicated `bool Success` from `ReadGames()` or checking `records.Count == 0 && failureDetail is not null` before choosing which status setter to call.

### WR-05: `scripts/pre-commit` cannot distinguish a `gitleaks` CLI error from an actual secret detection

**File:** `scripts/pre-commit:9-15`
**Issue:**
```sh
if git diff --cached | gitleaks stdin; then
    exit 0
else
    echo "gitleaks detected a potential secret in staged changes. Commit blocked."
    exit 1
fi
```
The hook's `else` branch always prints "gitleaks detected a potential secret," but a non-zero exit from `gitleaks stdin` can just as easily mean the installed `gitleaks` version doesn't support the `stdin` subcommand, a bad flag, a crash, or any other CLI-level error — none of which are "a potential secret was found." There is no version pin/check and no `2>&1` capture to differentiate. This is the phase's sole secrets-scanning safety net (02-05, CRED-05-equivalent), so a misleading or (worse) fail-open behavior here directly undercuts the hardening goal.
**Fix:** Verify `gitleaks stdin` exists for the documented install target (`winget install --id Gitleaks.Gitleaks --exact`) and pin/document a minimum version; consider capturing stderr separately so a CLI usage error surfaces a distinct message from an actual finding, e.g.:
```sh
output=$(git diff --cached | gitleaks stdin 2>&1); rc=$?
if [ $rc -eq 0 ]; then exit 0; fi
echo "$output"
echo "gitleaks exited with code $rc — check output above (secret found or CLI error)."
exit 1
```

### WR-06: Real secrets committed in `daf1b71` remain in git history and on the pushed `origin/main`, even though the file is now gitignored

**File:** `GamePlanner.API/appsettings.Development.json` (git history), `.gitignore`
**Issue:** `git log` shows `daf1b71` ("Initial commit") committed real values for `IGDB:ClientId`, `IGDB:ClientSecret`, `Steam:ApiKey`, and `Jwt:Secret` in `appsettings.Development.json`; `6c59f38` (this phase) strips the file to empty placeholders and gitignores it going forward, but does not rewrite history. `git merge-base --is-ancestor daf1b71 origin/main` confirms that commit is reachable from the pushed remote (`git@github.com:fedenadur/GamePlanner.git`), meaning the plaintext historical secrets are already on GitHub and recoverable by anyone with clone access via `git show daf1b71:GamePlanner.API/appsettings.Development.json`. The good news: plan 02-04 (per `02-04-SUMMARY.md`) live-verified rotation of all three provider-issued secrets, so the specific leaked values are dead — this is a hygiene/defense-in-depth gap, not an active credential exposure.
**Fix:** If acceptable given the repo's exposure profile, rewrite history to purge the secret-bearing blob (`git filter-repo` or BFG Repo-Cleaner) and force-push with team coordination; at minimum, document in the phase record that history still contains dead-but-recognizable secret values and that rotation (not history removal) is the mitigation in place, so a future reviewer doesn't mistake old commits for a live leak.

## Info

### IN-01: `GameSyncManager`/`GameEnrichmentManager` populate both `Tags` and `Themes` from the same IGDB `themes` field

**File:** `GamePlanner.API/Managers/GameSyncManager.cs:102-129`, `GamePlanner.API/Managers/GameEnrichmentManager.cs:45-73`
**Issue:** `themeNames` (from `result.Themes?.Select(t => t.Name)`) is used as the source for both `game.Tags` and `game.Themes` (`GameSyncManager`), and as the sole source for `game.Tags` (`GameEnrichmentManager`, where the local variable is even named `tagNames` but is assigned from `result.Themes`). This may be an intentional mapping (IGDB has no distinct "tags" facet), but it produces two DB-level collections (`Tags`, `Themes`) holding identical name sets, which looks like a copy-paste artifact and is worth confirming is not a leftover from an earlier `result.Tags`/`result.Keywords`-style field that no longer exists on `IgdbGame`.
**Fix:** Confirm intent with a code comment (e.g. `// IGDB has no separate "tags" concept; mirror themes here by design`), or source `Tags` from a distinct IGDB field (e.g. keywords) if one is meant to be used.

### IN-02: `GameImportManager.ImportWithProgressAsync` never calls `enrichment.EnrichAsync`

**File:** `GamePlanner.API/Managers/GameImportManager.cs:97-167`
**Issue:** `ImportFromSteamAsync` (the synchronous import path) calls `enrichment.EnrichAsync(ct)` after inserting/updating games. `ImportWithProgressAsync` (the batched, progress-tracked import path used by `ImportJob`) does the same insert/update work but never calls enrichment — games imported through this path are left without Genre/Tag enrichment from this manager (they'll only pick it up later via the separate `GameSyncManager`/`GameEnrichmentManager` background workers, if those are running).
**Fix:** If the two import paths are meant to have parity, add the same `enrichment.EnrichAsync(ct)` call (with the same failure-count logging) at the end of `ImportWithProgressAsync`; otherwise leave a comment noting it's deliberately deferred to the background sync workers.

### IN-03: Unintended BOM added to `GamePlanner.API.csproj`

**File:** `GamePlanner.API/GamePlanner.API.csproj:1`
**Issue:** The diff shows `-<Project Sdk="Microsoft.NET.Sdk.Web">` → `+﻿<Project Sdk="Microsoft.NET.Sdk.Web">` — a UTF-8 byte-order-mark was prepended to the file as a side effect of adding `<UserSecretsId>`. Harmless to MSBuild, but inconsistent with the rest of the repo (no other reviewed file carries a BOM) and can cause noisy diffs/tooling warnings later.
**Fix:** Re-save the file without a BOM (most editors have a "save without BOM" / UTF-8 encoding option), or normalize via `dotnet format` / a `.editorconfig` encoding rule.

### IN-04: Failure-detail truncation uses two undocumented, inconsistent magic numbers (480 vs. 500)

**File:** `GamePlanner.API/Managers/GameEnrichmentManager.cs:101`, `GameSyncManager.cs:82`, `GameImageManager.cs:56`, `SteamPlaytimeSyncManager.cs:54`, `WorkerStatusManager.cs:28`, `Database/35_AlterWorkerStatus_AddFailureTracking.sql:9`
**Issue:** Each of the four managers truncates its failure-detail string to 480 chars before returning it (`detail.Length > 480 ? detail[..480] : detail`), while `WorkerStatusManager.SetCompletedAsync` independently re-truncates to 500 (matching the `NVARCHAR(500)` column). Both work today, but the two limits are unrelated magic numbers defined in five separate places with no shared constant — a future change to the column width (or to one manager's cap) can silently desync the others.
**Fix:** Define a single `const int MaxFailureDetailLength = 480;` (or similar) in a shared location and reference it from all five call sites, or drop the managers' own truncation and rely solely on `WorkerStatusManager`'s.

### IN-05: Duplicated `if (skipped > 0) logger.LogWarning(...)` block in `GameHLTBSyncWorker.RunAsync`

**File:** `GamePlanner.API/Workers/GameHLTBSyncWorker.cs:50-51, 59-60`
**Issue:** The same two-line warning-log block appears verbatim in both the `records.Count == 0` early-return branch and after the `hltbManager.UpsertAsync` call — minor duplication that could drift if one copy is edited without the other.
**Fix:** Move the `if (skipped > 0) logger.LogWarning(...)` call to a single spot after the `try` block's shared logic, or extract a small local function.

---

_Reviewed: 2026-08-30_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
