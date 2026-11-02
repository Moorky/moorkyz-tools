@echo off
setlocal
REM Optional Windows helper; the portable public entrypoint is the root moorkyz file.
pushd "%~dp0..\..\.." || exit /b 1
node "moorkyz" %*
set "moorkyz_exit=%ERRORLEVEL%"
popd
exit /b %moorkyz_exit%
