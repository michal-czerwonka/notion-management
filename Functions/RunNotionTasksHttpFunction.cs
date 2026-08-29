using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NotionTaskScheduler.Models;
using NotionTaskScheduler.Services;

namespace NotionTaskScheduler.Functions;

public sealed class RunNotionTasksHttpFunction
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly SchedulerClock _clock;
    private readonly NotionTaskRunner _runner;
    private readonly ILogger<RunNotionTasksHttpFunction> _logger;

    public RunNotionTasksHttpFunction(
        SchedulerClock clock,
        NotionTaskRunner runner,
        ILogger<RunNotionTasksHttpFunction> logger)
    {
        _clock = clock;
        _runner = runner;
        _logger = logger;
    }

    [Function(nameof(RunNotionTasksHttpFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "run")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var (requestedDate, error) = await ReadRequestedDateAsync(request, cancellationToken);
        if (requestedDate is null)
        {
            return await WriteJsonAsync(
                request,
                HttpStatusCode.BadRequest,
                new { error },
                cancellationToken);
        }

        try
        {
            var result = await _runner.RunAsync(requestedDate.Value, cancellationToken);

            return await WriteJsonAsync(request, HttpStatusCode.OK, result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual Notion task run failed before per-task processing could complete.");

            return await WriteJsonAsync(
                request,
                HttpStatusCode.InternalServerError,
                new { error = ex.Message },
                cancellationToken);
        }
    }

    private async Task<(DateOnly? Date, string? Error)> ReadRequestedDateAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        if (request.Body is null || !request.Body.CanRead)
        {
            return (_clock.Today(), null);
        }

        using var reader = new StreamReader(request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return (_clock.Today(), null);
        }

        RunNotionTasksRequest? body;
        try
        {
            body = JsonSerializer.Deserialize<RunNotionTasksRequest>(rawBody, SerializerOptions);
        }
        catch (JsonException)
        {
            return (null, "Invalid JSON body.");
        }

        if (string.IsNullOrWhiteSpace(body?.Date))
        {
            return (_clock.Today(), null);
        }

        if (!DateOnly.TryParseExact(body.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return (null, "Invalid date. Expected yyyy-MM-dd.");
        }

        return (date, null);
    }

    private static async Task<HttpResponseData> WriteJsonAsync<T>(
        HttpRequestData request,
        HttpStatusCode statusCode,
        T body,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(body, cancellationToken);

        return response;
    }
}
