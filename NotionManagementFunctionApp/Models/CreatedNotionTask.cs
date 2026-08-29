namespace NotionManagementFunctionApp.Models;

public sealed record CreatedNotionTask(
    string TaskId,
    DateOnly Date,
    string PageId,
    string? Url,
    string? PublicUrl,
    DateTimeOffset? CreatedTime,
    string? ParentType,
    string? ParentId);
