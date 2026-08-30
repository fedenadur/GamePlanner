# Phase 2: Credential & Secrets Hardening - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-30
**Phase:** 2-Credential & Secrets Hardening
**Areas discussed:** Todo triage, Secret rotation & storage split, Pre-commit secret scanning, REL-03 broad catch(Exception) scope, Steam credential-in-URL handling (CRED-02)

---

## Todo Triage (pre-discussion)

| Option | Description | Selected |
|--------|-------------|----------|
| Leave both as pending todos | Neither todo touches credential handling or external-API error handling | ✓ |
| Fold session-planner leak into Phase 2 | Treat cross-user leak as urgent, fix now despite different concern | |
| Fold IGDB upsert fix into Phase 2 | Apply the already-drafted scripts as part of this phase | |

**User's choice:** Leave both as pending todos.
**Notes:** Two todos scored as keyword matches (0.9, 0.6) but are data-isolation/data-integrity bugs, not credential/secrets issues. Kept separate.

---

## Secret Rotation & Storage Split

| Option | Description | Selected |
|--------|-------------|----------|
| IGDB ClientId/Secret, Steam ApiKey, Jwt Secret | Move only actual credentials; connection string and CSV path stay | ✓ |
| Everything in appsettings.Development.json | Move all values including non-secrets, for consistency | |

**User's choice:** IGDB ClientId/Secret, Steam ApiKey, Jwt Secret.

| Option | Description | Selected |
|--------|-------------|----------|
| ASPNETCORE_ env-var convention | Double-underscore env vars picked up automatically by IConfiguration | ✓ |
| Separate appsettings.Production.json gitignored file | File-based, kept out of source control | |

**User's choice:** ASPNETCORE_ env-var convention.

| Option | Description | Selected |
|--------|-------------|----------|
| You rotate now, give me the new values | User rotates via provider consoles, hands values to Claude | ✓ |
| I'll rotate them myself entirely | User handles rotation + local secrets setup outside conversation | |

**User's choice:** User rotates now, hands values to Claude to wire in.

| Option | Description | Selected |
|--------|-------------|----------|
| No, rotation is enough | Old committed values become useless once rotated | ✓ |
| Yes, scrub history too | Rewrite git history to remove leaked secrets | |

**User's choice:** No, rotation is enough — no history rewrite.

| Option | Description | Selected |
|--------|-------------|----------|
| No concern, rotate whenever | Personal/single-user app, re-login is fine | ✓ |
| Flag it, but don't sequence around it specially | Same outcome, just noted as a side effect | |

**User's choice:** No concern, rotate whenever.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, add a .example template | Commit placeholder-value template once real file is gitignored | ✓ |
| No, document in README/CLAUDE.md instead | Skip extra file | |

**User's choice:** Yes, add a `.example` template.

---

## Pre-commit Secret Scanning

| Option | Description | Selected |
|--------|-------------|----------|
| gitleaks | Static binary, native Windows support, fast | ✓ |
| trufflehog | Entropy + verified-secret detection, heavier | |

**User's choice:** gitleaks.

| Option | Description | Selected |
|--------|-------------|----------|
| Plain native git hook (.git/hooks/pre-commit) | Shell script calling gitleaks binary, no new dependency | ✓ |
| Husky (Node-based) | Managed via game-planner-ui's npm tooling | |

**User's choice:** Plain native git hook.

| Option | Description | Selected |
|--------|-------------|----------|
| Small setup script + CLAUDE.md note | scripts/install-git-hooks.ps1 copies hook, documented setup step | ✓ |
| Use git's core.hooksPath config instead | Hooks live in version control directly | |

**User's choice:** Small setup script + CLAUDE.md note.

| Option | Description | Selected |
|--------|-------------|----------|
| Warn and allow the commit through | Missing binary should never hard-block commits on personal machine | ✓ |
| Block the commit until gitleaks is installed | Fail closed | |

**User's choice:** Warn and allow the commit through.

---

## REL-03 Broad catch(Exception) Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Manager/Service-level only | GameImageManager, GameEnrichmentManager, GameSyncManager, SteamPlaytimeSyncManager, CsvGameService | ✓ |
| Both tiers | Also tighten 6 Worker-level top-loop catches | |

**User's choice:** Manager/Service-level only. Worker-level catches are a legitimate, separate resilience pattern.

| Option | Description | Selected |
|--------|-------------|----------|
| Keep catch(Exception), raise severity + stop silent continuation | LogWarning→LogError, ensure failures surface | ✓ |
| Catch specific exception types | Narrow to HttpRequestException/TaskCanceledException/JsonException | |

**User's choice:** Keep catch(Exception), raise severity + stop silent continuation.

| Option | Description | Selected |
|--------|-------------|----------|
| WorkerStatus table gets partial-failure info | Extend existing table, reuse /api/worker/status endpoint | ✓ |
| Log only, no status-table change | Keep visibility log-file-only | |

**User's choice:** WorkerStatus table gets partial-failure info.

---

## Steam Credential-in-URL Handling (CRED-02)

| Option | Description | Selected |
|--------|-------------|----------|
| Add explicit try/catch in SteamService that logs a redacted message | Wrap the three Steam HTTP calls, never log ApiKey/full URL | ✓ |
| Just verify no global handler logs raw exception messages | No code change if already satisfied | |

**User's choice:** Add explicit try/catch with redacted logging.

---

## Claude's Discretion

- Exact wording/format of redacted log messages (SteamService, REL-03 sites) — follow existing structured-logging conventions.
- Exact shape of the WorkerStatus schema extension for partial-failure info — Claude designs columns/fields, following the numbered-SQL-migration convention.
- Exact content/naming of the git-hook install script — PowerShell vs shell, given Windows dev machine with Git Bash available.

## Deferred Ideas

- Session planner cross-user data leak (`SessionManager.cs:15`) — stays a pending todo, not folded into Phase 2.
- IGDBId upsert dedup bug (`Database/33_...sql`, `34_...sql`) — stays a pending todo despite in-progress uncommitted fix; candidate for Phase 4 or an earlier standalone fix.
