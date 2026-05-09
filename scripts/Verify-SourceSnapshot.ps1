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
    $blockedPatterns = @(
        '.git/',
        '.agent-runtime/',
        'agent-runtime/',
        'bin/',
        'obj/',
        '.vs/',
        'TestResults/',
        'coverage/',
        'vscode-extension/*.vsix',
        '*.vsix',
        'localcursoragent.secrets.json',
        '*/localcursoragent.secrets.json'
    )

    $entries = $zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
    foreach ($pattern in $blockedPatterns) {
        if ($entries | Where-Object { $_ -like "$pattern*" }) {
            throw "Blocked path found in archive: $pattern"
        }
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Source snapshot verified: $ArchivePath"
