@echo off
setlocal
cd /d "%~dp0"

echo Running UI performance check (close GestureClip first)...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\check-ui-performance.ps1" %*
set "EXITCODE=%errorlevel%"

echo.
if not "%EXITCODE%"=="0" (
    echo FAILED: a UI performance budget was exceeded or no sample was collected.
    echo See the log folder under LOCALAPPDATA\GestureClip\logs for details.
) else (
    echo Done.
)

pause
exit /b %EXITCODE%
