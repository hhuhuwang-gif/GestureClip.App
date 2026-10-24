@echo off
rem ============================================================================
rem  GestureClip launcher
rem
rem  Default            build the release package if it is stale, then start it
rem  --quick / -q       use the incremental Debug build instead (fast, for UI work)
rem  --rebuild          force a build even when the current build looks fresh
rem  --no-check         skip the staleness check and start whatever exists
rem  Any other argument is forwarded to GestureClip.exe (e.g. --trace-scroll)
rem
rem  Console output stays ASCII so cmd.exe renders it correctly on any code page.
rem ============================================================================
setlocal EnableExtensions

set "REPO_ROOT=%~dp0"
set "RELEASE_EXE=%REPO_ROOT%artifacts\release\GestureClip\GestureClip.exe"
set "DEBUG_EXE=%REPO_ROOT%src\GestureClip.App\bin\Debug\net8.0-windows10.0.19041.0\GestureClip.exe"
set "PROJECT_FILE=%REPO_ROOT%src\GestureClip.App\GestureClip.App.csproj"
set "PUBLISH_SCRIPT=%REPO_ROOT%scripts\publish-win-x64.ps1"
set "QUICK_SCRIPT=%REPO_ROOT%scripts\build-debug-quick.ps1"
set "FRESHNESS_SCRIPT=%REPO_ROOT%scripts\check-build-freshness.ps1"

set "TARGET=release"
set "FORCE_BUILD=0"
set "SKIP_CHECK=0"
set "APP_ARGS="

:parse_args
if "%~1"=="" goto args_done
if /i "%~1"=="--quick" goto opt_quick
if /i "%~1"=="-q" goto opt_quick
if /i "%~1"=="--release" goto opt_release
if /i "%~1"=="--rebuild" goto opt_rebuild
if /i "%~1"=="--no-check" goto opt_nocheck
if /i "%~1"=="--help" goto usage
if /i "%~1"=="-h" goto usage
goto opt_passthrough

:opt_quick
set "TARGET=quick"
shift
goto parse_args

:opt_release
set "TARGET=release"
shift
goto parse_args

:opt_rebuild
set "FORCE_BUILD=1"
shift
goto parse_args

:opt_nocheck
set "SKIP_CHECK=1"
shift
goto parse_args

:opt_passthrough
set "APP_ARGS=%APP_ARGS% "%~1""
shift
goto parse_args

:args_done
if /i "%TARGET%"=="quick" goto select_quick
set "APP_EXE=%RELEASE_EXE%"
goto check_state

:select_quick
set "APP_EXE=%DEBUG_EXE%"

:check_state
if "%SKIP_CHECK%"=="1" goto launch
if "%FORCE_BUILD%"=="1" goto build_by_target
if not exist "%APP_EXE%" (
    echo [INFO] No build found at "%APP_EXE%".
    echo [INFO] Building it now, this can take a few minutes...
    goto build_by_target
)

set "STATE=UNKNOWN"
if exist "%FRESHNESS_SCRIPT%" (
    for /f "usebackq delims=" %%A in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%FRESHNESS_SCRIPT%" -Exe "%APP_EXE%" -SourceRoot "%REPO_ROOT%src"`) do set "STATE=%%A"
)

if /i "%STATE%"=="FRESH" goto launch
if /i not "%STATE%"=="STALE" goto launch

echo.
echo [WARN] Source files are newer than the build you are about to start.
echo        Starting it now would run code WITHOUT your latest changes.
echo.
echo   [1] Quick Debug build and start  (seconds, recommended for UI checks)
echo   [2] Full release rebuild         (runs all tests, a few minutes)
echo   [3] Start the outdated build anyway
echo.
choice /c 123 /n /t 12 /d 1 /m "Select [1-3], default 1 in 12s: "
if errorlevel 3 goto launch
if errorlevel 2 goto build_release
goto build_quick

:build_by_target
if /i "%TARGET%"=="quick" goto build_quick
goto build_release

:build_quick
if not exist "%QUICK_SCRIPT%" (
    echo [ERROR] Missing build script: "%QUICK_SCRIPT%"
    goto fail
)
echo.
echo [INFO] Incremental Debug build...
powershell -NoProfile -ExecutionPolicy Bypass -File "%QUICK_SCRIPT%"
if errorlevel 1 goto fail
set "APP_EXE=%DEBUG_EXE%"
goto launch

:build_release
if not exist "%PUBLISH_SCRIPT%" (
    echo [ERROR] Missing publish script: "%PUBLISH_SCRIPT%"
    goto fail
)
echo.
echo [INFO] Full release rebuild: restore + tests + publish, this takes a few minutes...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PUBLISH_SCRIPT%"
if errorlevel 1 goto fail
set "APP_EXE=%RELEASE_EXE%"
goto launch

:launch
if not exist "%APP_EXE%" goto fail

set "APP_VERSION=unknown"
for /f "tokens=3 delims=<>" %%V in ('findstr /r /c:"<Version>" "%PROJECT_FILE%" 2^>nul') do set "APP_VERSION=%%V"

echo.
echo [INFO] Build   : %APP_EXE%
echo [INFO] Version : %APP_VERSION%
echo [INFO] Args    : %APP_ARGS%
echo.
start "" "%APP_EXE%" %APP_ARGS%
exit /b 0

:fail
echo.
echo [FAILED] GestureClip could not be started.
pause
exit /b 1

:usage
echo Usage: Start-GestureClip.bat [--quick^|--release] [--rebuild] [--no-check] [app arguments]
echo.
echo   --quick, -q   incremental Debug build (fast path for UI iteration)
echo   --release     release package in artifacts\release (default)
echo   --rebuild     force a build even if the current one looks fresh
echo   --no-check    skip the source/build staleness check
echo.
echo   Everything else is forwarded to GestureClip.exe, e.g. --trace-scroll or --trace-ui
exit /b 0
