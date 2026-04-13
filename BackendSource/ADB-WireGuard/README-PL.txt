ADB przez WireGuard

Ten pakiet działa lokalnie. Nie wymaga dysku N: ani udziału sieciowego.

Układ:
- komputer z telefonem: uruchom serwer ADB
- drugi komputer: połącz się z tym serwerem

Najważniejsze pliki:
- 1-Start-ADB-Server-Over-WireGuard.ps1
- 1-Start-ADB-Server-Over-WireGuard.bat
- 2-Run-Remote-ADB-Command.ps1
- 2-Run-Remote-ADB-Command.bat
- 3-Stop-ADB-Server-Over-WireGuard.ps1
- 3-Stop-ADB-Server-Over-WireGuard.bat
- Invoke-ADB-WG-Wrapper.ps1

Uruchomienie serwera na komputerze z telefonem:
- powershell -ExecutionPolicy Bypass -File ".\1-Start-ADB-Server-Over-WireGuard.ps1"
- albo uruchom: 1-Start-ADB-Server-Over-WireGuard.bat
- albo uruchom jako administrator: 5-Start-ADB-Server-As-Admin.bat

Połączenie z drugiego komputera:
- powershell -ExecutionPolicy Bypass -File ".\2-Run-Remote-ADB-Command.ps1" -ServerHost <ADRES_SERWERA> -AdbCommand "devices"
- albo: 2-Run-Remote-ADB-Command.bat <ADRES_SERWERA> devices

Zatrzymanie:
- powershell -ExecutionPolicy Bypass -File ".\3-Stop-ADB-Server-Over-WireGuard.ps1"
- albo uruchom: 3-Stop-ADB-Server-Over-WireGuard.bat

Uwagi:
- Pakiet szuka adb.exe w PATH, w typowych lokalizacjach Android SDK i w folderze platform-tools obok skryptów.
- Klucz SSH do routera nie jest dołączany do paczki. Importujesz go osobno w GUI albo wkładasz do folderu mikrotik obok skryptów pod neutralną nazwą mikrotik_ed25519.
- Stan i logi są zapisywane lokalnie w folderze state obok skryptów oraz w AppData\Local\ADB-WireGuard.
