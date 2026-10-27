@echo off
setlocal
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -File "%~dp0src\scripts\startup\run.ps1" %*
set "VISORY_EXIT=%ERRORLEVEL%"
if "%VISORY_EXIT%"=="0" exit /b 0
echo Visory stopped with exit code %VISORY_EXIT%. See the diagnostics paths above.
if not "%VISORY_NONINTERACTIVE%"=="1" pause
exit /b %VISORY_EXIT%
