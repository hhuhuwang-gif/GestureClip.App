@echo off
setlocal
chcp 65001 >nul
title GestureClip Setup
set "SCRIPT_DIR=%~dp0"
set "PS1=%SCRIPT_DIR%install.ps1"

echo.
echo  ========================================
echo   GestureClip 安装
echo  ========================================
echo   双击本 Setup.cmd 即可安装（无需管理员）
echo   安装目录: %%LOCALAPPDATA%%\Programs\GestureClip
echo   用户数据: %%LOCALAPPDATA%%\GestureClip\
echo  ========================================
echo.

if not exist "%PS1%" (
  echo [错误] 找不到 install.ps1，请确认已完整解压 Setup 压缩包。
  echo.
  pause
  exit /b 1
)

if /I "%~1"=="/S" goto silent
if /I "%~1"=="/SILENT" goto silent
if /I "%~1"=="-Silent" goto silent
if /I "%~1"=="/UNINSTALL" goto uninstall

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
set "ERR=%ERRORLEVEL%"
if not "%ERR%"=="0" (
  echo.
  echo [错误] 安装失败，退出码 %ERR%
  pause
)
exit /b %ERR%

:silent
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Silent %2 %3 %4
exit /b %ERRORLEVEL%

:uninstall
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%" -Uninstall -Silent
exit /b %ERRORLEVEL%
