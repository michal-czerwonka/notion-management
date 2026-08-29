# NotionTaskScheduler

Small Azure Functions 4 app using the C# isolated worker model on .NET 10.

The timer is configured through `Scheduler:Schedule`. The sample value runs every day at 08:00 UTC:

```json
"Scheduler:Schedule": "0 0 8 * * *"
```

Task definitions live in `tasks.json`, which is copied to build output and publish artifacts.

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
- `Tasks:FilePath` - task definition file path; defaults to `tasks.json`.

For Azure app settings, use the equivalent environment variable names, for example `Notion__Token` and `Notion__DataSourceId`.

Do not log tokens, credentials, connection strings, or Authorization headers.

Created tasks set these Notion properties:

- `Nazwa` - task name.
- `Zaplanowane na` - scheduled date from `tasks.json`.
- `Status` - `Do zrobienia`.

`Created at` is a Notion `created_time` property, so Notion fills it automatically when the page is created.
