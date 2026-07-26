@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo  GestureClip - rebuild exe
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet SDK not found.
    echo Install .NET 8 SDK first: https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

tasklist /FI "IMAGENAME eq GestureClip.exe" 2>nul | find /I "GestureClip.exe" >nul
if not errorlevel 1 (
    echo [INFO] GestureClip is running, closing it...
    taskkill /IM GestureClip.exe /F >nul 2>nul
    timeout /t 2 /nobreak >nul
)

echo [INFO] Running restore + tests + publish, this takes a few minutes...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\publish-win-x64.ps1"
if errorlevel 1 (
    echo.
    echo [FAILED] Build or tests failed. exe NOT updated.
    echo Please send the error above to Claude.
    echo.
    pause
    exit /b 1
)

echo.
echo [OK] GestureClip.exe and GestureClip-latest.exe updated.
echo Verify: open clipboard panel, type digits in search box - should NOT auto-paste.
echo.
pause
