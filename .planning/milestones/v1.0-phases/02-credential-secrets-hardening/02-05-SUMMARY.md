---
phase: 02-credential-secrets-hardening
plan: 05
subsystem: infra
tags: [gitleaks, git-hooks, secret-scanning, powershell]

requires:
  - phase: 02-credential-secrets-hardening (02-01, 02-04)
    provides: secrets removed from tracked config and rotated at providers
provides:
  - Native git pre-commit hook (scripts/pre-commit) that scans the staged diff with gitleaks and blocks commits containing secret-shaped values
  - Installer script (scripts/install-git-hooks.ps1) that reproduces the untracked .git/hooks/pre-commit after a fresh clone
  - CLAUDE.md "Local setup (one-time)" section documenting hook install and the four secrets' Development/environment-variable sources
affects: [any future phase touching config, external service credentials, or git tooling]

tech-stack:
  added: [gitleaks 8.30.1 (winget package Gitleaks.Gitleaks, standalone binary, not a project dependency)]
  patterns: [POSIX sh git hook piping `git diff --cached` into `gitleaks stdin`, PowerShell installer copying tracked source into untracked .git/hooks]

key-files:
  created: [scripts/pre-commit, scripts/install-git-hooks.ps1]
  modified: [CLAUDE.md]

key-decisions:
  - "Installed gitleaks via winget --id Gitleaks.Gitleaks --exact; confirmed the resolved source is the official Gitleaks.Gitleaks package pointing at gitleaks/gitleaks on GitHub, installer hash verified"
  - "install-git-hooks.ps1 stages scripts/pre-commit with `git add` before running `git update-index --chmod=+x` — the chmod call requires the path to already exist in the index, which is true after a fresh clone but not on this repo's first-time authoring run; staging first makes the script work in both cases"
  - "The plan's suggested AWS-shaped probe (AKIA + 16 random chars) does NOT trigger a finding under gitleaks 8.30.1's embedded default ruleset on this machine (verified: canonical AWS example key, a synthetic AKIA-prefixed key, a GitHub PAT prefix, and an RSA private key header all scanned clean). The JSON-style `\"api_key\": \"<32-char random>\"` shape reliably triggers the `generic-api-key` rule and was used instead. Documented here since it is a real, verified deviation from the plan's assumption about which default rule fires deterministically — flagged for anyone tightening the hook later."

requirements-completed: [CRED-05]

coverage:
  - id: D1
    description: "A staged change containing a secret-shaped value causes git commit to abort with a non-zero exit and a message pointing at the gitleaks findings"
    requirement: "CRED-05"
    verification:
      - kind: manual_procedural
        ref: "git commit with a staged .gitleaks-hook-probe.tmp containing a generic-api-key-shaped JSON secret; observed exit 1 and block message, then unstaged/deleted"
        status: pass
    human_judgment: false
  - id: D2
    description: "When gitleaks is not on PATH, the hook prints an install hint and exits 0, letting the commit through"
    requirement: "CRED-05"
    verification:
      - kind: manual_procedural
        ref: "env PATH=/usr/bin:/bin sh scripts/pre-commit; observed exit 0 and install-hint message"
        status: pass
    human_judgment: false
  - id: D3
    description: "An ordinary commit containing no secret-shaped value passes the hook unchanged"
    requirement: "CRED-05"
    verification:
      - kind: manual_procedural
        ref: "git commit of scripts/pre-commit, scripts/install-git-hooks.ps1, CLAUDE.md with the hook active; observed 'no leaks found' and successful commit 7781881"
        status: pass
    human_judgment: false
  - id: D4
    description: "Hook installed locally at .git/hooks/pre-commit, byte-identical to tracked source, and CLAUDE.md documents the one-time setup"
    requirement: "CRED-05"
    verification:
      - kind: manual_procedural
        ref: "diff scripts/pre-commit .git/hooks/pre-commit (exit 0); grep checks for install-git-hooks/user-secrets/IGDB__ClientId in CLAUDE.md; all pass"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-08-31
status: complete
---

# Phase 2 Plan 5: Gitleaks Pre-Commit Hook Summary

**Native git pre-commit hook pipes the staged diff into gitleaks 8.30.1, blocking commits with secret-shaped values while soft-failing open when the binary is absent from PATH**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-08-31T01:41:40Z
- **Tasks:** 2
- **Files modified:** 3 (`scripts/pre-commit`, `scripts/install-git-hooks.ps1`, `CLAUDE.md`)

## Accomplishments

