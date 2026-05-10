@echo off
setlocal EnableExtensions

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\..") do set "ROOT=%%~fI"
set "EXT_DIR=%ROOT%\vscode-extension"
set "SETTINGS=%APPDATA%\Code\User\settings.json"
set "MODE=%~1"
if "%MODE%"=="" set "MODE=full"
set "JSON=0"
if /i "%~2"=="-json" set "JSON=1"

set "SMOKEGATE_STATUS=SKIP"
set "NPM_STATUS=SKIP"
set "OVERALL=PASS"
set "FAIL_REASON="
set "RUNTIME_DIR=%ROOT%\.agent-runtime"
set "LOCKFILE=%RUNTIME_DIR%\doctor.lock"

if not exist "%RUNTIME_DIR%" mkdir "%RUNTIME_DIR%" >nul 2>nul
if exist "%LOCKFILE%" (
  echo [doctor] lock: FAIL ^(another doctor run is active: %LOCKFILE%^)
  set "FAIL_REASON=lock_conflict"
  set "OVERALL=FAIL"
  goto :END
)
(
  echo pid=%PROCESS_ID%
  echo time=%DATE% %TIME%
  echo mode=%MODE%
) > "%LOCKFILE%"

echo [doctor] Root: "%ROOT%"

if exist "%ROOT%\LocalCursorAgent.csproj" (
  echo [doctor] csproj: PASS
) else (
  echo [doctor] csproj: FAIL ^(LocalCursorAgent.csproj not found^)
  set "OVERALL=FAIL"
  goto :END
)

if exist "%SETTINGS%" (
  echo [doctor] settings.json: PASS
) else (
  echo [doctor] settings.json: FAIL ^(%SETTINGS% not found^)
  set "OVERALL=FAIL"
  goto :END
)

powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
  "$p='%SETTINGS%'; $j = Get-Content -LiteralPath $p -Raw | ConvertFrom-Json; if($j.'localCursorAgent.backendProjectPath'){exit 0}else{exit 2}"
if errorlevel 1 (
  echo [doctor] backendProjectPath: FAIL
  set "OVERALL=FAIL"
  goto :END
) else (
  echo [doctor] backendProjectPath: PASS
)

powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
  "$p='%SETTINGS%'; $j = Get-Content -LiteralPath $p -Raw | ConvertFrom-Json; if($j.'localCursorAgent.targetWorkspacePath'){exit 0}else{exit 2}"
if errorlevel 1 (
  echo [doctor] targetWorkspacePath: FAIL
  set "OVERALL=FAIL"
  goto :END
) else (
  echo [doctor] targetWorkspacePath: PASS
)

echo [doctor] newest VSIX files:
powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command ^
  "$files=Get-ChildItem -Path '%ROOT%' -Filter 'local-cursor-agent-*.vsix' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 2; if(-not $files){'  (none)'} else {$files | ForEach-Object {'  ' + $_.Name}}"

if /i "%MODE%"=="quick" goto :NPM

echo.
echo [doctor] Running SmokeGate...
call "%SCRIPT_DIR%SmokeGate.cmd"
if errorlevel 1 (
  set "SMOKEGATE_STATUS=FAIL"
  echo [doctor] SmokeGate: FAIL
  set "OVERALL=FAIL"
  goto :END
) else (
  set "SMOKEGATE_STATUS=PASS"
  echo [doctor] SmokeGate: PASS
)

:NPM
echo.
echo [doctor] Running vscode-extension npm test...
if not exist "%EXT_DIR%\package.json" (
  echo [doctor] npm test: FAIL ^(package.json missing in vscode-extension^)
  set "NPM_STATUS=FAIL"
  set "OVERALL=FAIL"
  goto :END
) else (
  pushd "%EXT_DIR%" >nul
  call npm test
  if errorlevel 1 (
    set "NPM_STATUS=FAIL"
    echo [doctor] npm test: FAIL
    popd >nul
    set "OVERALL=FAIL"
    goto :END
  ) else (
    set "NPM_STATUS=PASS"
    echo [doctor] npm test: PASS
  )
  popd >nul
)

:END
if exist "%LOCKFILE%" del /f /q "%LOCKFILE%" >nul 2>nul
echo.
echo [doctor] OVERALL: %OVERALL%
if "%JSON%"=="1" (
  echo {"overall":"%OVERALL%","mode":"%MODE%","smokegate":"%SMOKEGATE_STATUS%","npm":"%NPM_STATUS%","reason":"%FAIL_REASON%"}
)
if /i "%OVERALL%"=="PASS" (
  exit /b 0
)
exit /b 1
