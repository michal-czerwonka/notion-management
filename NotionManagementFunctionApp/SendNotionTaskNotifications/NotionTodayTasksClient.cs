using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed class NotionTodayTasksClient
{
    private const string TitlePropertyName = "Nazwa";
    private const string StatusPropertyName = "Status";
    private const string ProjectPropertyName = "Projekt";

    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _dataSourceId;
    private readonly string _viewId;
    private readonly string _viewName;
    private readonly ILogger<NotionTodayTasksClient> _logger;

    public NotionTodayTasksClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NotionTodayTasksClient> logger)
    {
        _httpClient = httpClient;
        NotionApi.Configure(_httpClient);

        _token = configuration["Notion:Token"] ?? "";
        _dataSourceId = configuration["Notion:DataSourceId"] ?? "";
        _viewId = configuration["Notion:TodayViewId"] ?? "";
        _viewName = configuration["Notion:TodayViewName"] ?? "Na dzisiaj";
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotionTodayTask>> GetTodayViewTasksAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        if (!string.IsNullOrWhiteSpace(_viewId))
        {
            _logger.LogInformation("Querying configured Notion today view id '{ViewId}'.", _viewId);
            return await QueryViewTasksAsync(_viewId, cancellationToken);
        }

        var view = await FindViewByNameAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Notion view '{_viewName}' was not found for configured data source.");

        _logger.LogInformation("Found Notion today view '{ViewName}' with id '{ViewId}'.", view.Name, view.Id);
        return await QueryViewTasksAsync(view.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<TodayTaskStatus>> GetStatusOptionsAsync(CancellationToken cancellationToken)
    {
        EnsureTaskDataSourceConfigured();
        using var request = CreateRequest(HttpMethod.Get, $"data_sources/{Uri.EscapeDataString(_dataSourceId)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "retrieve task data source");
        using var document = JsonDocument.Parse(body);
        var options = document.RootElement.GetProperty("properties").GetProperty(StatusPropertyName)
            .GetProperty("status").GetProperty("options");
        return options.EnumerateArray().Select(option => new TodayTaskStatus(
            GetString(option, "name") ?? throw new InvalidOperationException("Task status is missing a name."),
            GetString(option, "color") ?? "default")).ToArray();
    }

    public async Task UpdateStatusAsync(string id, string status, CancellationToken cancellationToken)
    {
        var statuses = await GetStatusOptionsAsync(cancellationToken);
        if (!statuses.Any(item => item.Name == status)) throw new ArgumentException("Unknown status.");
        await EnsureTaskBelongsAsync(id, cancellationToken);
        using var request = CreateRequest(HttpMethod.Patch, $"pages/{Uri.EscapeDataString(id)}", new
        {
            properties = new Dictionary<string, object> { [StatusPropertyName] = new { status = new { name = status } } }
        });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "update task status");
    }

    public async Task ArchiveAsync(string id, CancellationToken cancellationToken)
    {
        EnsureTaskDataSourceConfigured();
        await EnsureTaskBelongsAsync(id, cancellationToken);
        using var request = CreateRequest(HttpMethod.Patch, $"pages/{Uri.EscapeDataString(id)}", new { in_trash = true });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "archive task");
    }

    private async Task<NotionViewReference?> FindViewByNameAsync(CancellationToken cancellationToken)
    {
        var viewIds = await ListViewIdsAsync(cancellationToken);

        foreach (var viewId in viewIds)
        {
            var view = await TryRetrieveViewForNameSearchAsync(viewId, cancellationToken);
            if (view is not null && string.Equals(view.Name, _viewName, StringComparison.Ordinal))
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

    private async Task<NotionViewReference?> TryRetrieveViewForNameSearchAsync(string viewId, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"views/{Uri.EscapeDataString(viewId)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (IsUnsupportedViewReference(response, responseBody))
            {
                _logger.LogDebug("Skipping unsupported Notion view reference '{ViewId}' while searching for view '{ViewName}'.", viewId, _viewName);
                return null;
            }

            EnsureSuccess(response, responseBody, "retrieve Notion view");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        return new NotionViewReference(
            GetString(root, "id") ?? viewId,
            GetString(root, "name") ?? "");
    }

    private async Task<IReadOnlyList<NotionTodayTask>> QueryViewTasksAsync(string viewId, CancellationToken cancellationToken)
    {
        var pageIds = await QueryViewPageIdsAsync(viewId, cancellationToken);
        var tasks = new List<NotionTodayTask>(pageIds.Count);
        var projects = new Dictionary<string, string>();

        foreach (var pageId in pageIds)
        {
            tasks.Add(await RetrieveTaskDetailsAsync(pageId, projects, cancellationToken));
        }

        _logger.LogInformation("Retrieved {TaskCount} task detail(s) from Notion today view id '{ViewId}'.", tasks.Count, viewId);
        return tasks;
    }

    private async Task<IReadOnlyList<string>> QueryViewPageIdsAsync(string viewId, CancellationToken cancellationToken)
    {
        string? queryId = null;

        try
        {
            var pageIds = new List<string>();
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
                AddPageIdsFromQueryResponse(root, pageIds);

                startCursor = GetBool(root, "has_more") ? GetString(root, "next_cursor") : null;
            }
            while (!string.IsNullOrWhiteSpace(startCursor));

            _logger.LogInformation("Retrieved {PageCount} page id(s) from Notion today view id '{ViewId}'.", pageIds.Count, viewId);
            return pageIds;
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

    private async Task<NotionTodayTask> RetrieveTaskDetailsAsync(string pageId, Dictionary<string, string> projects, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"pages/{Uri.EscapeDataString(pageId)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, responseBody, "retrieve Notion page details");

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;

        var name = NotionApi.ReadTitle(root, TitlePropertyName);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "(bez nazwy)";
        }

        var status = ReadStatus(root, StatusPropertyName) ?? "(bez statusu)";

        return new NotionTodayTask(pageId, name, status, await GetProjectsAsync(root, projects, cancellationToken), GetString(root, "url"));
    }

    private async Task<IReadOnlyList<string>> GetProjectsAsync(JsonElement task, Dictionary<string, string> projects, CancellationToken cancellationToken)
    {
        if (!task.GetProperty("properties").TryGetProperty(ProjectPropertyName, out var projectProperty) ||
            !projectProperty.TryGetProperty("relation", out var relation)) return [];
        var names = new List<string>();
        foreach (var relationItem in relation.EnumerateArray())
        {
            var id = GetString(relationItem, "id");
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!projects.TryGetValue(id, out var name))
            {
                name = await GetPageTitleAsync(id, cancellationToken);
                projects[id] = name;
            }
            names.Add(name);
        }
        return names;
    }

    private async Task<string> GetPageTitleAsync(string id, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"pages/{Uri.EscapeDataString(id)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "retrieve project");
        using var document = JsonDocument.Parse(body);
        foreach (var property in document.RootElement.GetProperty("properties").EnumerateObject())
        {
            if (GetString(property.Value, "type") == "title") return string.Concat(property.Value.GetProperty("title").EnumerateArray().Select(part => GetString(part, "plain_text")));
        }
        return "(unnamed project)";
    }

    private async Task EnsureTaskBelongsAsync(string id, CancellationToken cancellationToken)
    {
        EnsureTaskDataSourceConfigured();
        using var request = CreateRequest(HttpMethod.Get, $"pages/{Uri.EscapeDataString(id)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) throw new KeyNotFoundException("Task was not found.");
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response, body, "retrieve task");
        using var document = JsonDocument.Parse(body);
        var parent = document.RootElement.GetProperty("parent");
        if (GetString(parent, "type") != "data_source_id" || GetString(parent, "data_source_id") != _dataSourceId) throw new KeyNotFoundException("Task was not found.");
    }

    private void EnsureTaskDataSourceConfigured()
    {
        if (string.IsNullOrWhiteSpace(_token) || string.IsNullOrWhiteSpace(_dataSourceId)) throw new InvalidOperationException("Missing Notion task configuration.");
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

    private static void AddPageIdsFromQueryResponse(JsonElement root, List<string> pageIds)
    {
        foreach (var result in root.GetProperty("results").EnumerateArray())
        {
            var pageId = GetString(result, "id");
            if (!string.IsNullOrWhiteSpace(pageId))
            {
                pageIds.Add(pageId);
            }
        }
    }

    private static string? ReadStatus(JsonElement page, string propertyName)
    {
        if (!page.TryGetProperty("properties", out var properties) ||
            !properties.TryGetProperty(propertyName, out var statusProperty) ||
            !statusProperty.TryGetProperty("status", out var status) ||
            status.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return GetString(status, "name");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? body = null)
    {
        var request = NotionApi.CreateRequest(method, uri, _token);

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

        if (!string.IsNullOrWhiteSpace(_viewId))
        {
            return;
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

    private static bool IsUnsupportedViewReference(HttpResponseMessage response, string responseBody)
    {
        return (int)response.StatusCode == 400 &&
            responseBody.Contains("Unsupported view type", StringComparison.OrdinalIgnoreCase);
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
