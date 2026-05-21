@echo off
dotnet --info >nul 2>nul
if errorlevel 1 (
  echo Doctor-Quick FAIL: dotnet missing
  exit /b 1
)
echo Doctor-Quick PASS
exit /b 0
