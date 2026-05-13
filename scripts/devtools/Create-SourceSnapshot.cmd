@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "SCRIPTS_DIR=%%~fI"
set "SNAPSHOT_SCRIPT=%SCRIPTS_DIR%\Create-SourceSnapshot.ps1"

if not exist "%SNAPSHOT_SCRIPT%" (
  echo [snapshot] Snapshot script not found: "%SNAPSHOT_SCRIPT%"
  exit /b 1
)

where powershell >nul 2>nul
if errorlevel 1 (
  echo [snapshot] powershell was not found in PATH.
  exit /b 1
)

powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%SNAPSHOT_SCRIPT%" %*
if errorlevel 1 (
  echo [snapshot] Source snapshot flow failed.
  exit /b 1
)

exit /b 0
