@echo off
setlocal

set "REPO_ROOT=%~dp0"
set "APP_EXE=%REPO_ROOT%artifacts\release\GestureClip\GestureClip.exe"
set "PUBLISH_SCRIPT=%REPO_ROOT%scripts\publish-win-x64.ps1"

if not exist "%APP_EXE%" (
    if not exist "%PUBLISH_SCRIPT%" (
        echo Missing publish script: "%PUBLISH_SCRIPT%"
        pause
        exit /b 1
    )

    echo GestureClip release executable not found. Building it now...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PUBLISH_SCRIPT%"
    if errorlevel 1 (
        echo Failed to build GestureClip release executable.
        pause
        exit /b 1
    )
)

if "%~1"=="" (
    start "" "%APP_EXE%"
    exit /b 0
)

"%APP_EXE%" %*
exit /b %ERRORLEVEL%
