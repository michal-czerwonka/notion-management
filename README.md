# NotionManagement

Solution for Notion-related automation.

Current projects:

- `NotionManagementFunctionApp` - Azure Functions 4 app using the C# isolated worker model on .NET 10.

The timer is configured through `Scheduler:Schedule`. The sample value runs every day at 08:00 UTC:

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
- `Scheduler:TimeZone` - timezone used for selecting "today"; currently `UTC`.
- `Tasks:FilePath` - task definition file path; defaults to `CreateNotionTasks/tasks.json`.

For Azure app settings, use the equivalent environment variable names, for example `Notion__Token` and `Notion__DataSourceId`.

Do not log tokens, credentials, connection strings, or Authorization headers.

Created tasks set these Notion properties:

- `Nazwa` - task name.
- `Zaplanowane na` - scheduled date from `tasks.json`.
- `Status` - `Do zrobienia`.

`Created at` is a Notion `created_time` property, so Notion fills it automatically when the page is created.

## Deployment

Use `NotionManagementFunctionApp/deploy.ps1` from the function app directory. Fill the configuration section first, especially `SubscriptionId`, `FunctionAppName`, and `StorageAccountName`.

Do not write the Notion token into the script. Set it for the current PowerShell session:

```powershell
$env:NOTION_TOKEN = "secret_xxx"
.\deploy.ps1
```

The script creates or reuses the resource group, storage account, and Windows Consumption Function App, configures app settings, builds the solution, and publishes the function app.
## Notion Task Notifications

`SendNotionTaskNotificationsFunction` runs on `Notifications:Schedule`. The default deployment setting is `0 0 12 * * *`, so notifications are sent daily at 12:00 UTC.

Notion is the source of truth. The function lists views for `Notion:DataSourceId`, finds the view named by `Notion:TodayViewName`, queries that view, and sends one summary notification. It does not reimplement due-date, overdue, status, or completion rules in C#.

If the Notion view contains no tasks, no notification is sent.

Manual notification test endpoint:

```http
POST /api/notifications/run
```

The endpoint uses `AuthorizationLevel.Function`, so in Azure call it with `x-functions-key`.

Required settings:

- `Notifications:Schedule` / `Notifications__Schedule` - timer schedule for notifications.
- `Notion:TodayViewName` / `Notion__TodayViewName` - defaults to `Na dzisiaj`.
- `Ntfy:BaseUrl` / `Ntfy__BaseUrl` - defaults to `https://ntfy.sh`.
- `Ntfy:Topic` / `Ntfy__Topic` - your ntfy topic.

### ntfy Setup From Scratch

1. You do not need an ntfy account for an unprotected topic on `https://ntfy.sh`.
2. Pick a long, hard-to-guess topic name. Treat it like a secret: anyone who knows an unprotected topic can publish to it and subscribe to it.
3. Install the ntfy app on your phone.
4. Subscribe to the same topic in the phone app. Topics are created on first use, so there is no separate topic creation step.
5. Locally, set these values in `local.settings.json`:

```json
"Notifications:Schedule": "0 0 12 * * *",
"Notion:TodayViewName": "Na dzisiaj",
"Ntfy:BaseUrl": "https://ntfy.sh",
"Ntfy:Topic": "your-private-topic"
```

6. In Azure Function App Settings, set:

```text
Notifications__Schedule = 0 0 12 * * *
Notion__TodayViewName = Na dzisiaj
Ntfy__BaseUrl = https://ntfy.sh
Ntfy__Topic = your-private-topic
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