@echo off
setlocal
if "%~1"=="" (
    if "%ADB_WG_SERVER_HOST%"=="" (
        echo Podaj adres serwera jako pierwszy argument albo ustaw zmienna ADB_WG_SERVER_HOST.
        echo Przyklad: 7-Remote-ADB-Logcat.bat 10.10.10.1
        echo.
        pause
        exit /b 1
    )
    set "WG_HOST=%ADB_WG_SERVER_HOST%"
) else (
    set "WG_HOST=%~1"
)
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp02-Run-Remote-ADB-Command.ps1" -ServerHost "%WG_HOST%" -AdbCommand "logcat -d"
set "EXITCODE=%ERRORLEVEL%"
echo.
pause
exit /b %EXITCODE%
