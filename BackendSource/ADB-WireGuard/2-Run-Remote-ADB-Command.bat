@echo off
setlocal
if "%~1"=="" (
    echo Uzycie:
    echo %~nx0 IP_Z_WIREGUARD [polecenie adb...]
    echo.
    echo Przyklad:
    echo %~nx0 10.66.66.2 devices
    echo %~nx0 10.66.66.2 shell getprop ro.product.model
    echo.
    pause
    exit /b 1
)

set "WG_HOST=%~1"
shift

if "%~1"=="" (
    powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp02-Run-Remote-ADB-Command.ps1" -ServerHost "%WG_HOST%" -AdbCommand "devices"
) else (
    powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp02-Run-Remote-ADB-Command.ps1" -ServerHost "%WG_HOST%" -AdbCommand "%*"
)

set "EXITCODE=%ERRORLEVEL%"
echo.
pause
exit /b %EXITCODE%
