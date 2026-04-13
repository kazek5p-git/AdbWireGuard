@echo off
setlocal

set "LOCAL_REPORT=%LOCALAPPDATA%\ADB-WireGuard\state\last-launcher-report-local.txt"
set "LOCAL_START_REPORT=%~dp0state\last-start-report.txt"

del /f /q "%LOCAL_REPORT%" >nul 2>&1
del /f /q "%LOCAL_START_REPORT%" >nul 2>&1

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-ADB-WG-Admin.ps1"
set "EXITCODE=%ERRORLEVEL%"
if exist "%LOCAL_START_REPORT%" (
    echo.
    type "%LOCAL_START_REPORT%"
    echo.
) else if exist "%LOCAL_REPORT%" (
    echo.
    type "%LOCAL_REPORT%"
    echo.
)
pause
exit /b %EXITCODE%
