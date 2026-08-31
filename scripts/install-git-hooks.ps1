# scripts/install-git-hooks.ps1
# Installs the tracked git hooks from scripts/ into the local (untracked) .git/hooks directory.
# Git never tracks .git/hooks, so this script must be re-run after every fresh clone.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$hooksDir = Join-Path $repoRoot ".git\hooks"

if (-not (Test-Path $hooksDir)) {
    Write-Error "No .git\hooks directory found at '$hooksDir'. Run this script from inside a git repository clone."
    exit 1
}

$source = Join-Path $repoRoot "scripts\pre-commit"
$dest = Join-Path $hooksDir "pre-commit"

Copy-Item -Path $source -Destination $dest -Force

# Windows has no executable bit git honours, but set it on the tracked source anyway
# so a Linux/macOS clone gets an executable hook. Skip quietly if already set.
# git writes routine autocrlf warnings to stderr; under $ErrorActionPreference = "Stop"
# PowerShell treats that stderr output as a terminating error, so relax it just for
# these native calls and check $LASTEXITCODE instead.
Push-Location $repoRoot
try {
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"

    # git update-index --chmod requires the path to already exist in the index.
    # On a fresh checkout it always does; on first-time authoring (this file has
    # never been staged yet) it does not, so stage it first — a no-op on an
    # already-tracked, unmodified file.
    git add scripts/pre-commit 2>&1 | Out-Null

    git update-index --chmod=+x scripts/pre-commit 2>&1 | Out-Null
    $chmodExit = $LASTEXITCODE

    $ErrorActionPreference = $prevEap
    if ($chmodExit -ne 0) {
        Write-Error "git update-index --chmod=+x failed with exit code $chmodExit"
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host "Installed pre-commit hook to: $dest"
