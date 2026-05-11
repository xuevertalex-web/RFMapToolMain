@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\..") do set "ROOT=%%~fI\"
set "EXT_DIR=%ROOT%vscode-extension"
set "PKG_JSON=%EXT_DIR%\package.json"

if not exist "%PKG_JSON%" (
  echo [update] package.json not found: "%PKG_JSON%"
  exit /b 1
)

echo [update] Bumping patch version in vscode-extension\package.json...
set "BUMP_SCRIPT=%SCRIPT_DIR%Update-VSCodeExtension.ps1"
if not exist "%BUMP_SCRIPT%" (
  echo [update] bump script not found: "%BUMP_SCRIPT%"
  exit /b 1
)

echo [update] Unblocking local scripts/VSIX files to avoid security prompts...
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
  "$root='%ROOT%';" ^
  "Get-ChildItem -Path $root -Recurse -File -Include *.cmd,*.ps1,*.vsix | ForEach-Object { Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue }" >nul 2>nul

for /f "usebackq delims=" %%V in (`powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%BUMP_SCRIPT%" -PackageJson "%PKG_JSON%"`) do (
  set "NEW_VERSION=%%V"
)

if not defined NEW_VERSION (
  echo [update] Failed to bump version.
  exit /b 1
)

echo [update] New version: !NEW_VERSION!

pushd "%EXT_DIR%" >nul

echo [update] Packaging VSIX...
call npx @vscode/vsce package --allow-missing-repository
if errorlevel 1 (
  popd >nul
  echo [update] VSIX packaging failed.
  exit /b 1
)

set "VSIX_FILE=local-cursor-agent-!NEW_VERSION!.vsix"
if not exist "%VSIX_FILE%" (
  popd >nul
  echo [update] VSIX file not found: "%VSIX_FILE%"
  exit /b 1
)

echo [update] Installing extension into VS Code...
call code --install-extension "%CD%\%VSIX_FILE%" --force
if errorlevel 1 (
  popd >nul
  echo [update] VSIX install failed.
  exit /b 1
)

copy /Y "%VSIX_FILE%" "%ROOT%%VSIX_FILE%" >nul
popd >nul

echo [update] Keeping only the 2 newest root VSIX files...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$root='%ROOT%';" ^
  "$files=Get-ChildItem -Path $root -Filter 'local-cursor-agent-*.vsix' -File | Sort-Object LastWriteTime -Descending;" ^
  "if($files.Count -gt 2){$files | Select-Object -Skip 2 | Remove-Item -Force}"

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

echo [update] Done. Installed version !NEW_VERSION!.
echo [update] If UI still shows old state, run: Developer: Reload Window
exit /b 0
