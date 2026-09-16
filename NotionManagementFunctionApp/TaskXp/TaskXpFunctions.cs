using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Cosmos;
using NotionManagementFunctionApp.SendNotionTaskNotifications;

namespace NotionManagementFunctionApp.TaskXp;

public sealed class TaskXpFunctions(TaskXpService taskXp, NotionTodayTasksClient notion, IOptions<TaskXpOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("GetTaskXpTotal")]
    public async Task<HttpResponseData> TotalAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "task-xp/{segment}/total")] HttpRequestData request, string segment, CancellationToken cancellationToken)
    {
        if (!Authorize(segment)) return request.CreateResponse(HttpStatusCode.NotFound);
        try { return await JsonAsync(request, HttpStatusCode.OK, new { totalXp = await taskXp.GetTotalAsync(cancellationToken) }, cancellationToken); }
        catch (CosmosException) { return await JsonAsync(request, HttpStatusCode.ServiceUnavailable, new { error = "Task progress is temporarily unavailable." }, cancellationToken); }
    }

    [Function("GetTaskXpEvents")]
    public async Task<HttpResponseData> EventsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "task-xp/{segment}/events")] HttpRequestData request, string segment, CancellationToken cancellationToken)
    {
        if (!Authorize(segment)) return request.CreateResponse(HttpStatusCode.NotFound);
        var limit = 50;
        var values = ParseQuery(request.Url.Query);
        if (int.TryParse(values.GetValueOrDefault("limit"), out var parsed)) limit = parsed;
        if (limit is < 1 or > 100) return await JsonAsync(request, HttpStatusCode.BadRequest, new { error = "limit must be between 1 and 100." }, cancellationToken);
        try
        {
            var page = await taskXp.GetEventsAsync(limit, values.GetValueOrDefault("continuationToken"), cancellationToken);
            return await JsonAsync(request, HttpStatusCode.OK, new { events = page.Events, continuationToken = page.ContinuationToken }, cancellationToken);
        }
        catch (CosmosException) { return await JsonAsync(request, HttpStatusCode.BadRequest, new { error = "Invalid continuation token or unavailable task progress." }, cancellationToken); }
    }

    [Function("ReceiveNotionTaskXpWebhook")]
    public async Task<HttpResponseData> WebhookAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notion/task-xp-webhook")] HttpRequestData request, CancellationToken cancellationToken)
    {
        var raw = await new StreamReader(request.Body, Encoding.UTF8).ReadToEndAsync(cancellationToken);
        JsonDocument document;
        try { document = JsonDocument.Parse(raw); }
        catch (JsonException) { return request.CreateResponse(HttpStatusCode.BadRequest); }
        using (document)
        {
        var root = document.RootElement;
        if (root.TryGetProperty("verification_token", out var verificationToken)) return await JsonAsync(request, HttpStatusCode.OK, new { verificationToken = verificationToken.GetString() }, cancellationToken);
        if (!IsValidSignature(raw, request.Headers.TryGetValues("X-Notion-Signature", out var signatures) ? signatures.FirstOrDefault() : null)) return request.CreateResponse(HttpStatusCode.Unauthorized);
        if (!string.Equals(GetString(root, "type"), "page.properties_updated", StringComparison.Ordinal)) return request.CreateResponse(HttpStatusCode.NoContent);
        var statusPropertyId = await notion.GetStatusPropertyIdAsync(cancellationToken);
        if (!HasStatusChange(root, statusPropertyId)) return request.CreateResponse(HttpStatusCode.NoContent);
        var pageId = FindPageId(root);
        var eventId = GetString(root, "id");
        if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(eventId)) return request.CreateResponse(HttpStatusCode.NoContent);
        try
        {
            var snapshot = await notion.GetTaskSnapshotAsync(pageId, cancellationToken);
            var occurredAt = DateTimeOffset.TryParse(GetString(root, "timestamp") ?? GetString(root, "occurred_at"), out var timestamp) ? timestamp : snapshot.LastEditedAt;
            await taskXp.RecordAsync(snapshot, "notion", eventId, occurredAt, cancellationToken);
            return request.CreateResponse(HttpStatusCode.NoContent);
        }
        catch (KeyNotFoundException) { return request.CreateResponse(HttpStatusCode.NoContent); }
        catch (TaskXpConfigurationException) { return await JsonAsync(request, HttpStatusCode.ServiceUnavailable, new { error = "Task progress is unavailable." }, cancellationToken); }
        catch (CosmosException) { return await JsonAsync(request, HttpStatusCode.BadGateway, new { error = "Task progress could not be recorded." }, cancellationToken); }
        }
    }

    private bool Authorize(string segment) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(segment), Encoding.UTF8.GetBytes(options.Value.ReadRouteSegment));
    private bool IsValidSignature(string body, string? supplied)
    {
        var secret = options.Value.NotionWebhookVerificationToken;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(supplied)) return false;
        var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
    private static string? FindPageId(JsonElement root)
    {
        if (root.TryGetProperty("entity", out var entity) && entity.TryGetProperty("id", out var id)) return id.GetString();
        return root.TryGetProperty("data", out var data) && data.TryGetProperty("page_id", out var pageId) ? pageId.GetString() : null;
    }
    private static bool HasStatusChange(JsonElement root, string statusPropertyId)
    {
        if (!root.TryGetProperty("data", out var data)) return false;
        foreach (var propertyName in new[] { "updated_properties", "properties" })
        {
            if (!data.TryGetProperty(propertyName, out var changed) || changed.ValueKind != JsonValueKind.Array) continue;
            if (changed.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && string.Equals(item.GetString(), statusPropertyId, StringComparison.Ordinal))) return true;
        }
        return false;
    }
    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2)).Where(parts => parts.Length == 2)
        .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]), StringComparer.Ordinal);
    private static string? GetString(JsonElement value, string property) => value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static async Task<HttpResponseData> JsonAsync(HttpRequestData request, HttpStatusCode status, object body, CancellationToken cancellationToken) { var response = request.CreateResponse(status); response.Headers.Add("Cache-Control", "no-store"); await response.WriteAsJsonAsync(body, cancellationToken); return response; }
}
