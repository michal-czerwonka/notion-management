# NotionManagement

Solution for Notion-related automation.

Current projects:

- `NotionManagementFunctionApp` - Azure Functions 4 app using the C# isolated worker model on .NET 10.
- `NotionManagementApp` - React + TypeScript + Capacitor Android app; independent npm project, intentionally outside the .NET solution.

Notion Inbox MVP: anonymous `GET` and `POST /api/inbox/a9cea60dda62442e`, backed by `Notion:InboxDataSourceId`. See [Inbox setup, local frontend, Android and APK instructions](NotionManagementApp/README.md). The random route is temporary obscurity, not authentication; the app contains no Notion token or Function App key.

## Required local tools

Docker is not required for the standard local workflow. Install the following tools directly on Windows and make their commands available on `PATH`.

### Function App and local services

- **.NET SDK 10** — builds and runs `NotionManagementFunctionApp`.
- **Azure Functions Core Tools v4** — provides `func start` for local Functions and `func azure functionapp publish` for deployment.
- **Azurite** — local Azure Storage emulator required by `AzureWebJobsStorage=UseDevelopmentStorage=true`. Start it before running the Function App. It can be installed with `npm install --global azurite`.
- **Node.js 22 or newer (LTS recommended)** — supplies `npm` for the web/mobile project and for installing Azurite. The project-local dependencies install with `npm ci`; do not install Vite or Capacitor globally.

### Notion Management Android application

- **Android Studio** — opens, builds, and runs the native Android project.
- **Android SDK Platform 36 and Android SDK Build-Tools** — installed through Android Studio's SDK Manager.
- **Android SDK Platform-Tools** — supplies `adb` for installing the debug APK on a phone or emulator.
- **JDK 21** — required by the current Gradle/Android build. Configure `JAVA_HOME` to this JDK when running the build script. The currently installed Android Studio bundled JBR is Java 25 and is not compatible with this Gradle version.

Gradle does not need a separate installation because the project includes the Gradle Wrapper. For command-line Android builds, configure `ANDROID_HOME` to the Android SDK location and add `%ANDROID_HOME%\platform-tools` to `PATH`.

### Deployment to Azure

- **PowerShell** — runs `NotionManagementFunctionApp/deploy.ps1`.
- **Azure CLI (`az`)** — authenticates with Azure, configures Function App settings and CORS, and reads Azure resource data used by the deployment script.
- **.NET SDK 10** and **Azure Functions Core Tools v4** — also required by the deployment script to build and publish the Function App.
- **Azure account with access to the existing resource group, Function App and Application Insights resource** — required by `az login` and the script. The script does not create Azure infrastructure.

Docker remains optional, for isolated builds only. It is not needed to run the Function App, frontend, Azurite, Android Studio, or deployment workflow locally.

Timers are evaluated in the Function App timezone configured by `WEBSITE_TIME_ZONE`. For this project, Azure uses `Central European Standard Time`, which corresponds to Europe/Warsaw. The task creation timer is configured through `Scheduler:Schedule`:

```json
"Scheduler:Schedule": "0 0 8 * * *"
```

Task definitions live in `NotionManagementFunctionApp/CreateNotionTasks/tasks.json`, which is copied to build output and publish artifacts. Dates inside task definitions use `dd.MM`, for example `30.08`; the app uses the current run year when creating the Notion date. Invalid date entries such as `29.02` or `13.45` are ignored.

Tasks can also include calendar rules. `dates` and `rules` complement each other; if either matches the run date, the task is created once.

```json
{
  "id": "pay-bills",
  "name": "Zaplacic rachunki",
  "dates": ["30.08"],
  "rules": [
    { "type": "daily" },
    { "type": "weekly", "dayOfWeek": "Monday" },
    { "type": "monthly", "day": 10 }
  ]
}
```

## Manual Run

Run the same workflow manually with:

```http
POST /api/run
```

An empty body runs for the current scheduler date. You can also provide an explicit date:

```json
{
  "date": "2026-09-10"
}
```

## Configuration

Use `local.settings.json` locally. Do not commit `local.settings.json`.

Required settings:

- `Notion:Token` - Notion integration token, read through `IConfiguration`.
- `Notion:DataSourceId` - target Notion data source id for the `Zadania` database.
- `Scheduler:Schedule` - NCRONTAB expression for the timer trigger.
- `Scheduler:TimeZone` - timezone used by task creation code for selecting "today"; currently `UTC`. Timer schedules themselves use `WEBSITE_TIME_ZONE`.
- `Tasks:FilePath` - task definition file path; defaults to `CreateNotionTasks/tasks.json`.

For Azure app settings, use the equivalent environment variable names, for example `Notion__Token` and `Notion__DataSourceId`.

Do not log tokens, credentials, connection strings, or Authorization headers.

Created tasks set these Notion properties:

- `Nazwa` - task name.
- `Zaplanowane na` - scheduled date from `tasks.json`.
- `Status` - `Do zrobienia`.

`Created at` is a Notion `created_time` property, so Notion fills it automatically when the page is created.

## Deployment

Use the manual GitHub Actions workflows for development deployments. Their configuration, secret setup, Azure OIDC setup and detailed build/deployment process are documented in [deployment/README.md](deployment/README.md).

For a manual deployment from Windows, use `deployment/Deploy-FunctionApp.ps1`. It reads non-secret configuration from `deployment/config/development.json` and prompts for `NOTION_TOKEN` and `NTFY_TOPIC` when they are not present in the current environment.

