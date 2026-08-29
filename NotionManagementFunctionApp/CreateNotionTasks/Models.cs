namespace NotionManagementFunctionApp.CreateNotionTasks;

public sealed class RunNotionTasksRequest
{
    public string? Date { get; set; }
}

public sealed class ScheduledTask
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public List<string> Dates { get; set; } = [];
}

public sealed class TasksFile
{
    public int Version { get; set; }

    public List<ScheduledTask> Tasks { get; set; } = [];
}

public sealed record DueTask(string Id, string Name, DateOnly Date);

public sealed record RunNotionTasksResult(
    DateOnly Date,
    int Found,
    int Created,
    int Failed,
    IReadOnlyList<CreatedNotionTask> CreatedTasks,
    IReadOnlyList<string> Errors);

public sealed record CreatedNotionTask(
    string TaskId,
    DateOnly Date,
    string PageId,
    string? Url,
    string? PublicUrl,
    DateTimeOffset? CreatedTime,
    string? ParentType,
    string? ParentId);

public sealed record NotionCreatePageResult(
    string PageId,
    string? Url,
    string? PublicUrl,
    DateTimeOffset? CreatedTime,
    string? ParentType,
    string? ParentId);