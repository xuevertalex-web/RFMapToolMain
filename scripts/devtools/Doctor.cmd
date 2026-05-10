@echo off
set "SCRIPT_DIR=%~dp0"
call "%SCRIPT_DIR%Doctor.Common.cmd" full %*
exit /b %errorlevel%

