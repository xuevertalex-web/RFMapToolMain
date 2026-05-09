@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "TARGET=%SCRIPT_DIR%scripts\devtools\Start-VSCodeAgent.cmd"
if not exist "%TARGET%" (
  echo [ERROR] Missing target script: %TARGET%
  exit /b 1
)
call "%TARGET%" %*
exit /b %ERRORLEVEL%
