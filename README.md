# NotionManagement

Solution for Notion-related automation.

Current projects:

- `NotionManagementFunctionApp` - Azure Functions 4 app using the C# isolated worker model on .NET 10.

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

Use `NotionManagementFunctionApp/deploy.ps1` from the function app directory. Fill the configuration section first, especially `SubscriptionId`, `ResourceGroupName`, `FunctionAppName`, and `ApplicationInsightsName`.

Do not write the Notion token into the script. Set it for the current PowerShell session:

```powershell
$env:NOTION_TOKEN = "secret_xxx"
.\deploy.ps1
```

The script does not create Azure infrastructure. The resource group, storage account, Function App, Log Analytics workspace, and Application Insights resource must already exist. The script checks the existing Function App and Application Insights resource, configures app settings, builds the solution, and publishes the function app.

## Application Insights

Azure resources are created manually. `deploy.ps1` expects these resources to already exist:

- Log Analytics workspace: `notion-management-law`.
- Workspace-based Application Insights resource: `notion-management-ai`.

The script reads the existing Application Insights connection string from Azure and stores it in the Function App setting:

```text
APPLICATIONINSIGHTS_CONNECTION_STRING = <read from Azure during deployment>
```

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

8. To deploy these settings with `deploy.ps1`, set the Notion secret in the current PowerShell session:

```powershell
$env:NOTION_TOKEN = "secret_xxx"
.\deploy.ps1
```

Fill `$NtfyTopic` in `deploy.ps1` before running it.
