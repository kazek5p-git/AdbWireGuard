@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp05-Start-ADB-Server-As-Admin.ps1"
exit /b %ERRORLEVEL%
