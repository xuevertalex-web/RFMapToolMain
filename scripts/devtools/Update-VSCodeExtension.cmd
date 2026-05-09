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

for /f "usebackq delims=" %%V in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%BUMP_SCRIPT%" -PackageJson "%PKG_JSON%"`) do (
  set "NEW_VERSION=%%V"
)

if not defined NEW_VERSION (
  echo [update] Failed to bump version.
  exit /b 1
)

echo [update] New version: !NEW_VERSION!

pushd "%EXT_DIR%" >nul

echo [update] Packaging VSIX...
call npx @vscode/vsce package
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

echo [update] Done. Installed version !NEW_VERSION!.
echo [update] If UI still shows old state, run: Developer: Reload Window
exit /b 0
