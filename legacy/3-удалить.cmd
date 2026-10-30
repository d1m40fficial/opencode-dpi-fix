@echo off
setlocal

rem Elevates itself: editing the hosts file requires administrator rights.
net session >nul 2>&1
if errorlevel 1 (
  powershell -NoProfile -Command "Start-Process -Verb RunAs -FilePath '%~f0'"
  exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0apply-hosts.ps1" -Revert %*
echo.
pause
