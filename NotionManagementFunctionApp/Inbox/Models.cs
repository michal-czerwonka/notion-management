using System.Text.Json.Serialization;

namespace NotionManagementFunctionApp.Inbox;

public sealed record InboxItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);
public sealed record CreateInboxItemRequest(string? Name);
