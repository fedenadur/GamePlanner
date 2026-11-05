# Rebuilds the throwaway GamePlannerDB_Test database from the numbered Database/*.sql scripts (D-18).
# Refuses any database name that does not end in _Test. Never touches the live GamePlannerDB.
param(
    [string]$Server = 'localhost',
    [string]$Database = 'GamePlannerDB_Test'
)

$ErrorActionPreference = 'Stop'

if ($Database -notmatch '_Test$') {
    throw 'Refusing to build: database name must end in _Test'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$dbDir = Join-Path $repoRoot 'Database'

function Invoke-Sql {
    param([string]$Db, [string]$File)
    & sqlcmd -S $Server -E -C -b -I -d $Db -i $File
    if ($LASTEXITCODE -ne 0) { throw "Script $([IO.Path]::GetFileName($File)) failed" }
}

# Drop only the _Test database, if present.
$dropSql = "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END"
& sqlcmd -S $Server -E -C -b -d master -Q $dropSql
if ($LASTEXITCODE -ne 0) { throw "Dropping $Database failed" }

# Ordinal sort so 17_ precedes 17b_.
$names = [string[]]@(Get-ChildItem -Path $dbDir -Filter '*.sql' | Where-Object { $_.Name -match '^\d' } | ForEach-Object { $_.Name })
[Array]::Sort($names, [StringComparer]::Ordinal)

foreach ($name in $names) {
    $raw = Get-Content -Path (Join-Path $dbDir $name) -Raw -Encoding UTF8
    $sql = [regex]::Replace($raw, '\bGamePlannerDB\b', $Database)
    $temp = Join-Path $env:TEMP "gp-testdb-$name"
    # UTF-8 with BOM so sqlcmd reads non-ASCII text correctly.
    [IO.File]::WriteAllText($temp, $sql, (New-Object System.Text.UTF8Encoding($true)))
    $db = if ($name -like '01_*') { 'master' } else { $Database }
    try {
        Invoke-Sql -Db $db -File $temp
    }
    finally {
        Remove-Item -Path $temp -ErrorAction SilentlyContinue
    }
}

# CONVENTION EXEMPTION (owner decision D-23): this is a test-DB-only write that runs after the _Test guard above.
# It deactivates the 04_SeedData sample games so they never enter a manager scan.
& sqlcmd -S $Server -E -C -b -I -d $Database -Q 'UPDATE Games SET IsActive = 0;'
if ($LASTEXITCODE -ne 0) { throw 'Deactivating seed games failed' }

Write-Output "TEST_DB_READY=$Database"
