@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..\..") do set "ROOT=%%~fI"

set "TEST1=PASS AnalysisFallback_ModelTimeout_IndexedContextSummary_StructuredObservability"
set "TEST2=PASS AnalysisFallback_LlmRequestFailed_IndexedContextSummary_StructuredObservability"
set "TEST3=PASS Analysis_NormalModelResponse_NoFallbackTimeline"

set "SEEN1=0"
set "SEEN2=0"
set "SEEN3=0"
set "HAS_FAIL=0"

echo [smoke-gate] dotnet build "%ROOT%\LocalCursorAgent.csproj"
dotnet build "%ROOT%\LocalCursorAgent.csproj"
if errorlevel 1 (
  echo [smoke-gate] FAILED: build step failed.
  exit /b 1
)

echo [smoke-gate] dotnet run --project "%ROOT%\SafetyTests\SafetyTests.csproj"
for /f "usebackq delims=" %%L in (`dotnet run --project "%ROOT%\SafetyTests\SafetyTests.csproj" 2^>^&1`) do (
  echo %%L
  if "%%L"=="!TEST1!" set "SEEN1=1"
  if "%%L"=="!TEST2!" set "SEEN2=1"
  if "%%L"=="!TEST3!" set "SEEN3=1"
  echo %%L | findstr /C:"FAIL " >nul && set "HAS_FAIL=1"
)

if not "!SEEN1!"=="1" (
  echo [smoke-gate] FAILED: missing baseline PASS: !TEST1!
  exit /b 1
)
if not "!SEEN2!"=="1" (
  echo [smoke-gate] FAILED: missing baseline PASS: !TEST2!
  exit /b 1
)
if not "!SEEN3!"=="1" (
  echo [smoke-gate] FAILED: missing baseline PASS: !TEST3!
  exit /b 1
)
if "!HAS_FAIL!"=="1" (
  echo [smoke-gate] FAILED: SafetyTests reported FAIL.
  exit /b 1
)

echo [smoke-gate] PASS
echo [guardrail] Do not add production tools to bypass external environment issues.
echo [guardrail] If rg fails in coding-agent environment, do not modify LocalCursorAgent production code.
echo [guardrail] Any new tool must be introduced only via a separate explicit task.
exit /b 0
