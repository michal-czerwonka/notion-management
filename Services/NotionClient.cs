using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using NotionTaskScheduler.Models;

namespace NotionTaskScheduler.Services;

public sealed class NotionClient
{
    private const string NotionVersion = "2022-06-28";

    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _databaseId;

    public NotionClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.notion.com/v1/");
        _httpClient.DefaultRequestHeaders.Add("Notion-Version", NotionVersion);

        _token = configuration["Notion:Token"] ?? "";

        // TODO: uzupelnic w konfiguracji po utworzeniu docelowej bazy Notion.
        _databaseId = configuration["Notion:DatabaseId"] ?? "";
    }

    public async Task CreateTaskAsync(DueTask task, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            throw new InvalidOperationException("Missing Notion:Token configuration.");
        }

        if (string.IsNullOrWhiteSpace(_databaseId) || _databaseId.StartsWith("TODO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Missing Notion:DatabaseId configuration.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "pages")
        {
            Content = JsonContent.Create(new
            {
                parent = new
                {
                    database_id = _databaseId
                },
                properties = new
                {
                    Name = new
                    {
                        title = new[]
                        {
                            new
                            {
                                text = new
                                {
                                    content = task.Name
                                }
                            }
                        }
                    },
                    Due = new
                    {
                        date = new
                        {
                            start = task.Date.ToString("yyyy-MM-dd")
                        }
                    }
                }
            })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
