using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace NotionManagementFunctionApp.NotionInboxItems;

public sealed class NotionInboxItemsClient
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _dataSourceId;

    public NotionInboxItemsClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        NotionApi.Configure(_httpClient);
        _token = configuration["Notion:Token"] ?? "";
        _dataSourceId = configuration["Notion:InboxDataSourceId"] ?? "";
    }

    public async Task<IReadOnlyList<NotionInboxItem>> GetItemsAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var items = new List<NotionInboxItem>();
        string? cursor = null;
        do
        {
            var query = new Dictionary<string, object>
            {
                ["page_size"] = 100,
                ["sorts"] = new[] { new { timestamp = "created_time", direction = "descending" } }
            };
            if (cursor is not null)
            {
                query["start_cursor"] = cursor;
            }

            using var request = NotionApi.CreateRequest(HttpMethod.Post,
                $"data_sources/{Uri.EscapeDataString(_dataSourceId)}/query", _token,
                JsonContent.Create(query));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            foreach (var page in root.GetProperty("results").EnumerateArray())
            {
                items.Add(ReadItem(page));
            }

            cursor = root.GetProperty("has_more").GetBoolean()
                ? root.GetProperty("next_cursor").GetString()
                    ?? throw new JsonException("Missing Notion pagination cursor.")
                : null;
        }
        while (cursor is not null);

        return items;
    }

    public async Task<NotionInboxItem> CreateItemAsync(string name, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var request = NotionApi.CreateRequest(HttpMethod.Post, "pages", _token,
            JsonContent.Create(new
            {
                parent = new { type = "data_source_id", data_source_id = _dataSourceId },
                properties = new Dictionary<string, object> { ["Nazwa"] = NotionApi.TitleProperty(name) }
            }));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return ReadItem(document.RootElement);
    }

    private static NotionInboxItem ReadItem(JsonElement page) => new(
        page.GetProperty("id").GetString() ?? throw new JsonException("Missing Notion page id."),
        NotionApi.ReadTitle(page, "Nazwa") ?? "");

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            throw new InvalidOperationException("Missing Notion:Token configuration.");
        }

        if (string.IsNullOrWhiteSpace(_dataSourceId) || _dataSourceId.StartsWith("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Missing Notion:InboxDataSourceId configuration.");
        }
    }
}
