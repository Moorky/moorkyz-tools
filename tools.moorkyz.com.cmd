@echo off
REM Windows CMD / PowerShell launcher for the tools.moorkyz.com CLI.
node "%~dp0src\.moorkyz\launchers\tools.moorkyz.com.cjs" %*
exit /b %ERRORLEVEL%