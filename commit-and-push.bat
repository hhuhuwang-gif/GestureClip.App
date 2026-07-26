@echo off
setlocal
cd /d "%~dp0"
git add -A
git commit -m "fix(clipboard): search-box digit bug; pinyin/regex search, links filter, text tools, UI polish"
git push origin main
echo.
pause
