@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\..") do set "ROOT=%%~fI\"
set "CODE=%LOCALAPPDATA%\Programs\Microsoft VS Code\bin\code.cmd"
set "UPDATER=%SCRIPT_DIR%Update-VSCodeExtension.cmd"

if not exist "%CODE%" (
  echo VS Code launcher not found:
  echo %CODE%
  exit /b 1
)

if not exist "%UPDATER%" (
  echo VS Code extension updater not found:
  echo %UPDATER%
  exit /b 1
)

echo [start] Updating VS Code extension from local project...
call "%UPDATER%"
if errorlevel 1 (
  echo [start] Extension update failed. VS Code will not be started.
  exit /b 1
)

"%CODE%" "%ROOT%"
