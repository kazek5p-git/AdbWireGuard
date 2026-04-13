# AdbWireGuard Relay

Minimalny broker sesji dla `ADB over WireGuard`.

## Co robi

- tworzy krótkotrwałe sesje relay
- generuje jednorazowy kod parowania
- wydaje token wznowienia dla hosta i klienta
- trzyma krótki grace period na reconnect
- wpina hosta i klienta przez WebSocket
- przekazuje surowe dane między obiema stronami

## Czego nie robi

- nie wystawia publicznie surowego portu `adb`
- nie przechowuje trwałych danych sesji
- nie daje jeszcze pełnego modelu wielu użytkowników

## Konfiguracja

Ustaw co najmniej jeden token hosta:

```powershell
$env:ADBWG_RELAY_HOST_TOKENS="twoj-sekretny-token"
```

Możesz też użyć kilku tokenów:

```powershell
$env:ADBWG_RELAY_HOST_TOKENS="token-1;token-2"
```

## Uruchomienie

```powershell
dotnet run --project .\AdbWireGuardRelay
```

Domyślny health check:

```text
GET /healthz
```

## API

### Utworzenie sesji przez hosta

```text
POST /api/v1/relay/sessions
Authorization: Bearer <host-token>
```

Body:

```json
{
  "deviceName": "Pixel 10 Pro",
  "requestedTtlMinutes": 5
}
```

### Claim przez klienta

```text
POST /api/v1/relay/claim
```

Body:

```json
{
  "pairCode": "ABCD1234...",
  "clientName": "Kazek-PC"
}
```

### WebSocket hosta

```text
/ws/host/{sessionId}?token={hostConnectToken|hostResumeToken}
```

### WebSocket klienta

```text
/ws/client/{sessionId}?token={clientConnectToken|clientResumeToken}
```

### Heartbeat / odświeżenie sesji

```text
POST /api/v1/relay/sessions/{sessionId}/heartbeat
```

Body:

```json
{
  "role": "host",
  "resumeToken": "token-wznowienia"
}
```

## Następny etap

- dopiąć w GUI prawdziwy tunel ADB po WebSocket
- dodać automatyczny reconnect host/client z użyciem resume token
- dodać bezpieczne wdrożenie za reverse proxy na `kazpar`
