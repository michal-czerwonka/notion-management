# Inbox — React + TypeScript + Capacitor

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

Z katalogu głównego repozytorium:

```powershell
cd NotionManagementFunctionApp
func start --functions GetInbox CreateInboxItem
```

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

W Azure ustaw `Notion__InboxDataSourceId`. Możesz też wypełnić `$NotionInboxDataSourceId` w `deploy.ps1`; pusta wartość pozostawia istniejące ustawienie Azure bez zmian. Skrypt dodaje do CORS origin Androida `https://localhost`. Dla lokalnego frontendu korzystającego z Azure dodaj również `http://127.0.0.1:5173` do `$InboxAllowedOrigins` albo w panelu CORS Function App. CORS dotyczy całej Function App; istniejące originy pozostają zachowane.

**MVP:** endpointy są anonimowe. Losowy fragment ścieżki utrudnia zgadywanie, ale jest widoczny w APK i ruchu sieciowym. Każdy znający URL może czytać i dodawać wpisy. TODO docelowej autoryzacji znajduje się w `InboxFunctions.cs` i `src/api/inbox.ts`. Nie umieszczaj klucza Function App ani tokena Notion w aplikacji.

## 2. Frontend lokalnie

Wymagany Node.js 22+. Z katalogu głównego repozytorium:

```powershell
cd NotionInboxApp
npm ci
Copy-Item .env.example .env.local
npm run dev
```

Otwórz `http://127.0.0.1:5173`. Jeśli port jest zajęty, zwolnij go lub dodaj faktyczny origin do CORS. `VITE_INBOX_API_URL` w `.env.local` to pełny adres endpointu. Zmiana wymaga restartu Vite, a dla APK ponownego builda i synchronizacji. Wszystkie wartości `VITE_*` są publiczne i wbudowane w aplikację.

```powershell
npm run build
npm run preview
```

Preview używa portu 4173 — dodaj jego origin do lokalnego CORS, jeśli chcesz korzystać z API w tym trybie.

## 3. Android przez Capacitor

Zainstaluj Android Studio 2025.2.1 lub nowsze, Android SDK Platform 36 oraz SDK Platform-Tools. Android Studio dostarcza JDK; dla poleceń Gradle ustaw `JAVA_HOME` na jego katalog `jbr` (JDK 21+) oraz `ANDROID_HOME` na katalog SDK. Szczegóły: [środowisko Capacitor](https://capacitorjs.com/docs/getting-started/environment-setup), [Android](https://capacitorjs.com/docs/android).

W `.env.local` ustaw adres wdrożonej Function App przez HTTPS:

```dotenv
VITE_INBOX_API_URL=https://TWOJA-FUNCTION-APP.azurewebsites.net/api/inbox/a9cea60dda62442e
```

Projekt `android/` jest w repozytorium, więc nie wykonuj ponownie `cap add android`. Z katalogu `NotionInboxApp`:

```powershell
npm run android:sync
npm run android:open
```

W Android Studio poczekaj na Gradle Sync, wybierz emulator lub telefon z włączonym debugowaniem USB i kliknij Run. Alternatywnie:

```powershell
npm run android:run
```

Telefon nie widzi komputera pod `localhost`. Dla urządzenia używaj API Azure przez HTTPS (z CORS `https://localhost`); konfiguracja produkcyjna nie dopuszcza niezabezpieczonego HTTP. Lokalne API można udostępnić przez tunel HTTPS i wpisać jego URL do `.env.local`.

## 4. APK i instalacja przez USB

Po skonfigurowaniu SDK/JDK, z katalogu `NotionInboxApp`:

```powershell
npm run android:sync
cd android
.\gradlew.bat assembleDebug
adb devices
adb install -r app\build\outputs\apk\debug\app-debug.apk
```

`adb` musi być w PATH (katalog `$env:ANDROID_HOME\platform-tools`). W telefonie włącz Opcje programisty → Debugowanie USB i zaakceptuj klucz komputera. Przy wielu urządzeniach użyj `adb -s SERIAL install -r ...`.

Debug APK jest podpisany kluczem deweloperskim i wystarcza do MVP. Do dystrybucji użyj Android Studio → Build → Generate Signed App Bundle / APK → APK i własnego keystore przechowywanego poza repozytorium. Po każdej zmianie frontendu/config uruchom ponownie `android:sync` oraz build APK.
