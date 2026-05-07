param(
    [string]$OutputDirectory = ".\release",
    [string]$ArchivePrefix = "LocalCursorAgent-source"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

$createScript = Join-Path $scriptRoot "Create-SourceSnapshot.ps1"
$verifyScript = Join-Path $scriptRoot "Verify-SourceSnapshot.ps1"

& $createScript -OutputDirectory $OutputDirectory -ArchivePrefix $ArchivePrefix

$latest = Get-ChildItem -Path $OutputDirectory -Filter "$ArchivePrefix-*.zip" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if (-not $latest) {
    throw "No snapshot archive found in $OutputDirectory."
}

& $verifyScript -ArchivePath $latest.FullName

Write-Host "Snapshot flow completed: $($latest.FullName)"
