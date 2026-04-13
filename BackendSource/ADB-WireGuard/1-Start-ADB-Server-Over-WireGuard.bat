@echo off
setlocal

set "REPORT=%~dp0state\last-start-report.txt"
set "WRAPPER_REPORT=%~dp0state\5-wrapper-report.txt"

del /f /q "%REPORT%" "%WRAPPER_REPORT%" >nul 2>&1

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-ADB-WG-Wrapper.ps1"
set "EXITCODE=%ERRORLEVEL%"

if exist "%REPORT%" (
    echo.
    type "%REPORT%"
    echo.
) else if exist "%WRAPPER_REPORT%" (
    echo.
    type "%WRAPPER_REPORT%"
    echo.
)

pause
exit /b %EXITCODE%
