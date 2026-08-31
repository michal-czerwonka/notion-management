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

Use `local.settings.json` locally, based on `local.settings.example.json`. Do not commit `local.settings.json`.

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