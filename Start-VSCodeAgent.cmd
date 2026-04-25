@echo off
setlocal

set "ROOT=%~dp0"
set "CODE=%LOCALAPPDATA%\Programs\Microsoft VS Code\bin\code.cmd"

if not exist "%CODE%" (
  echo VS Code launcher not found:
  echo %CODE%
  exit /b 1
)

"%CODE%" "%ROOT%"