- Installed gitleaks 8.30.1 via `winget install --id Gitleaks.Gitleaks --exact`; confirmed official `Gitleaks.Gitleaks` package publisher resolving to the `gitleaks/gitleaks` GitHub project, installer hash verified by winget before extraction.
- Authored `scripts/pre-commit`: POSIX `sh` hook with a `command -v gitleaks` PATH guard (exits 0 with an install hint when absent — D-13), pipes `git diff --cached | gitleaks stdin` (current, non-deprecated invocation per RESEARCH Pitfall 1), exits 1 with a block message on any finding.
- Authored `scripts/install-git-hooks.ps1`: copies the tracked hook into `.git/hooks/pre-commit` (overwriting any existing file), sets the executable bit on the tracked source via `git update-index --chmod=+x` for portability to Linux/macOS clones, fails loudly (non-zero exit) if `.git/hooks` is missing.
- Ran the installer; `.git/hooks/pre-commit` is live and byte-identical to `scripts/pre-commit`, and confirmed untracked by git (as intended — D-11/D-12).
- Documented the one-time setup in `CLAUDE.md` under a new "Local setup (one-time)" section: hook install commands, and where the four secrets (`IGDB:ClientId`, `IGDB:ClientSecret`, `Steam:ApiKey`, `Jwt:Secret`) come from in Development (`dotnet user-secrets`, keyed off `appsettings.Development.json.example`) vs. other environments (`IGDB__ClientId`, `IGDB__ClientSecret`, `Steam__ApiKey`, `Jwt__Secret`).
- Exercised all three hook branches by execution (not assertion): soft-fail with a stripped PATH (exit 0), hard block with a staged secret-shaped value (exit 1, commit refused), and a clean commit of the plan's own three tracked files (exit 0, commit succeeded) — this last commit is the delivery of both tasks' work.

## Task Commits

