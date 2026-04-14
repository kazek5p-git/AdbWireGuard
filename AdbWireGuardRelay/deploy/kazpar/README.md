# Wdrozenie AdbWireGuard Broker na kazpar

Ten katalog zawiera minimalny zestaw do wdrozenia brokera sesji `AdbWireGuard` na serwerze Linux z `systemd` oraz reverse proxy `nginx` lub `Apache`.

## Co jest tutaj

- `adbwireguard-broker.service` - jednostka `systemd`
- `adbwireguard-broker.user.service` - jednostka `systemd --user`
- `adbwireguard-broker.nginx.conf` - przykladowy reverse proxy dla `nginx`
- `adbwireguard-broker.apache.conf` - przykladowy snippet dla `Apache`
- `install-kazpar.sh` - skrypt pierwszej instalacji lub aktualizacji po stronie serwera
- `publish-kazpar.ps1` - lokalny skrypt publikacji artefaktow do paczki wdrozeniowej

## Zalecany model wdrozenia

1. Lokalnie zbuduj paczke:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\kazpar\publish-kazpar.ps1
```

2. Skopiuj na serwer katalog `artifacts\kazpar-deploy`.

3. Na serwerze uruchom:

```bash
sudo ./install-kazpar.sh
```

4. Uzupelnij tokeny hosta w:

```bash
/etc/adbwireguard-broker.env
```

Przyklad:

```bash
ADBWG_BROKER_HOST_TOKENS=twoj-sekretny-token
```

5. Wlacz i sprawdz usluge:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now adbwireguard-broker.service
sudo systemctl status adbwireguard-broker.service
curl http://127.0.0.1:5127/healthz
```

## Wariant bez sudo

Jesli serwer nie ma zainstalowanego `dotnet` i nie masz `sudo` bez hasla, mozna uruchomic broker jako `systemd --user` na self-contained publish.

W tym wariancie:

- publikujesz `linux-x64 --self-contained`
- kopiujesz payload do `~/adbwireguard-broker/current`
- zapisujesz token do `~/.config/adbwireguard-broker.env`
- uruchamiasz `adbwireguard-broker.user.service`

Ten model zostal sprawdzony na `kazpar`.

## Reverse proxy

Przyklad `nginx` lub `Apache` zaklada:

- broker nasluchuje lokalnie na `127.0.0.1:5127`
- publiczny endpoint jest wystawiony pod osobna domena lub sciezka
- `WebSocket` dla `/ws/...` przechodzi przez proxy bez terminowania aplikacyjnego

## Uwaga o SSH

Na tym etapie repo ma juz komplet plikow deploymentu, ale samo zdalne wdrozenie wymaga poprawnego logowania SSH na `kazpar`.
