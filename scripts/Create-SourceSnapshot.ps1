param(
    [string]$OutputDirectory = ".\release",
    [string]$ArchivePrefix = "LocalCursorAgent-source",
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $repoRoot ".git"))) {
    throw "Run this script inside a git working tree."
}

git -C $repoRoot rev-parse --verify HEAD | Out-Null

$dirtyStatus = git -C $repoRoot status --porcelain
if ($dirtyStatus -and -not $AllowDirty) {
    throw "Working tree has uncommitted changes. Commit or stash them before creating a source snapshot, or pass -AllowDirty."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$archivePath = Join-Path $OutputDirectory "$ArchivePrefix-$timestamp.zip"
if (Test-Path $archivePath) { Remove-Item -LiteralPath $archivePath -Force }

# Active LocalCursorAgent scope to include by default.
$includeDirs = @(
    "Core",
    "Context",
    "Indexing",
    "SafetyTests",
    "scripts",
    "vscode-extension"
)

$includeRootPatterns = @(
    "*.sln",
    "*.csproj",
    "*.props",
    "*.targets",
    "*.json",
    "*.md",
    "*.txt",
    "*.config",
    ".editorconfig",
    ".gitattributes",
    ".gitignore",
    "LICENSE*",
    "README*"
)

$excludeDirSegments = @(
    "desktop-app",
    "Desktop",
    ".git",
    ".vs",
    "bin",
    "obj",
    "node_modules",
    "release",
    ".agent-runtime",
    "logs"
)

$excludeFilePatterns = @(
    "*.vsix",
    "*.exe",
    "*.msi",
    "*.appx",
    "*.nupkg",
    "*.tmp",
    "*.cache"
)

function Test-ExcludedPath([string]$fullPath, [bool]$isDirectory) {
    $relative = Get-RepoRelativePath $fullPath
    $segments = $relative.Split("/")
    foreach ($segment in $segments) {
        if ($excludeDirSegments -contains $segment) { return $true }
    }
    if (-not $isDirectory) {
        $name = [System.IO.Path]::GetFileName($fullPath)
        foreach ($pattern in $excludeFilePatterns) {
            if ($name -like $pattern) { return $true }
        }
    }
    return $false
}

function Get-RepoRelativePath([string]$fullPath) {
    $root = [System.IO.Path]::GetFullPath($repoRoot)
    if (-not $root.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $root += [System.IO.Path]::DirectorySeparatorChar
    }
    $rootUri = [System.Uri]$root
    $fileUri = [System.Uri]([System.IO.Path]::GetFullPath($fullPath))
    $relativeUri = $rootUri.MakeRelativeUri($fileUri)
    $relative = [System.Uri]::UnescapeDataString($relativeUri.ToString())
    return $relative.Replace("\", "/")
}

$fileSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($dir in $includeDirs) {
    $abs = Join-Path $repoRoot $dir
    if (-not (Test-Path $abs)) { continue }
    Get-ChildItem -LiteralPath $abs -Recurse -File | ForEach-Object {
        if (-not (Test-ExcludedPath $_.FullName $false)) {
            [void]$fileSet.Add($_.FullName)
        }
    }
}

Get-ChildItem -LiteralPath $repoRoot -File | ForEach-Object {
    $name = $_.Name
    $match = $false
    foreach ($pattern in $includeRootPatterns) {
        if ($name -like $pattern) { $match = $true; break }
    }
    if ($match -and -not (Test-ExcludedPath $_.FullName $false)) {
        [void]$fileSet.Add($_.FullName)
    }
}

$files = @($fileSet) | Sort-Object
if ($files.Count -eq 0) {
    throw "No files selected for snapshot."
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $relative = Get-RepoRelativePath $file
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

if (-not (Test-Path $archivePath)) {
    throw "Archive creation failed: $archivePath"
}

$archiveItem = Get-Item -LiteralPath $archivePath
$sizeMb = [Math]::Round($archiveItem.Length / 1MB, 2)

Write-Host "Created source snapshot: $archivePath"
Write-Host "Archive size: $sizeMb MB"
