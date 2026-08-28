namespace NotionTaskScheduler.Models;

public sealed class ScheduledTask
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public List<string> Dates { get; set; } = [];
}
