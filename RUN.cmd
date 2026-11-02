@echo off
setlocal
if "%MOORKYZ_HEADLESS%"=="1" goto cli
if not "%~1"=="" goto cli
if exist "%~dp0src\.moorkyz\app\dist\Moorkyz.exe" (
  start "" "%~dp0src\.moorkyz\app\dist\Moorkyz.exe" --root "%~dp0."
  exit /b
)
start "" powershell.exe -NoLogo -NoProfile -WindowStyle Hidden -File "%~dp0src\.moorkyz\app\launch.ps1" -Root "%~dp0."
exit /b %ERRORLEVEL%
:cli
pushd "%~dp0" || exit /b 1
node "moorkyz" %*
set "moorkyz_exit=%ERRORLEVEL%"
popd
exit /b %moorkyz_exit%