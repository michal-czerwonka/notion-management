using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NotionTaskScheduler.Models;

namespace NotionTaskScheduler.Services;

public sealed class NotionClient
{
    private const string NotionVersion = "2026-03-11";
    private const string TodoStatusName = "Do zrobienia";

    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _dataSourceId;

    public NotionClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.notion.com/v1/");
        _httpClient.DefaultRequestHeaders.Add("Notion-Version", NotionVersion);

        _token = configuration["Notion:Token"] ?? "";

        // TODO: uzupelnic w konfiguracji po utworzeniu docelowej bazy Notion.
        _dataSourceId = configuration["Notion:DataSourceId"] ?? "";
    }

    public async Task<NotionCreatePageResult> CreateTaskAsync(DueTask task, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            throw new InvalidOperationException("Missing Notion:Token configuration.");
        }

        if (string.IsNullOrWhiteSpace(_dataSourceId) || _dataSourceId.StartsWith("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Missing Notion:DataSourceId configuration.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "pages")
        {
            Content = CreateJsonContent(new Dictionary<string, object?>
            {
                ["parent"] = new Dictionary<string, object?>
                {
                    ["type"] = "data_source_id",
                    ["data_source_id"] = _dataSourceId
                },
                ["properties"] = new Dictionary<string, object?>
                {
                    ["Nazwa"] = new Dictionary<string, object?>
                    {
                        ["type"] = "title",
                        ["title"] = new object[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["type"] = "text",
                                ["text"] = new Dictionary<string, object?>
                                {
                                    ["content"] = task.Name
                                }
                            }
                        }
                    },
                    ["Zaplanowane na"] = new Dictionary<string, object?>
                    {
                        ["type"] = "date",
                        ["date"] = new Dictionary<string, object?>
                        {
                            ["start"] = task.Date.ToString("yyyy-MM-dd")
                        }
                    },
                    ["Status"] = new Dictionary<string, object?>
                    {
                        ["type"] = "status",
                        ["status"] = new Dictionary<string, object?>
                        {
                            ["name"] = TodoStatusName
                        }
                    }
                }
            })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Notion API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }

        return ParseCreatePageResponse(responseBody);
    }

    private static StringContent CreateJsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static NotionCreatePageResult ParseCreatePageResponse(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        var pageId = GetString(root, "id") ?? throw new InvalidOperationException("Notion response did not include page id.");
        var createdTime = GetDateTimeOffset(root, "created_time");
        var parentType = GetProperty(root, "parent") is { } parent
            ? GetString(parent, "type")
            : null;
        var parentId = parentType is null || GetProperty(root, "parent") is not { } parentElement
            ? null
            : GetString(parentElement, parentType);

        return new NotionCreatePageResult(
            pageId,
            GetString(root, "url"),
            GetString(root, "public_url"),
            createdTime,
            parentType,
            parentId);
    }

    private static JsonElement? GetProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetDateTimeOffset()
            : null;
    }
}
