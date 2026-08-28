# NotionTaskScheduler

Small Azure Functions 4 app using the C# isolated worker model on .NET 10.

The timer is configured through `Scheduler:Schedule`. The sample value runs every day at 08:00 UTC:

```json
"Scheduler:Schedule": "0 0 8 * * *"
```

Task definitions live in `tasks.json`, which is copied to build output and publish artifacts.

## Configuration

Use `local.settings.json` locally, based on `local.settings.example.json`. Do not commit `local.settings.json`.

Required settings:

- `Notion:Token` - Notion integration token, read through `IConfiguration`.
- `Notion:DatabaseId` - TODO: fill this after the target Notion database is created.
- `Scheduler:Schedule` - NCRONTAB expression for the timer trigger.
- `Scheduler:TimeZone` - timezone used for selecting "today"; currently `UTC`.
- `Tasks:FilePath` - task definition file path; defaults to `tasks.json`.

For Azure app settings, use the equivalent environment variable names, for example `Notion__Token` and `Notion__DatabaseId`.

Do not log tokens, credentials, connection strings, or Authorization headers.
