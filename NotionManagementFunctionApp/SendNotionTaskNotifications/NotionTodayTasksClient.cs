using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed class NotionTodayTasksClient
{
    private const string NotionVersion = "2026-03-11";
    private const string TitlePropertyName = "Nazwa";

    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _dataSourceId;
    private readonly string _viewName;
    private readonly ILogger<NotionTodayTasksClient> _logger;

    public NotionTodayTasksClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NotionTodayTasksClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.notion.com/v1/");
        _httpClient.DefaultRequestHeaders.Add("Notion-Version", NotionVersion);

        _token = configuration["Notion:Token"] ?? "";
        _dataSourceId = configuration["Notion:DataSourceId"] ?? "";
        _viewName = configuration["Notion:TodayViewName"] ?? "Na dzisiaj";
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotionTodayTask>> GetTodayViewTasksAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var view = await FindViewByNameAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Notion view '{_viewName}' was not found for configured data source.");

        return await QueryViewAsync(view.Id, cancellationToken);
    }

    private async Task<NotionViewReference?> FindViewByNameAsync(CancellationToken cancellationToken)
    {
        var viewIds = await ListViewIdsAsync(cancellationToken);

        foreach (var viewId in viewIds)
        {
            var view = await RetrieveViewAsync(viewId, cancellationToken);
            if (string.Equals(view.Name, _viewName, StringComparison.Ordinal))
            {
                return view;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<string>> ListViewIdsAsync(CancellationToken cancellationToken)
    {
        var viewIds = new List<string>();
        string? startCursor = null;

        do
        {
            var uri = new StringBuilder($"views?data_source_id={Uri.EscapeDataString(_dataSourceId)}");
            if (!string.IsNullOrWhiteSpace(startCursor))
            {
                uri.Append($"&start_cursor={Uri.EscapeDataString(startCursor)}");
            }

            using var request = CreateRequest(HttpMethod.Get, uri.ToString());
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccess(response, responseBody, "list Notion views");

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            foreach (var result in root.GetProperty("results").EnumerateArray())
            {
                var id = GetString(result, "id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    viewIds.Add(id);
                }
            }

            startCursor = GetBool(root, "has_more") ? GetString(root, "next_cursor") : null;
        }
        while (!string.IsNullOrWhiteSpace(startCursor));

        return viewIds;
    }

    private async Task<NotionViewReference> RetrieveViewAsync(string viewId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"views/{Uri.EscapeDataString(viewId)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, responseBody, "retrieve Notion view");

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        return new NotionViewReference(
            GetString(root, "id") ?? viewId,
            GetString(root, "name") ?? "");
    }

    private async Task<IReadOnlyList<NotionTodayTask>> QueryViewAsync(string viewId, CancellationToken cancellationToken)
    {
        string? queryId = null;

        try
        {
            var tasks = new List<NotionTodayTask>();
            string? startCursor = null;

            do
            {
                using var request = startCursor is null
                    ? CreateViewQueryRequest(viewId)
                    : CreateViewQueryPageRequest(viewId, queryId!, startCursor);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                EnsureSuccess(response, responseBody, "query Notion view");

                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                queryId ??= GetString(root, "id") ?? throw new InvalidOperationException("Notion view query response did not include query id.");
                AddTasksFromQueryResponse(root, tasks);

                startCursor = GetBool(root, "has_more") ? GetString(root, "next_cursor") : null;
            }
            while (!string.IsNullOrWhiteSpace(startCursor));

            return tasks;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(queryId))
            {
                try
                {
                    await DeleteViewQueryAsync(viewId, queryId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete cached Notion view query '{QueryId}'. It will expire automatically.", queryId);
                }
            }
        }
    }

    private HttpRequestMessage CreateViewQueryRequest(string viewId)
    {
        return CreateRequest(HttpMethod.Post, $"views/{Uri.EscapeDataString(viewId)}/queries", new Dictionary<string, object?>
        {
            ["page_size"] = 50
        });
    }

    private HttpRequestMessage CreateViewQueryPageRequest(string viewId, string queryId, string startCursor)
    {
        var uri = $"views/{Uri.EscapeDataString(viewId)}/queries/{Uri.EscapeDataString(queryId)}?page_size=50&start_cursor={Uri.EscapeDataString(startCursor)}";

        return CreateRequest(HttpMethod.Get, uri);
    }

    private async Task DeleteViewQueryAsync(string viewId, string queryId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"views/{Uri.EscapeDataString(viewId)}/queries/{Uri.EscapeDataString(queryId)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, responseBody, "delete Notion view query");
    }

    private static void AddTasksFromQueryResponse(JsonElement root, List<NotionTodayTask> tasks)
    {
        foreach (var result in root.GetProperty("results").EnumerateArray())
        {
            var pageId = GetString(result, "id");
            if (string.IsNullOrWhiteSpace(pageId))
            {
                continue;
            }

            var name = ReadTitle(result, TitlePropertyName);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "(bez nazwy)";
            }

            tasks.Add(new NotionTodayTask(pageId, name, GetString(result, "url")));
        }
    }

    private static string? ReadTitle(JsonElement page, string propertyName)
    {
        if (!page.TryGetProperty("properties", out var properties) ||
            !properties.TryGetProperty(propertyName, out var titleProperty) ||
            !titleProperty.TryGetProperty("title", out var titleValues))
        {
            return null;
        }

        var parts = titleValues
            .EnumerateArray()
            .Select(title => GetString(title, "plain_text"))
            .Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join("", parts);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        if (body is not null)
        {
            request.Content = CreateJsonContent(body);
        }

        return request;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            throw new InvalidOperationException("Missing Notion:Token configuration.");
        }

        if (string.IsNullOrWhiteSpace(_dataSourceId))
        {
            throw new InvalidOperationException("Missing Notion:DataSourceId configuration.");
        }

        if (string.IsNullOrWhiteSpace(_viewName))
        {
            throw new InvalidOperationException("Missing Notion:TodayViewName configuration.");
        }
    }

    private static StringContent CreateJsonContent(object payload)
    {
        var json = JsonSerializer.Serialize(payload);

        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static void EnsureSuccess(HttpResponseMessage response, string responseBody, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to {operation}. Notion API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool GetBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;
    }
}