@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\..") do set "ROOT=%%~fI\"
set "EXT_DIR=%ROOT%vscode-extension"
set "PKG_JSON=%EXT_DIR%\package.json"
set "BUMP_SCRIPT=%SCRIPT_DIR%Update-VSCodeExtension.ps1"

if not exist "%PKG_JSON%" (
  echo [update] package.json not found: "%PKG_JSON%"
  exit /b 1
)

if not exist "%BUMP_SCRIPT%" (
  echo [update] bump script not found: "%BUMP_SCRIPT%"
  exit /b 1
)

where powershell >nul 2>nul
if errorlevel 1 (
  echo [update] powershell was not found in PATH.
  exit /b 1
)

where code >nul 2>nul
if errorlevel 1 (
  echo [update] VS Code CLI 'code' was not found in PATH.
  echo [update] In VS Code, run: Shell Command: Install 'code' command in PATH
  exit /b 1
)

where npx >nul 2>nul
if errorlevel 1 (
  echo [update] npx was not found in PATH. Install Node.js/npm first.
  exit /b 1
)

echo [update] Unblocking only required update scripts...
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
  "$targets = @('%~f0','%BUMP_SCRIPT%');" ^
  "foreach ($t in $targets) { if (Test-Path -LiteralPath $t) { Unblock-File -LiteralPath $t -ErrorAction Stop } }" >nul 2>nul
if errorlevel 1 (
  echo [update] Failed while unblocking required update scripts.
  exit /b 1
)

echo [update] Removing stale VSIX files before packaging...
del /q "%EXT_DIR%\local-cursor-agent-*.vsix" >nul 2>nul
del /q "%ROOT%local-cursor-agent-*.vsix" >nul 2>nul

echo [update] Bumping patch version in vscode-extension\package.json...
for /f "usebackq delims=" %%V in (`powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%BUMP_SCRIPT%" -PackageJson "%PKG_JSON%"`) do (
  set "NEW_VERSION=%%V"
)

if not defined NEW_VERSION (
  echo [update] Failed to bump version.
  exit /b 1
)

echo [update] New package version: !NEW_VERSION!

pushd "%EXT_DIR%" >nul
if errorlevel 1 (
  echo [update] Failed to enter extension directory: "%EXT_DIR%"
  exit /b 1
)

echo [update] Running extension tests...
call npm test
if errorlevel 1 (
  popd >nul
  echo [update] Extension tests failed.
  exit /b 1
)

echo [update] Packaging fresh VSIX...
call npx @vscode/vsce package --allow-missing-repository
if errorlevel 1 (
  popd >nul
  echo [update] VSIX packaging failed.
  exit /b 1
)

set "VSIX_FILE=local-cursor-agent-!NEW_VERSION!.vsix"
if not exist "%VSIX_FILE%" (
  popd >nul
  echo [update] Expected VSIX file not found: "%VSIX_FILE%"
  exit /b 1
)

for /f %%C in ('dir /b "local-cursor-agent-*.vsix" 2^>nul ^| find /c /v ""') do set "VSIX_COUNT=%%C"
if not "!VSIX_COUNT!"=="1" (
  popd >nul
  echo [update] Expected exactly one freshly packaged VSIX, found !VSIX_COUNT!.
  exit /b 1
)

echo [update] Unblocking freshly packaged VSIX only...
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
  "$vsix = Join-Path '%CD%' '%VSIX_FILE%';" ^
  "if (-not (Test-Path -LiteralPath $vsix)) { throw 'Packaged VSIX not found for unblocking.' };" ^
  "Unblock-File -LiteralPath $vsix -ErrorAction Stop" >nul 2>nul
if errorlevel 1 (
  popd >nul
  echo [update] Failed while unblocking packaged VSIX.
  exit /b 1
)

echo [update] Installing fresh VSIX into VS Code...
call code --install-extension "%CD%\%VSIX_FILE%" --force
if errorlevel 1 (
  popd >nul
  echo [update] VSIX install failed.
  exit /b 1
)

