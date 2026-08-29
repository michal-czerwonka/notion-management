namespace NotionManagementFunctionApp.Models;

public sealed class TasksFile
{
    public int Version { get; set; }

    public List<ScheduledTask> Tasks { get; set; } = [];
}
