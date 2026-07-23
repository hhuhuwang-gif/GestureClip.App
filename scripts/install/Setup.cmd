@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "PS1=%SCRIPT_DIR%install.ps1"

if not exist "%PS1%" (
  echo install.ps1 not found next to Setup.cmd
  exit /b 1
)

if /I "%~1"=="/S" goto silent
if /I "%~1"=="/SILENT" goto silent
if /I "%~1"=="-Silent" goto silent
if /I "%~1"=="/UNINSTALL" goto uninstall

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
exit /b %ERRORLEVEL%

:silent
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Silent %2 %3 %4
exit /b %ERRORLEVEL%

:uninstall
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Uninstall -Silent
exit /b %ERRORLEVEL%
