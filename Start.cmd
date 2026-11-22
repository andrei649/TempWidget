@echo off
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%dist\TempWidget.exe"
set "MARKER=%ROOT%dist\.built-commit"

set "NEED_BUILD=0"
if /I "%~1"=="--rebuild" (
  set "NEED_BUILD=1"
  shift
)
if not exist "%EXE%" set "NEED_BUILD=1"

set "HEAD_SHA="
for /f "usebackq delims=" %%H in (`git -C "%ROOT%." rev-parse HEAD 2^>nul`) do set "HEAD_SHA=%%H"
set "BUILT_SHA="
if exist "%MARKER%" set /p BUILT_SHA=<"%MARKER%"
if defined HEAD_SHA if /I not "%HEAD_SHA%"=="%BUILT_SHA%" set "NEED_BUILD=1"

if "%NEED_BUILD%"=="1" (
  echo [TempWidget] Building latest local version into dist\ ...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%build-local.ps1"
  if errorlevel 1 (
    echo.
    echo [TempWidget] Build failed. Press any key to close.
    pause >nul
    exit /b 1
  )
)

start "" /D "%ROOT%dist" "%EXE%" %1 %2 %3 %4 %5 %6 %7 %8 %9
exit /b 0
