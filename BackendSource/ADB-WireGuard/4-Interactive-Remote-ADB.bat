@echo off
setlocal EnableExtensions EnableDelayedExpansion

echo Zdalne ADB po WireGuard
echo.
set /p WG_HOST=Podaj IP WireGuard komputera z Pixelem: 
if "%WG_HOST%"=="" set "WG_HOST=%ADB_WG_SERVER_HOST%"
if "%WG_HOST%"=="" (
echo Nie podano adresu serwera ADB przez WireGuard.
echo Ustaw go recznie albo wpisz zmienna ADB_WG_SERVER_HOST.
echo.
pause
exit /b 1
)

echo.
echo Wpisz komende adb bez slowa "adb".
echo Przyklady:
echo devices
echo shell getprop ro.product.model
echo logcat -d
echo install -r C:\sciezka\app.apk
echo.
set /p ADB_COMMAND=Polecenie: 
if "%ADB_COMMAND%"=="" set "ADB_COMMAND=devices"

echo.
echo Uruchamiam na %WG_HOST%:
echo adb %ADB_COMMAND%
echo.

powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp02-Run-Remote-ADB-Command.ps1" -ServerHost "%WG_HOST%" -AdbCommand "%ADB_COMMAND%"
set "EXITCODE=%ERRORLEVEL%"
echo.
pause
exit /b %EXITCODE%
