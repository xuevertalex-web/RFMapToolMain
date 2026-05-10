@echo off
setlocal EnableExtensions
set "SCRIPT_DIR=%~dp0"
call "%SCRIPT_DIR%Doctor.Common.cmd" quick %*
exit /b %errorlevel%
