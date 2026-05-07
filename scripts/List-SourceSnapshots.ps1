param(
    [string]$OutputDirectory = ".\release",
    [string]$ArchivePrefix = "LocalCursorAgent-source"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutputDirectory)) {
    Write-Output "[]"
    exit 0
}

$items = Get-ChildItem -Path $OutputDirectory -Filter "$ArchivePrefix-*.zip" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object @{Name="name";Expression={$_.Name}}, @{Name="size_bytes";Expression={$_.Length}}, @{Name="last_write_utc";Expression={$_.LastWriteTimeUtc.ToString("o")}}

$items | ConvertTo-Json -Depth 4
