@echo off
setlocal

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp03-Stop-ADB-Server-Over-WireGuard.ps1"
set "EXITCODE=%ERRORLEVEL%"

set "REPORT=%~dp0state\last-start-report.txt"
if exist "%REPORT%" (
    echo.
    type "%REPORT%"
    echo.
)

pause
exit /b %EXITCODE%
