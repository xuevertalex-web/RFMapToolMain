param(
    [Parameter(Mandatory = $true)]
    [string]$ArchivePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ArchivePath)) {
    throw "Archive not found: $ArchivePath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $ArchivePath))
try {
    $entries = $zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
    $summary = [ordered]@{
        total_entries = $entries.Count
        contains_git = ($entries | Where-Object { $_ -like ".git/*" }).Count
        contains_runtime = ($entries | Where-Object { $_ -like ".agent-runtime/*" -or $_ -like "agent-runtime/*" }).Count
        contains_bin = ($entries | Where-Object { $_ -like "bin/*" }).Count
        contains_obj = ($entries | Where-Object { $_ -like "obj/*" }).Count
        contains_vs = ($entries | Where-Object { $_ -like ".vs/*" }).Count
        contains_vsix = ($entries | Where-Object { $_ -like "*.vsix" }).Count
        contains_local_secrets = ($entries | Where-Object { $_ -like "localcursoragent.secrets.json" -or $_ -like "*/localcursoragent.secrets.json" }).Count
    }

    $summary | ConvertTo-Json -Depth 4
}
finally {
    $zip.Dispose()
}
