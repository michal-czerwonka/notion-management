# Notion Management — React + TypeScript + Capacitor

This is a separate npm project alongside the .NET solution. `src/App.tsx` is the entry point for the application views, `src/pages/InboxPage.tsx` implements the Inbox, and `src/api/inbox.ts` communicates exclusively with the Function App. There is no router, global store, or UI library; the Material Design-inspired appearance is provided by CSS.

## Android launcher icons

After installing the APK, the Android launcher shows two icons: `Inbox` and `Routine tasks`. Both are aliases for the same Android activity, running in `singleTask` mode, so opening one after the other reuses the existing application instance. A native Capacitor bridge passes the selected launcher shortcut to React, which displays the Inbox or routine tasks view accordingly.

## Routine tasks

The local task configuration is in `src/config/routine-tasks.json`. Each task currently has an `id`, `name`, and `daysOfWeek`; the day list uses English identifiers from `monday` to `sunday`. New fields can later be added to the same object without changing the persisted state.

Completions and skips are stored only locally in the application. An active day lasts from 03:00 to 03:00 in the `Europe/Warsaw` time zone; after this boundary, both states are replaced with a new empty state. A screen left open refreshes at the boundary, and the state is also verified when the screen is opened again. No events are sent to the Function App yet.

## 1. Backend

Add the following setting to `Values` in the existing `NotionManagementFunctionApp/local.settings.json`:

```json
"Notion:InboxDataSourceId": "TODO_INBOX_DATA_SOURCE_ID"
```

Replace the placeholder with the Inbox **data source ID** (not a view ID). The existing `Notion:Token` is used. Share the database with the Notion integration and grant it permission to read, create, and update pages. The `Nazwa` property must have the `title` type.

Add or extend the `Host` section next to `Values`, preserving any existing settings:

```json
"Host": {
  "CORS": "http://127.0.0.1:5173,http://localhost:5173,https://localhost"
}
```

`local.settings.json.example` contains a configuration template for a fresh checkout, with timers disabled. Do not overwrite existing settings with it. When `UseDevelopmentStorage=true`, run a local Azurite instance. Requirements: .NET 10 SDK and Azure Functions Core Tools v4.

From the repository root, run `scripts\local\Start-LocalFunctionApp.ps1`. The script starts Azurite in the background and the Function App at `http://127.0.0.1:7071`. When it finishes, it also stops Azurite if it started it itself.

Endpoint contract:

```http
GET /api/inbox/a9cea60dda62442e
```

`200` response: `[{ "id": "notion-page-id", "name": "My thought" }]`. The list contains every result page, ordered from newest to oldest by creation time.

```http
POST /api/inbox/a9cea60dda62442e
Content-Type: application/json

{ "name": "My thought" }
```

`201` response: `{ "id": "notion-page-id", "name": "My thought" }`. The backend trims leading and trailing whitespace and requires 1–2,000 characters. `400` means invalid data, `503` means missing configuration, `502` means a Notion communication/response error, and `504` means timeout. Errors have the form `{ "error": "..." }` and do not expose Notion responses or secrets. POST requests are not retried automatically; if confirmation is lost, check the list before submitting again.

```http
PATCH /api/inbox/a9cea60dda62442e/{notion-page-id}
Content-Type: application/json

{ "name": "Corrected thought" }
```

`200` response: `{ "id": "notion-page-id", "name": "Corrected thought" }`. The endpoint updates only pages belonging to Inbox; it returns `404` for a foreign or deleted item.

```http
DELETE /api/inbox/a9cea60dda62442e/{notion-page-id}
```

`204` response. The endpoint moves only an Inbox item to the Notion trash; it returns `404` for a foreign or already deleted item.

```http
POST /api/inbox/a9cea60dda62442e/{notion-page-id}/move-to-tasks
```

`200` response: `{ "taskId": "notion-page-id" }`. The endpoint creates a task with the same name in the `Zadania` database, status `Do zrobienia`, and a `Zaplanowane na` date equal to the current day in `Scheduler:TimeZone`. The project property is not sent, so it remains empty. The Inbox item is archived only after the task is successfully created. If archiving fails, the item remains in Inbox and the response indicates that the task may already exist; retrying can therefore create a duplicate.

In Azure, set `Notion__InboxDataSourceId` through the `Deploy Function App (dev)` workflow. The configured Android origin is `https://localhost`. For a local frontend using Azure, also add `http://127.0.0.1:5173` to the Function App CORS configuration. CORS applies to the entire Function App; preserve the existing origins.

**MVP:** the endpoints are anonymous. The random path segment makes guessing harder, but it is visible in the APK and network traffic. Anyone who knows the URL can read and add items. The TODO for production authentication is in `NotionInboxItemFunctions.cs` and `src/api/inbox.ts`. Never put a Function App key or Notion token in the application.

## 2. Local API profiles

The API address is selected before building the APK and becomes embedded in it. Private profile files are ignored by Git:

- `.env.local-emulator` — local Function App for the Android emulator. It uses `http://10.0.2.2:7071`, the address through which the emulator reaches Windows.
- `.env.development-phone` — deployed development Function App available over HTTPS to a physical phone.

For first use, create both files from their templates:

```powershell
cd NotionManagementApp
Copy-Item .env.local-emulator.example .env.local-emulator
Copy-Item .env.development-phone.example .env.development-phone
```

Set the actual public Function App address in `development-phone`. `VITE_*` values are public and embedded in the APK, so never place tokens or Function App keys in them.

## 3. Build an APK

The `android/` project is committed to the repository, so do not run `cap add android`. The build script installs npm dependencies if needed, builds the frontend with the selected profile, runs `cap sync android`, builds a debug APK through the Gradle Wrapper, and opens the output folder in Windows Explorer. It requires JDK 21 in `JAVA_HOME`; Android Studio's bundled JBR currently uses Java 25, which is incompatible with the Gradle version in use.

For a local APK to update an APK built by GitHub Actions, both builds must use the same key. After creating a development keystore, copy `android/signing.properties.example` to `android/signing.properties` and enter that keystore's passwords and alias. The file and key are ignored by Git. Without this file, the local build still uses the default debug key and cannot update an APK signed by the pipeline.

Build for a local emulator — start `Start-LocalFunctionApp.ps1` first:

```powershell
.\scripts\local\Build-LocalEmulatorApk.ps1
```

Build for a physical phone using the development HTTPS API:

```powershell
.\scripts\local\Build-DevelopmentPhoneApk.ps1
```

The results are copied to `artifacts\android` at the repository root:

- `NotionManagementApp-localemulator-debug.apk`
- `NotionManagementApp-developmentphone-debug.apk`

You can drag the APK onto a running Android Studio emulator or install it manually on a phone. `adb` must be in `PATH` (usually `%ANDROID_HOME%\platform-tools`).

## 4. Frontend in a browser

To work with Vite in a browser, temporarily set the local API address for the current PowerShell session and start the server:

```powershell
cd NotionManagementApp
$env:VITE_INBOX_API_URL = 'http://127.0.0.1:7071/api/inbox/a9cea60dda62442e'
npm run dev
```

Open `http://127.0.0.1:5173`. If you use the deployed API, set its full HTTPS address instead. The preview uses port 4173, which must also be allowed in the local Function App CORS settings.
