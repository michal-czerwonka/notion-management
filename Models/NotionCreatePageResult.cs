namespace NotionTaskScheduler.Models;

public sealed record NotionCreatePageResult(
    string PageId,
    string? Url,
    string? PublicUrl,
    DateTimeOffset? CreatedTime,
    string? ParentType,
    string? ParentId);
