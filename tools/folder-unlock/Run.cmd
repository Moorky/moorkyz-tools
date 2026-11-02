@echo off
setlocal
powershell.exe -NoProfile -File "%~dp0Unlock-Folder.ps1" %*
exit /b %errorlevel%
