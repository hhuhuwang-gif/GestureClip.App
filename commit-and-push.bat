@echo off
rem One-click commit & push for the clipboard overlay improvements.
cd /d "%~dp0"
git add -A
git commit -m "fix(clipboard): digits typed in search no longer trigger paste-by-index; add pinyin-initial & regex search, links filter, text tools, UI polish"
git push origin main
pause
