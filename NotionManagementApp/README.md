# Notion Management — React + TypeScript + Capacitor

Osobny projekt npm obok rozwiązania .NET. `src/App.tsx` jest punktem wejścia dla kolejnych widoków, `src/pages/InboxPage.tsx` obsługuje Inbox, a `src/api/inbox.ts` komunikację wyłącznie z Function App. Nie ma routera, globalnego store ani biblioteki UI; wygląd inspirowany Material Design zapewnia CSS.

## 1. Backend

W istniejącym `NotionManagementFunctionApp/local.settings.json` dodaj do `Values`:

```json
"Notion:InboxDataSourceId": "TODO_INBOX_DATA_SOURCE_ID"
```

Zastąp placeholder **data source ID** Inbox (nie ID widoku). Wykorzystywany jest istniejący `Notion:Token`. Udostępnij bazę tej integracji w Notion i nadaj jej uprawnienia odczytu oraz dodawania stron. Property `Nazwa` musi mieć typ `title`.

Dodaj/rozszerz sekcję `Host` obok `Values`, zachowując istniejące ustawienia:

```json
"Host": {
  "CORS": "http://127.0.0.1:5173,http://localhost:5173,https://localhost"
}
```

Plik `local.settings.json.example` zawiera wzór konfiguracji dla świeżego checkoutu (z wyłączonymi timerami). Nie nadpisuj nim istniejących ustawień. Przy `UseDevelopmentStorage=true` uruchom lokalny Azurite. Wymagane: .NET 10 SDK i Azure Functions Core Tools v4.

Z katalogu głównego repozytorium uruchom `scripts\local\Start-LocalFunctionApp.ps1`. Skrypt uruchamia Azurite w tle i Function App na `http://127.0.0.1:7071`. Po zakończeniu pracy zatrzymaj Azurite przez `scripts\local\Stop-LocalAzurite.ps1`.

Kontrakt endpointów:

```http
GET /api/inbox/a9cea60dda62442e
```

Odpowiedź `200`: `[{ "id": "notion-page-id", "name": "Moja myśl" }]`. Lista zawiera wszystkie strony wyników, od najnowszych według czasu utworzenia.

```http
POST /api/inbox/a9cea60dda62442e
Content-Type: application/json

{ "name": "Moja myśl" }
```

Odpowiedź `201`: `{ "id": "notion-page-id", "name": "Moja myśl" }`. Backend usuwa białe znaki z początku i końca oraz wymaga 1–2000 znaków. `400` oznacza błędne dane, `503` brak konfiguracji, `502` błąd komunikacji/odpowiedzi Notion, `504` timeout. Błędy mają postać `{ "error": "..." }` i nie ujawniają odpowiedzi Notion ani sekretów. POST nie jest automatycznie ponawiany; przy utracie potwierdzenia sprawdź listę przed ponownym zapisem.

```http
PATCH /api/inbox/a9cea60dda62442e/{notion-page-id}
Content-Type: application/json

{ "name": "Poprawiona myśl" }
```

Odpowiedź `200`: `{ "id": "notion-page-id", "name": "Poprawiona myśl" }`. Endpoint aktualizuje wyłącznie strony należące do Inbox; dla obcego lub usuniętego wpisu zwraca `404`.

W Azure ustaw `Notion__InboxDataSourceId`. Możesz też wypełnić `$NotionInboxDataSourceId` w `deploy.ps1`; pusta wartość pozostawia istniejące ustawienie Azure bez zmian. Skrypt dodaje do CORS origin Androida `https://localhost`. Dla lokalnego frontendu korzystającego z Azure dodaj również `http://127.0.0.1:5173` do `$InboxAllowedOrigins` albo w panelu CORS Function App. CORS dotyczy całej Function App; istniejące originy pozostają zachowane.

**MVP:** endpointy są anonimowe. Losowy fragment ścieżki utrudnia zgadywanie, ale jest widoczny w APK i ruchu sieciowym. Każdy znający URL może czytać i dodawać wpisy. TODO docelowej autoryzacji znajduje się w `NotionInboxItemFunctions.cs` i `src/api/inbox.ts`. Nie umieszczaj klucza Function App ani tokena Notion w aplikacji.

## 2. Lokalne profile API

Adres API jest wybierany przed budową APK i zostaje w nim osadzony. Prywatne pliki profili są ignorowane przez Git:

- `.env.local-emulator` — lokalna Function App dla emulatora Androida. Używa `http://10.0.2.2:7071`, ponieważ pod tym adresem emulator widzi Windows.
- `.env.development-phone` — wdrożona, developerska Function App dostępna przez HTTPS dla fizycznego telefonu.

Przy pierwszym użyciu utwórz oba pliki na podstawie wzorów:

```powershell
cd NotionManagementApp
Copy-Item .env.local-emulator.example .env.local-emulator
Copy-Item .env.development-phone.example .env.development-phone
```

W pliku `development-phone` ustaw rzeczywisty publiczny adres Function App. Wartości `VITE_*` są publiczne i wbudowywane do APK, dlatego nigdy nie umieszczaj w nich tokenów ani kluczy Function App.

## 3. Build APK

Projekt `android/` jest w repozytorium, więc nie uruchamiaj `cap add android`. Skrypt sam instaluje zależności npm, jeśli ich nie ma, buduje frontend z właściwym profilem, wykonuje `cap sync android`, buduje debug APK przez Gradle Wrapper i otwiera folder wyniku w Eksploratorze Windows. Wymaga JDK 21 ustawionego w `JAVA_HOME`; wbudowane JBR Android Studio ma obecnie Javę 25 i nie współpracuje z używaną wersją Gradle.

Build dla lokalnego emulatora — wcześniej uruchom `Start-LocalFunctionApp.ps1`:

```powershell
.\scripts\local\Build-LocalEmulatorApk.ps1
```

Build dla fizycznego telefonu, korzystający z developerskiego API HTTPS:

```powershell
.\scripts\local\Build-DevelopmentPhoneApk.ps1
```

Wyniki są kopiowane do `artifacts\android` przy root repozytorium:

- `NotionManagementApp-localemulator-debug.apk`
- `NotionManagementApp-developmentphone-debug.apk`

Możesz przeciągnąć APK do uruchomionego emulatora w Android Studio albo zainstalować go ręcznie na telefonie. `adb` musi być w `PATH` (zwykle `%ANDROID_HOME%\platform-tools`).

## 4. Frontend w przeglądarce

Do pracy z Vite w przeglądarce ustaw tymczasowo adres lokalnego API dla bieżącej sesji PowerShell i uruchom serwer:

```powershell
cd NotionManagementApp
$env:VITE_INBOX_API_URL = 'http://127.0.0.1:7071/api/inbox/a9cea60dda62442e'
npm run dev
```

Otwórz `http://127.0.0.1:5173`. Jeśli używasz wdrożonego API, ustaw jego pełny adres HTTPS zamiast lokalnego. Preview używa portu 4173, który trzeba dodatkowo dopuścić w CORS lokalnej Function App.
