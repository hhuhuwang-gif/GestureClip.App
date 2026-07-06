namespace GestureClip.Infrastructure.Updates;

public static class UpdateInstallerScriptBuilder
{
    public static string Build(string sourceDirectory, string installDirectory, string executableName)
    {
        return $$"""
@echo off
setlocal
set "SRC={{sourceDirectory}}"
set "DEST={{installDirectory}}"
set "EXE={{executableName}}"

echo Waiting for GestureClip to exit...
timeout /t 2 /nobreak >nul

echo Installing update...
robocopy "%SRC%" "%DEST%" /E /R:20 /W:1 /XF gestureclip.db gestureclip.db-shm gestureclip.db-wal /XD logs
if %ERRORLEVEL% GEQ 8 (
    echo Update failed. Robocopy exit code: %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

echo Restarting GestureClip...
start "" "%DEST%\%EXE%"

echo Cleaning temporary update files...
rmdir /s /q "%SRC%" 2>nul
(del "%~f0" 2>nul) & exit /b 0
""";
    }
}
