param(
    [string]$OutputDirectory = ".\release",
    [string]$ArchivePrefix = "LocalCursorAgent-source"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $repoRoot ".git"))) {
    throw "Run this script inside a git working tree."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$archivePath = Join-Path $OutputDirectory "$ArchivePrefix-$timestamp.zip"

git -C $repoRoot archive --format=zip --output=$archivePath HEAD

Write-Host "Created source-only snapshot: $archivePath"
