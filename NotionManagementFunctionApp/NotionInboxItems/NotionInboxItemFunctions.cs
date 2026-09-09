using System.Net;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace NotionManagementFunctionApp.NotionInboxItems;

public sealed class NotionInboxItemFunctions(
    NotionInboxItemsClient client,
    TelemetryClient telemetryClient,
    ILogger<NotionInboxItemFunctions> logger)
{
    // TODO: replace anonymous access with user authentication. This path is discoverable in the APK.
    private const string Route = "inbox/a9cea60dda62442e";
    private const string ItemRoute = Route + "/{id}";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("GetNotionInboxItems")]
    public Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)] HttpRequestData request,
        CancellationToken cancellationToken) => ExecuteAsync(request, async () =>
        {
            var items = await client.GetItemsAsync(cancellationToken);
            return await WriteJsonAsync(request, HttpStatusCode.OK, items, cancellationToken);
        }, cancellationToken);

    [Function("CreateNotionInboxItem")]
    public Task<HttpResponseData> PostAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)] HttpRequestData request,
        CancellationToken cancellationToken) => ExecuteAsync(request, async () =>
        {
            CreateNotionInboxItemRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<CreateNotionInboxItemRequest>(
                    request.Body, JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return await WriteJsonAsync(request, HttpStatusCode.BadRequest,
                    new { error = "Nieprawidłowy JSON. Oczekiwano obiektu z polem name." }, cancellationToken);
            }

            if (!TryNormalizeName(body?.Name, out var name))
            {
                return await WriteJsonAsync(request, HttpStatusCode.BadRequest,
                    new { error = "Wpis musi mieć od 1 do 2000 znaków." }, cancellationToken);
            }

            var item = await client.CreateItemAsync(name, cancellationToken);
            return await WriteJsonAsync(request, HttpStatusCode.Created, item, cancellationToken);
        }, cancellationToken);

    [Function("UpdateNotionInboxItem")]
    public Task<HttpResponseData> PatchAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = ItemRoute)] HttpRequestData request,
        string id,
        CancellationToken cancellationToken) => ExecuteAsync(request, async () =>
        {
            UpdateNotionInboxItemRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<UpdateNotionInboxItemRequest>(
                    request.Body, JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return await WriteJsonAsync(request, HttpStatusCode.BadRequest,
                    new { error = "Nieprawidłowy JSON. Oczekiwano obiektu z polem name." }, cancellationToken);
            }

            if (!TryNormalizeName(body?.Name, out var name))
            {
                return await WriteJsonAsync(request, HttpStatusCode.BadRequest,
                    new { error = "Wpis musi mieć od 1 do 2000 znaków." }, cancellationToken);
            }

            var item = await client.UpdateItemAsync(id, name, cancellationToken);
            return await WriteJsonAsync(request, HttpStatusCode.OK, item, cancellationToken);
        }, cancellationToken);

    private async Task<HttpResponseData> ExecuteAsync(HttpRequestData request,
        Func<Task<HttpResponseData>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notion Inbox item request failed.");
            telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["FunctionName"] = request.FunctionContext.FunctionDefinition.Name,
                ["Trigger"] = "Http"
            });
            var status = ex switch
            {
                HttpRequestException or JsonException => HttpStatusCode.BadGateway,
                OperationCanceledException => HttpStatusCode.GatewayTimeout,
                InvalidOperationException => HttpStatusCode.ServiceUnavailable,
                KeyNotFoundException => HttpStatusCode.NotFound,
                _ => HttpStatusCode.InternalServerError
            };
            return await WriteJsonAsync(request, status,
                new { error = "Nie udało się obsłużyć Inbox. Spróbuj ponownie później." }, cancellationToken);
        }
    }

    private static async Task<HttpResponseData> WriteJsonAsync<T>(HttpRequestData request,
        HttpStatusCode status, T body, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(status);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(body, cancellationToken);
        return response;
    }

    private static bool TryNormalizeName(string? value, out string name)
    {
        name = value?.Trim() ?? string.Empty;
        return name.Length is > 0 and <= 2000;
    }
}
