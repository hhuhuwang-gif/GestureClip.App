@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo  GestureClip - rebuild exe
echo ============================================
echo.

echo [INFO] Running restore + tests + publish, this takes a few minutes...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\publish-win-x64.ps1"
if errorlevel 1 (
    echo.
    echo [FAILED] Build, tests or packaging failed. See the error above.
    echo.
    pause
    exit /b 1
)

echo.
echo [OK] Built artifacts\release\GestureClip\GestureClip.exe
echo.
pause