1. **Task 1 + Task 2 (combined per plan's explicit instruction):** `7781881` (feat) — `scripts/pre-commit`, `scripts/install-git-hooks.ps1`, `CLAUDE.md`

The plan's Task 2 action text explicitly specifies that staging and committing the three tracked files IS the third verification check and simultaneously the delivery of Task 1's work ("that is simultaneously the third check and the delivery of Task 1's work"), so both tasks land in one commit rather than two separate task commits. The soft-fail and blocking-branch checks that precede it in Task 2 do not produce commits (the blocking-branch probe commit was refused, and its file was deleted before the delivery commit).

**Plan metadata:** (final metadata commit follows this SUMMARY)

## Files Created/Modified

- `scripts/pre-commit` - POSIX sh pre-commit hook: gitleaks-on-staged-diff with soft-fail-open on missing binary
- `scripts/install-git-hooks.ps1` - PowerShell installer copying the tracked hook into `.git/hooks/`
- `CLAUDE.md` - New "Local setup (one-time)" section (hook install + secret sourcing)

## Decisions Made

- Installed gitleaks via winget with the official `Gitleaks.Gitleaks` package id, per D-10 and the plan's provenance-check requirement — no loose executable from a mirror.
- `install-git-hooks.ps1` runs `git add scripts/pre-commit` before `git update-index --chmod=+x scripts/pre-commit`. The chmod call operates on the git index and requires the path to already be tracked/staged; on a normal fresh clone the file is already tracked so this is a no-op, but on this repo's very first authoring run (before any commit exists) the file was untracked, and the bare chmod call failed with exit 128. Staging first makes the installer work correctly in both the fresh-clone case and the first-time-authoring case. [Rule 1 - Bug]
- Also fixed a related PowerShell issue: `$ErrorActionPreference = "Stop"` was turning git's routine autocrlf stderr warnings into terminating errors for native command calls. Scoped `$ErrorActionPreference = "Continue"` around the git calls and check `$LASTEXITCODE` explicitly instead. [Rule 1 - Bug]
- Deviated from the plan's literal probe recipe (AWS-shaped `AKIA` + 16 random chars) after verifying it does not trigger any finding under the installed gitleaks 8.30.1 default ruleset on this machine — see "Deviations from Plan" below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `git update-index --chmod=+x` failed on first-time authoring run**
- **Found during:** Task 1 (running the installer for the first time)
- **Issue:** `scripts/pre-commit` was newly authored and not yet in the git index; `git update-index --chmod=+x` requires an existing index entry and exited 128.
- **Fix:** Added `git add scripts/pre-commit` immediately before the chmod call in `install-git-hooks.ps1`, scoped to that single path only (never a broad `git add`).
- **Files modified:** `scripts/install-git-hooks.ps1`
- **Verification:** Re-ran the installer; it completed with exit 0 and the confirmation message.
- **Committed in:** `7781881` (Task 1+2 delivery commit)

**2. [Rule 1 - Bug] PowerShell `$ErrorActionPreference = "Stop"` turned git's stderr warnings into terminating errors**
- **Found during:** Task 1 (first installer run)
- **Issue:** `git update-index` emitted a routine `LF will be replaced by CRLF` warning to stderr; under `$ErrorActionPreference = "Stop"`, PowerShell surfaced this as a script-terminating `NativeCommandError`, causing the installer to exit 1 even though git itself would have succeeded.
- **Fix:** Scoped `$ErrorActionPreference = "Continue"` around the native git calls and check `$LASTEXITCODE` explicitly for genuine failures, restoring the prior preference afterward.
- **Files modified:** `scripts/install-git-hooks.ps1`
- **Verification:** Re-ran the installer; completed successfully with the expected confirmation output.
- **Committed in:** `7781881` (Task 1+2 delivery commit)

**3. [Rule 1 - Bug] Plan's suggested AWS-shaped secret probe does not trigger a gitleaks finding on this machine**
- **Found during:** Task 2 (blocking-branch check)
- **Issue:** The plan specified generating `AKIA` + 16 random uppercase letters/digits to deterministically match gitleaks' default `aws-access-token` rule. Direct testing with `gitleaks detect` (isolated from the git-diff pipeline, to rule out hook-specific issues) showed this format, the canonical AWS documentation example key (`AKIAIOSFODNN7EXAMPLE`), a GitHub PAT prefix (`ghp_...`), and a full RSA private key PEM header (`-----BEGIN RSA PRIVATE KEY-----`) all scan clean under gitleaks 8.30.1's embedded default ruleset on this Windows machine — zero leaks found for any of them, confirmed at debug log level that the default config was in fact loaded (`using default gitleaks config`). A custom minimal `.toml` config with an equivalent literal-match rule DID detect the same RSA header correctly, confirming the gitleaks engine and CLI plumbing both function; the gap is specifically in which default rules fire. A JSON-style `"api_key": "<32-char random alnum>"` value, matching the shape already proven to fire against this repo's real (now-rotated) leaked `ClientSecret` in `appsettings.Development.json`, reliably triggers the `generic-api-key` rule (confirmed via multiple runs).
- **Fix:** Used the `generic-api-key`-shaped probe (`{"api_key": "<32-char random>"}`) instead of the plan's `AKIA`-prefixed probe for the blocking-branch check. The hook's behavior itself (block on any gitleaks finding, exit 1) is unchanged — this only changes which secret shape was used to prove the block, since the originally-specified shape does not reliably prove it on the currently-installed gitleaks version/build.
- **Files modified:** None (probe file was ephemeral, created and deleted during verification, never committed)
- **Verification:** `git commit` with the generic-api-key-shaped probe staged exited 1 with the hook's block message; probe file then unstaged and deleted, confirmed via `git status --porcelain`.
- **Committed in:** N/A (no commit — this is the proof that commits ARE blocked)

---

**Total deviations:** 3 auto-fixed (2 blocking, 1 bug/plan-assumption correction)
**Impact on plan:** All three deviations were necessary to complete the plan's own verification requirements on this machine. No scope creep — the hook's actual behavior (block/soft-fail/pass) matches the plan's design exactly; only the specific probe secret shape used to prove the block branch differed from what the plan suggested. This is flagged in `key-decisions` above for future attention: if the AWS-specific default rule is expected to matter for this project's real usage, it should be re-verified against a future gitleaks version, since it did not fire on 8.30.1 for several standard AWS-key-shaped test inputs.

## Issues Encountered

None beyond the three items documented above under Deviations from Plan.

## User Setup Required

None - no external service configuration required. The hook is already installed locally (`.git/hooks/pre-commit`); a fresh clone would need `winget install --id Gitleaks.Gitleaks --exact` followed by `scripts/install-git-hooks.ps1`, both now documented in `CLAUDE.md`.

## Next Phase Readiness

Phase 2 (Credential & Secrets Hardening) is now fully delivered: credentials moved out of URL query strings (02-01/02-02 lineage), secrets rotated (02-04), and this final gate (02-05) blocks any future secret from being committed. No blockers for closing out the phase.

---
*Phase: 02-credential-secrets-hardening*
*Completed: 2026-08-31*

## Self-Check: PASSED

- FOUND: scripts/pre-commit
- FOUND: scripts/install-git-hooks.ps1
- FOUND: .planning/phases/02-credential-secrets-hardening/02-05-SUMMARY.md
- FOUND: commit 7781881
