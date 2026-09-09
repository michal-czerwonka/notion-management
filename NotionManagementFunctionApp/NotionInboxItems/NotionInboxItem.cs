using System.Text.Json.Serialization;

namespace NotionManagementFunctionApp.NotionInboxItems;

public sealed record NotionInboxItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);
public sealed record CreateNotionInboxItemRequest(string? Name);
public sealed record UpdateNotionInboxItemRequest(string? Name);
