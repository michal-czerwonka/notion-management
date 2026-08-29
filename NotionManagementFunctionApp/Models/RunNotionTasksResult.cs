namespace NotionManagementFunctionApp.Models;

public sealed record RunNotionTasksResult(
    DateOnly Date,
    int Found,
    int Created,
    int Failed,
    IReadOnlyList<CreatedNotionTask> CreatedTasks,
    IReadOnlyList<string> Errors);
