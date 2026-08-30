---
phase: 02-credential-secrets-hardening
plan: 01
subsystem: api
tags: [dotnet-user-secrets, aspnet-core-configuration, httpclient, oauth2, credential-hardening, logging]

# Dependency graph
requires: []
provides:
  - IGDB OAuth2 client_credentials token request transported via FormUrlEncodedContent POST body (no credential in URL)
  - dotnet user-secrets store seeded with IGDB:ClientId, IGDB:ClientSecret, Steam:ApiKey, Jwt:Secret
  - appsettings.Development.json untracked and secret-free, with a committed .example companion
  - Redacted, rethrow-preserving exception handling at all three SteamService HTTP call sites
affects: [02-02, 02-04, 02-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "FormUrlEncodedContent over Dictionary<string,string> for OAuth2 client_credentials bodies (avoids hand-built query strings)"
    - "catch (OperationCanceledException) { throw; } before catch (Exception ex) { log; throw; } at HTTP call sites carrying secrets in the URL"

key-files:
  created:
    - GamePlanner.API/appsettings.Development.json.example
  modified:
    - GamePlanner.API/GamePlanner.API.csproj
    - GamePlanner.API/appsettings.Development.json
    - .gitignore
    - GamePlanner.API/Services/IgdbService.cs
    - GamePlanner.API/Services/SteamService.cs

key-decisions:
  - "Seeded user-secrets with the real (pre-rotation) values rather than empty placeholders, per plan D-06 sequencing — keeps the app functional between this plan and the rotation plan (02-04)"
  - "Wrapped the business-rule 'vanity not found' throw inside ResolveSteamIdAsync's try block rather than splitting it out — it's redacted-safe (no key/URL in its message) and keeps the method's control flow simple; classified as within-scope discretion, not a deviation"

requirements-completed: [CRED-01, CRED-02, CRED-03]

coverage:
  - id: D1
    description: "IGDB token request sends credentials in a FormUrlEncodedContent POST body instead of a URL query string"
    requirement: "CRED-01"
    verification:
      - kind: integration
        ref: "Live app run, ASPNETCORE_ENVIRONMENT=Development: log line 'IGDB token acquired, expires in Ns' observed 3x with real IGDB API 200 responses; grep gate '! grep -Eq client_secret= IgdbService.cs' passes"
        status: pass
    human_judgment: false
  - id: D2
    description: "Configuration precedence proven: Development reads secrets from dotnet user-secrets, non-Development reads the same keys from IGDB__*/Steam__*/Jwt__* env vars, with zero code changes"
    requirement: "CRED-03"
    verification:
      - kind: integration
        ref: "Live app run, ASPNETCORE_ENVIRONMENT=Staging with no env vars: token fetch fails (400, empty client_id/secret), 'IGDB token acquired' absent. Same run with IGDB__ClientId/IGDB__ClientSecret exported: 'IGDB token acquired' present."
        status: pass
    human_judgment: false
  - id: D3
    description: "appsettings.Development.json untracked and secret-free; appsettings.Development.json.example committed as reference"
    requirement: "CRED-03"
    verification:
      - kind: other
        ref: "git status --porcelain --ignored shows '!! GamePlanner.API/appsettings.Development.json'; git ls-files --error-unmatch fails for the real file, succeeds for .example; file still present on disk"
        status: pass
    human_judgment: false
  - id: D4
    description: "All three SteamService HTTP call sites redact failure logs (no ApiKey, no url, no ex.Message/ex.ToString) and rethrow so callers still observe failures"
    requirement: "CRED-02"
    verification:
      - kind: integration
        ref: "Live app run with Steam:ApiKey temporarily set to an invalid value: log line 'Fetching owned games failed for SteamID 76561198115372977.' observed (Error level, no key/URL), exception rethrown and caught by SteamPlaytimeSyncManager as a warning. Real key restored afterward. Grep gates (ex.Message/ex.ToString, url, ApiKey absent from Log* calls; 3x catch(Exception ex); 3x catch(OperationCanceledException)) all pass."
        status: pass
    human_judgment: false

duration: 9min
completed: 2026-08-30
status: complete
---

# Phase 02 Plan 01: IGDB Credential Transport, Secret Storage Split & Steam Log Redaction Summary

**IGDB OAuth2 token now posts credentials via FormUrlEncodedContent instead of a URL query string; the four real secrets moved from committed JSON into dotnet user-secrets (dev) with the same keys read from IGDB__*/Steam__*/Jwt__* env vars outside Development; all three SteamService HTTP call sites now redact failures instead of risking a leaked key/URL through exception output.**

## Performance

- **Duration:** 9 min
- **Started:** 2026-08-30T18:25:27Z
- **Completed:** 2026-08-30T18:33:59Z
- **Tasks:** 2
- **Files modified:** 6 (1 new)

## Accomplishments
- `IgdbService.GetTokenAsync` builds a `Dictionary<string,string>` (`client_id`/`client_secret`/`grant_type`) wrapped in `FormUrlEncodedContent`, posted to the bare `TokenUrl` — the token URL no longer carries `client_secret=` in cleartext.
- `dotnet user-secrets init` generated `UserSecretsId` `326c88de-f1d5-4bb2-9dcf-31e48f38bc60` in `GamePlanner.API.csproj`; the store now holds `IGDB:ClientId`, `IGDB:ClientSecret`, `Steam:ApiKey`, `Jwt:Secret` (seeded with the pre-rotation real values, per D-06).
- `appsettings.Development.json` stripped to empty-string placeholders for the four secret keys, removed from the git index (`git rm --cached`) while remaining on disk, and gitignored; `appsettings.Development.json.example` committed as the secret-free reference copy.
- All three `SteamService` HTTP call sites (`ResolveSteamIdAsync`, `GetPlayerSummaryAsync`, `FetchOwnedGamesAsync`) now wrap their HTTP call and deserialization in `try { } catch (OperationCanceledException) { throw; } catch (Exception ex) { logger.LogError(ex, "...", <safe param>); throw; }` — no log statement in the file references `ApiKey` or the `url` local, and no exception text (`ex.Message`/`ex.ToString()`) is copied into a message string.
- Live-verified both halves of the CRED-03/D-04 configuration split (Development via user-secrets works; Staging without env vars fails to authenticate; Staging with `IGDB__ClientId`/`IGDB__ClientSecret` env vars succeeds) and the CRED-02 redaction behavior (invalid `Steam:ApiKey` produces a redacted Error log with no key/URL leak, then real key restored).

## Task Commits

Each task was committed atomically:

1. **Task 1: Credential path end-to-end — user-secrets source through form-encoded IGDB token fetch** - `6c59f38` (feat)
2. **Task 2: Redact Steam failure output at all three key-bearing call sites** - `352464d` (fix)

## Files Created/Modified
- `GamePlanner.API/GamePlanner.API.csproj` - Added `<UserSecretsId>326c88de-f1d5-4bb2-9dcf-31e48f38bc60</UserSecretsId>` (generated by `dotnet user-secrets init`, not hand-typed)
- `GamePlanner.API/appsettings.Development.json` - Four secret values (`IGDB:ClientId`, `IGDB:ClientSecret`, `Steam:ApiKey`, `Jwt:Secret`) replaced with empty strings; `ConnectionStrings:DefaultConnection` and `HLTB:CsvPath` untouched; file untracked via `.gitignore` + `git rm --cached`, still present on disk
- `GamePlanner.API/appsettings.Development.json.example` - New committed file, identical shape to the stripped `appsettings.Development.json`, for local-setup discoverability
- `.gitignore` - Added `GamePlanner.API/appsettings.Development.json` under a comment pointing at user-secrets as the real-value source
- `GamePlanner.API/Services/IgdbService.cs` - `GetTokenAsync` now builds a `FormUrlEncodedContent` body instead of a query string; caching, expiry math, and the `LogInformation` line are unchanged
- `GamePlanner.API/Services/SteamService.cs` - Added try/catch around the HTTP call + deserialization at all three call sites, each with a two-level catch (`OperationCanceledException` rethrow, then `Exception` → redacted `LogError` → rethrow)

## Decisions Made
- **D-06 sequencing honored:** user-secrets seeded with the real pre-rotation values (not empty placeholders) so the app stays functional between this plan and the rotation plan (02-04), and so the live IGDB token gate below is a meaningful proof rather than a no-op.
- **ResolveSteamIdAsync's business-rule throw kept inside the try block:** the "vanity URL not found" `InvalidOperationException` is redacted-safe (message names only the vanity string) and its inclusion inside the try means it also gets a redacted Error log via the generic catch — a minor severity choice, not a functional deviation, and does not violate any acceptance criterion (no key/URL/ex.Message logged, still rethrown).

## Deviations from Plan

None — plan executed exactly as written. Both tasks' automated verify gates and acceptance-criteria behavior checks (live IGDB token fetch, Staging negative/positive proof, Steam redaction proof) all passed on the first attempt.

## Issues Encountered

None.

## User Setup Required

None for this plan. Note for the phase as a whole: plan 02-04 (credential rotation, CRED-04) still needs the user to visit the Twitch dev console and Steam API key page to regenerate `IGDB:ClientSecret` and `Steam:ApiKey`, then run `dotnet user-secrets set` with the new values — this plan only set up the plumbing and seeded the (still pre-rotation) real values.

## Next Phase Readiness

- CRED-01, CRED-02, CRED-03 are closed. The secret-storage plumbing (user-secrets + env-var split) and both credential-transport/logging fixes are live and verified end-to-end.
- Sibling plans in this phase (02-02 WorkerStatus partial-failure tracking, 02-05 gitleaks pre-commit hook) are unblocked — neither depended on this plan's specific edits beyond the shared `appsettings.Development.json`/`.gitignore` state, which is now stable.
- Plan 02-04 (CRED-04, credential rotation) can proceed once the user has rotated the IGDB client secret and Steam API key at their respective provider dashboards; this plan's user-secrets entries are the target for `dotnet user-secrets set` with the new values.

## Self-Check: PASSED

- FOUND: GamePlanner.API/appsettings.Development.json.example
- FOUND: GamePlanner.API/GamePlanner.API.csproj
- FOUND: GamePlanner.API/Services/IgdbService.cs
- FOUND: GamePlanner.API/Services/SteamService.cs
- FOUND commit: 6c59f38
- FOUND commit: 352464d

---
*Phase: 02-credential-secrets-hardening*
*Completed: 2026-08-30*
