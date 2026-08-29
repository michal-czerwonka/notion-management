namespace NotionTaskScheduler.Models;

public sealed record RunNotionTasksResult(
    DateOnly Date,
    int Found,
    int Created,
    int Failed,
    IReadOnlyList<string> Errors);
