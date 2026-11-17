@echo off
rem ============================================================================
rem  GestureClip launcher - double-click and go.
rem
rem  No argument (just double-click):
rem      start the current Release build; if the sources are newer than the exe,
rem      rebuild incrementally first (seconds, silent) so you never test old code.
rem      No menu, no prompt, no waiting for input.
rem
rem  --quick / -q     Debug build instead (fastest path while tweaking UI)
rem  --packaged       run artifacts\release\GestureClip\GestureClip.exe (shipped copy)
rem  --rebuild        rebuild even when the exe already looks fresh
rem  --no-build       start whatever exists, skip the freshness check entirely
rem  --publish        full package build (restore + tests + publish, minutes)
rem  --ask            show the old 1=Debug 2=Release 3=start-anyway menu
rem  Any other argument is forwarded to GestureClip.exe (e.g. --trace-ui).
rem
rem  Console output stays ASCII so cmd.exe renders it on any code page.
rem ============================================================================
setlocal EnableExtensions

set "REPO_ROOT=%~dp0"
set "REL_EXE=%REPO_ROOT%src\GestureClip.App\bin\Release\net8.0-windows10.0.19041.0\GestureClip.exe"
set "DBG_EXE=%REPO_ROOT%src\GestureClip.App\bin\Debug\net8.0-windows10.0.19041.0\GestureClip.exe"
set "PKG_EXE=%REPO_ROOT%artifacts\release\GestureClip\GestureClip.exe"
set "PROJECT_FILE=%REPO_ROOT%src\GestureClip.App\GestureClip.App.csproj"
set "QUICK_SCRIPT=%REPO_ROOT%scripts\build-quick.ps1"
set "PUBLISH_SCRIPT=%REPO_ROOT%scripts\publish-win-x64.ps1"
set "FRESHNESS_SCRIPT=%REPO_ROOT%scripts\check-build-freshness.ps1"
set "BUILD_LOG=%TEMP%\gestureclip-build.log"

set "TARGET=release"
set "BUILD_CONFIG=Release"
set "FORCE_BUILD=0"
set "SKIP_BUILD=0"
set "ASK=0"
set "DO_PUBLISH=0"
set "APP_ARGS="

:parse_args
if "%~1"=="" goto args_done
if /i "%~1"=="--quick" goto opt_quick
if /i "%~1"=="-q" goto opt_quick
if /i "%~1"=="--debug" goto opt_quick
if /i "%~1"=="--release" goto opt_release
if /i "%~1"=="--packaged" goto opt_packaged
if /i "%~1"=="--publish" goto opt_publish
if /i "%~1"=="--rebuild" goto opt_rebuild
if /i "%~1"=="--no-build" goto opt_nobuild
if /i "%~1"=="--no-check" goto opt_nobuild
if /i "%~1"=="--ask" goto opt_ask
if /i "%~1"=="--help" goto usage
if /i "%~1"=="-h" goto usage
goto opt_passthrough

:opt_quick
set "TARGET=debug"
set "BUILD_CONFIG=Debug"
shift
goto parse_args

:opt_release
set "TARGET=release"
set "BUILD_CONFIG=Release"
shift
goto parse_args

:opt_packaged
set "TARGET=packaged"
shift
goto parse_args

:opt_publish
set "DO_PUBLISH=1"
shift
goto parse_args

:opt_rebuild
set "FORCE_BUILD=1"
shift
goto parse_args

:opt_nobuild
set "SKIP_BUILD=1"
shift
goto parse_args

:opt_ask
set "ASK=1"
shift
goto parse_args

:opt_passthrough
set "APP_ARGS=%APP_ARGS% "%~1""
shift
goto parse_args

:args_done
if "%DO_PUBLISH%"=="1" goto build_publish
if /i "%TARGET%"=="packaged" goto select_packaged
if /i "%TARGET%"=="debug" goto select_debug
set "APP_EXE=%REL_EXE%"
goto check_state

:select_debug
set "APP_EXE=%DBG_EXE%"
goto check_state

:select_packaged
set "APP_EXE=%PKG_EXE%"
if not exist "%APP_EXE%" (
    echo [FAILED] No packaged build at:
    echo          %PKG_EXE%
    echo          Run: Start-GestureClip.bat --publish
    goto fail
)
goto launch

:check_state
if "%SKIP_BUILD%"=="1" goto launch
if "%FORCE_BUILD%"=="1" goto build_target
if not exist "%APP_EXE%" goto build_target

rem Fast path: compare timestamps once, then either launch now or rebuild quietly.
set "STATE=UNKNOWN"
if exist "%FRESHNESS_SCRIPT%" (
    for /f "usebackq delims=" %%A in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%FRESHNESS_SCRIPT%" -Exe "%APP_EXE%" -SourceRoot "%REPO_ROOT%src"`) do set "STATE=%%A"
)

if /i "%STATE%"=="FRESH" goto launch
if /i not "%STATE%"=="STALE" goto launch

if "%ASK%"=="1" goto ask_menu

echo [INFO] Sources changed since the last build - rebuilding %BUILD_CONFIG% (a few seconds)...
goto build_target

:ask_menu
echo.
echo [WARN] Source files are newer than the build you are about to start.
echo.
echo   [1] Quick %BUILD_CONFIG% build and start  (seconds)
echo   [2] Full release publish and start        (runs all tests, a few minutes)
echo   [3] Start the outdated build anyway
echo.
choice /c 123 /n /t 12 /d 1 /m "Select [1-3], default 1 in 12s: "
if errorlevel 3 goto launch
if errorlevel 2 goto build_publish
goto build_target

:build_publish
if not exist "%PUBLISH_SCRIPT%" (
    echo [FAILED] Missing publish script: %PUBLISH_SCRIPT%
    goto fail
)
echo [INFO] Full package build: restore + tests + publish, this takes a few minutes...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PUBLISH_SCRIPT%"
if errorlevel 1 goto fail
set "APP_EXE=%PKG_EXE%"
goto launch

:build_target
if not exist "%QUICK_SCRIPT%" (
    echo [FAILED] Missing build script: %QUICK_SCRIPT%
    goto fail
)
call :run_build
if errorlevel 1 goto build_failed
goto launch

:run_build
powershell -NoProfile -ExecutionPolicy Bypass -File "%QUICK_SCRIPT%" -Configuration %BUILD_CONFIG% > "%BUILD_LOG%" 2>&1
exit /b %errorlevel%

:build_failed
echo.
echo [FAILED] Build failed. Last lines of the log:
echo.
powershell -NoProfile -Command "Get-Content -LiteralPath '%BUILD_LOG%' -Tail 25"
echo.
echo Full log: %BUILD_LOG%
pause
exit /b 1

:launch
if not exist "%APP_EXE%" goto fail
start "" "%APP_EXE%" %APP_ARGS%
exit /b 0

:fail
echo.
pause
exit /b 1

:usage
echo Usage: Start-GestureClip.bat [options] [app arguments]
echo.
echo   (no option)   rebuild if needed, then start the Release build  [one click]
echo   --quick, -q   Debug build instead (fastest while tweaking the UI)
echo   --packaged    start artifacts\release portable copy (built by --publish)
echo   --publish     full package build: restore + tests + publish, minutes
echo   --rebuild     rebuild even if the current build looks fresh
echo   --no-build    start the existing exe without any check or build
echo   --ask         interactive menu when the build is stale
echo.
echo   Everything else is forwarded to GestureClip.exe, e.g. --trace-ui or --trace-mem
exit /b 0
