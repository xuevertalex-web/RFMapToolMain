param(
  [Parameter(Mandatory = $true)]
  [string]$PackageJson
)

if (-not (Test-Path -LiteralPath $PackageJson)) {
  throw "package.json not found: $PackageJson"
}

$bytes = [System.IO.File]::ReadAllBytes($PackageJson)

$encoding = if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
  [System.Text.UTF8Encoding]::new($true)
}
elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
  [System.Text.Encoding]::Unicode
}
elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
  [System.Text.Encoding]::BigEndianUnicode
}
else {
  [System.Text.UTF8Encoding]::new($false)
}

$text = $encoding.GetString($bytes)
$match = [System.Text.RegularExpressions.Regex]::Match($text, '"version"\s*:\s*"(\d+)\.(\d+)\.(\d+)"')
if (-not $match.Success) {
  throw "version field not found or invalid semver in $PackageJson"
}

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value
$patch = [int]$match.Groups[3].Value + 1
$newVersion = "$major.$minor.$patch"

$updated = [System.Text.RegularExpressions.Regex]::Replace(
  $text,
  '"version"\s*:\s*"\d+\.\d+\.\d+"',
  "`"version`": `"$newVersion`"",
  1
)

[System.IO.File]::WriteAllText($PackageJson, $updated, $encoding)
Write-Output $newVersion

