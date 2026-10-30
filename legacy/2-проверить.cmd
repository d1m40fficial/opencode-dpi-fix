@echo off
setlocal

rem Read-only check. No administrator rights needed.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0check.ps1" %*
echo.
pause