copy /Y "%VSIX_FILE%" "%ROOT%%VSIX_FILE%" >nul
if errorlevel 1 (
  popd >nul
  echo [update] Failed to copy VSIX to repo root.
  exit /b 1
)

popd >nul

echo [update] Verifying installed extension files match workspace sources...
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
  "$extRoot = Join-Path $env:USERPROFILE '.vscode\extensions';" ^
  "$installed = Get-ChildItem -LiteralPath $extRoot -Directory -Filter 'local.local-cursor-agent-*' | Sort-Object LastWriteTime -Descending | Select-Object -First 1;" ^
  "if (-not $installed) { throw 'Installed extension folder not found under .vscode\extensions' };" ^
  "$srcRoot = [System.IO.Path]::GetFullPath('%EXT_DIR%');" ^
  "$targets = @('commandHandlers.js','panelRunController.js','workspaceTaskClassifier.js','workspaceGuard.test.js','package.json');" ^
  "foreach ($rel in $targets) {" ^
  "  $src = Join-Path $srcRoot $rel;" ^
  "  $dst = Join-Path $installed.FullName $rel;" ^
  "  if (-not (Test-Path -LiteralPath $src)) { throw \"Source file missing: $src\" };" ^
  "  if (-not (Test-Path -LiteralPath $dst)) { throw \"Installed file missing: $dst\" };" ^
  "  $h1 = (Get-FileHash -Algorithm SHA256 -LiteralPath $src).Hash;" ^
  "  $h2 = (Get-FileHash -Algorithm SHA256 -LiteralPath $dst).Hash;" ^
  "  if ($h1 -ne $h2) { throw \"Hash mismatch for $rel`n  src=$src`n  dst=$dst\" };" ^
  "};" ^
  "Write-Output ('[update] Sync verified against installed folder: ' + $installed.FullName)"
if errorlevel 1 (
  echo [update] Installed extension does not match current workspace sources.
  exit /b 1
)

echo [update] Configuring VS Code backendProjectPath for any-workspace runs...
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
  "$settingsPath = Join-Path $env:APPDATA 'Code\User\settings.json';" ^
  "$projectPath = [System.IO.Path]::GetFullPath('%ROOT%LocalCursorAgent.csproj');" ^
  "$targetWorkspacePath = [System.IO.Path]::GetFullPath('%ROOT%');" ^
  "if (-not (Test-Path -LiteralPath $settingsPath)) { '{}' | Set-Content -LiteralPath $settingsPath -Encoding UTF8 };" ^
  "$raw = Get-Content -LiteralPath $settingsPath -Raw;" ^
  "if ([string]::IsNullOrWhiteSpace($raw)) { $raw = '{}' };" ^
  "try { $obj = $raw | ConvertFrom-Json } catch { $obj = New-Object psobject };" ^
  "if ($null -eq $obj) { $obj = New-Object psobject };" ^
  "$existing = $obj.PSObject.Properties['localCursorAgent.backendProjectPath'];" ^
  "if ($existing) { $existing.Value = $projectPath } else { $obj | Add-Member -NotePropertyName 'localCursorAgent.backendProjectPath' -NotePropertyValue $projectPath };" ^
  "$existingTarget = $obj.PSObject.Properties['localCursorAgent.targetWorkspacePath'];" ^
  "if ($existingTarget) { $existingTarget.Value = $targetWorkspacePath } else { $obj | Add-Member -NotePropertyName 'localCursorAgent.targetWorkspacePath' -NotePropertyValue $targetWorkspacePath };" ^
  "$obj | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsPath -Encoding UTF8"
if errorlevel 1 (
  echo [update] Failed to update VS Code user settings.
  exit /b 1
)

echo [update] Installed version !NEW_VERSION! from fresh package %VSIX_FILE%.
echo [update] Attempting to reload VS Code window...
call code --reuse-window --command workbench.action.reloadWindow >nul 2>nul
if errorlevel 1 (
  echo [update] Auto-reload command is not available in this code CLI build.
  echo [update] Run manually in VS Code: Developer: Reload Window
) else (
  echo [update] Reload command sent successfully.
)
exit /b 0