Do not write the Notion token into the script. Set it for the current PowerShell session:

```powershell
$env:NOTION_TOKEN = "secret_xxx"
.\deployment\Deploy-FunctionApp.ps1
```

The script does not create Azure infrastructure. The resource group, storage account, Function App, Log Analytics workspace, and Application Insights resource must already exist. The script checks only the existing Function App, configures app settings, builds the solution, and publishes the function app.

## Application Insights

Azure resources are created manually. The deployment workflow and `deployment/Deploy-FunctionApp.ps1` expect these resources to already exist:

- Log Analytics workspace: `notion-management-law`.
- Workspace-based Application Insights resource: `notion-management-ai`.

The script does not query Application Insights during deployment. Set the connection string manually in Azure Function App Settings, or provide it only when you want the script to update it:

```powershell
$env:APPLICATIONINSIGHTS_CONNECTION_STRING = "InstrumentationKey=...;IngestionEndpoint=..."
```

The deployment workflow and `deployment/Deploy-FunctionApp.ps1` leave the existing Application Insights app setting unchanged.

The Function App sends application logs directly from the isolated worker to Application Insights. The configuration keeps `ILogger` information logs and exceptions, while filtering dependency and request telemetry emitted by the worker. Host-level dependency tracking and performance counter collection are disabled in `host.json` to keep telemetry volume low.

Handled exceptions in manual HTTP triggers are explicitly sent with `TelemetryClient.TrackException`, so they appear as real records in the `exceptions` table instead of only as failed requests or gRPC worker traces.

Useful KQL queries in Application Insights Logs:

```kusto
traces
| order by timestamp desc
| take 50
```

```kusto
exceptions
| order by timestamp desc
| take 50
```

```kusto
exceptions
| where customDimensions["FunctionName"] in ("CreateNotionTasksFunctionHttp", "SendNotionTaskNotificationsFunctionHttp")
| order by timestamp desc
| take 50
```

```kusto
traces
| where severityLevel >= 1
| order by timestamp desc
| take 100
```
## Notion Task Notifications

`SendNotionTaskNotificationsFunction` runs on `Notifications:Schedule`. The default deployment setting is `0 0 15,23 * * *`, so notifications are sent daily at 15:00 and 23:00 Europe/Warsaw time.

Notion is the source of truth. The function queries the configured view id from `Notion:TodayViewId` and sends one summary notification. If `Notion:TodayViewId` is empty, it falls back to listing views for `Notion:DataSourceId` and finding the view named by `Notion:TodayViewName`. It does not reimplement due-date, overdue, status, or completion rules in C#.

If the Notion view contains no tasks at a scheduled notification time, the function sends `brak zadań, trzeba uzupełnić Notion`.

Manual notification test endpoint:

```http
POST /api/notifications/run
```

The endpoint uses `AuthorizationLevel.Function`, so in Azure call it with `x-functions-key`.

Required settings:

- `Notifications:Schedule` / `Notifications__Schedule` - timer schedule for notifications; defaults to `0 0 15,23 * * *`.
- `Notion:TodayViewId` / `Notion__TodayViewId` - Notion view id for `Na dzisiaj`; preferred over name-based lookup.
- `Notion:TodayViewName` / `Notion__TodayViewName` - fallback view name; defaults to `Na dzisiaj`.
- `WEBSITE_TIME_ZONE` - `Central European Standard Time` in Azure, matching Europe/Warsaw for Windows Function Apps.
- `Ntfy:BaseUrl` / `Ntfy__BaseUrl` - defaults to `https://ntfy.sh`.
- `Ntfy:Topic` / `Ntfy__Topic` - your ntfy topic.

### ntfy Setup From Scratch

1. You do not need an ntfy account for an unprotected topic on `https://ntfy.sh`.
2. Pick a long, hard-to-guess topic name. Treat it like a secret: anyone who knows an unprotected topic can publish to it and subscribe to it.
3. Install the ntfy app on your phone.
4. Subscribe to the same topic in the phone app. Topics are created on first use, so there is no separate topic creation step.
5. Locally, set these values in `local.settings.json`:

```json
"Notifications:Schedule": "0 0 15,23 * * *",
"Notion:TodayViewId": "34e8bc0919d380c595f1000c54fb8ad5",
"Notion:TodayViewName": "Na dzisiaj",
"Ntfy:BaseUrl": "https://ntfy.sh",
"Ntfy:Topic": "your-private-topic",
"WEBSITE_TIME_ZONE": "Central European Standard Time"
```

6. In Azure Function App Settings, set:

```text
Notifications__Schedule = 0 0 15,23 * * *
Notion__TodayViewId = 34e8bc0919d380c595f1000c54fb8ad5
Notion__TodayViewName = Na dzisiaj
Ntfy__BaseUrl = https://ntfy.sh
Ntfy__Topic = your-private-topic
WEBSITE_TIME_ZONE = Central European Standard Time
```

7. To test ntfy manually before using the Function App:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri "https://ntfy.sh/your-private-topic" `
  -Headers @{ Title = "Zadania na dzisiaj" } `
  -Body "Zadania na dzisiaj: 1`n`n• Test"
```

8. To deploy these settings manually, set the secrets in the current PowerShell session:

```powershell
$env:NOTION_TOKEN = "secret_xxx"
.\deployment\Deploy-FunctionApp.ps1
```

Set `NTFY_TOPIC` in the current PowerShell session before running the script.
