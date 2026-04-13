# Kazpar Relay Plan

## Cel

Umożliwić zdalne użycie `adb` bez ręcznej konfiguracji routera u użytkownika końcowego.
Komputer z telefonem ma działać jako host sesji, a `kazpar` ma pełnić rolę pośrednika.

## Czego nie robić

Nie wystawiać surowego `adb` publicznie na stałym porcie.

Powody:

- `adb` daje bardzo szerokie uprawnienia do urządzenia
- przypadkowe wystawienie portu tworzy trwałą powierzchnię ataku
- trudno to bezpiecznie ograniczyć tylko firewallem i adresem IP

## Zalecana architektura

Model: broker sesji + tunel odwrócony.

Strony:

- `Host`
  komputer z telefonem pod USB
- `Client`
  komputer, który chce połączyć się ze zdalnym `adb`
- `Relay`
  serwer na `kazpar`, który zestawia i autoryzuje jedną sesję między `Host` i `Client`

## Przepływ

1. `Host` loguje się do relaya i otwiera połączenie wychodzące TLS/WebSocket.
2. Relay tworzy krótkotrwałą sesję i zwraca:
   - `session_id`
   - jednorazowy kod lub token
   - czas wygaśnięcia
3. Użytkownik przekazuje kod klientowi.
4. `Client` łączy się do relaya i podaje kod.
5. Relay sprawdza:
   - czy host nadal jest online
   - czy kod nie wygasł
   - czy sesja nie została już użyta
6. Relay spina oba kanały TCP.
7. `Client` dostaje lokalny endpoint, np. `127.0.0.1:5037`, a program działa dalej jak zwykły klient `adb`.
8. Po rozłączeniu relay niszczy sesję.

## Minimalny zestaw bezpieczeństwa

- Każda sesja musi być jawnie uruchomiona przez `Host`.
- Kod parujący musi być:
  - jednorazowy
  - krótko ważny, np. `2-5 min`
  - losowy, minimum `128 bit` entropii
- Relay nie powinien przechowywać długoterminowo danych sesji poza logiem technicznym.
- `Host` musi móc zakończyć sesję jednym przyciskiem.
- Na serwerze trzeba trzymać limit:
  - maksymalnej liczby sesji
  - czasu trwania jednej sesji
  - liczby prób użycia złego kodu

## Uwierzytelnienie

Pierwsza wersja:

- `Host` ma własny długoterminowy token urządzenia
- `Client` używa jednorazowego kodu od `Host`

Lepsza wersja:

- konto użytkownika
- lista zaufanych klientów
- opcjonalne zatwierdzanie klienta po nazwie

## Transport

Najprostszy i sensowny wariant:

- HTTPS + WebSocket do sterowania sesją
- surowy strumień binarny po WebSocket albo TCP relay po stronie serwera

Nie ma potrzeby budować własnego protokołu ADB.
Relay ma tylko przenosić bajty między `Client` i `Host`.

## Integracja z obecnym GUI

W GUI można dodać trzeci tryb pracy:

- `Połącz przez serwer kazpar`

Po stronie `Host`:

- przycisk `Udostępnij przez kazpar`
- status sesji
- kod parujący
- przycisk `Zakończ sesję`

Po stronie `Client`:

- pole `Kod sesji`
- przycisk `Połącz`
- lokalny test `adb devices`

Obecny tryb WireGuard może zostać jako tryb zaawansowany.
Relay powinien być prostszą opcją dla większości użytkowników.

## Co uruchomić na kazpar

Na serwerze wystarczy osobna usługa, np.:

- API HTTP do tworzenia i łączenia sesji
- broker połączeń WebSocket/TCP

Sensowny stos:

- `Go`
  albo
- `Node.js`
  albo
- `.NET`

Najpraktyczniej:

- jeden mały serwis `relay-api`
- reverse proxy przez istniejący `nginx`
- osobny subpath albo subdomena, np. `adbwg.kazpar.pl`

## Logi i prywatność

Logować tylko:

- czas sesji
- `session_id`
- IP klienta i hosta
- wynik połączenia

Nie logować:

- treści komend `adb shell`
- danych przesyłanych przez strumień ADB

## Plan wdrożenia

### Etap 1

- relay dla jednego użytkownika
- token hosta zapisany ręcznie
- jednorazowy kod sesji
- jeden aktywny klient na sesję

### Etap 2

- wiele hostów
- lista urządzeń
- historia ostatnich sesji
- lepsze komunikaty w GUI

### Etap 3

- konta użytkowników
- autoryzacja klienta
- limity i monitoring

## Wniosek

Tak, `kazpar` może być serwerem pośrednim, ale powinien działać jako broker sesji i tunel odwrócony, nie jako publicznie wystawiony port `adb`.

To jest wykonalne i sensowne, jeśli priorytetem jest:

- prostota dla użytkownika
- brak ręcznej konfiguracji routera
- zachowanie kontroli nad dostępem do telefonu
