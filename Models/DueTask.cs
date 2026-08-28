namespace NotionTaskScheduler.Models;

public sealed record DueTask(string Id, string Name, DateOnly Date);
