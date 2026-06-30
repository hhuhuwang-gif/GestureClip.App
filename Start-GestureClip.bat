@echo off
setlocal

set "ROOT=%~dp0"
set "EXE=%ROOT%artifacts\release\GestureClip\GestureClip.exe"
set "PUBLISH=%ROOT%scripts\publish-win-x64.ps1"

if not exist "%EXE%" (
    echo GestureClip.exe was not found.
    echo Building the release package now...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PUBLISH%"
    if errorlevel 1 (
        echo.
        echo Build failed. Please install the .NET 8 SDK, then try again.
        pause
        exit /b 1
    )
)

if not exist "%EXE%" (
    echo.
    echo GestureClip.exe still was not found:
    echo %EXE%
    pause
    exit /b 1
)

start "" "%EXE%"
endlocal
