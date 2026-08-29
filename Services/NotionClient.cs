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

    public async Task CreateTaskAsync(DueTask task, CancellationToken cancellationToken)
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
        response.EnsureSuccessStatusCode();
    }

    private static StringContent CreateJsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
