using System.Text.Json.Serialization;

namespace NotionManagementFunctionApp.NotionInboxItems;

public sealed record NotionInboxItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);
public sealed record CreateNotionInboxItemRequest(string? Name);
public sealed record UpdateNotionInboxItemRequest(string? Name);
public sealed record MoveNotionInboxItemResult(string TaskId);

public sealed class TaskCreatedInboxArchiveFailedException : Exception
{
    public TaskCreatedInboxArchiveFailedException(Exception innerException)
        : base("The task was created, but the Inbox item could not be archived.", innerException)
    {
    }
}
