using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace NotionManagementFunctionApp.CreateNotionTasks;

public sealed class CreateNotionTasksFunction
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly SchedulerClock _clock;
    private readonly CreateNotionTasksRunner _runner;
    private readonly ILogger<CreateNotionTasksFunction> _logger;

    public CreateNotionTasksFunction(
        SchedulerClock clock,
        CreateNotionTasksRunner runner,
        ILogger<CreateNotionTasksFunction> logger)
    {
        _clock = clock;
        _runner = runner;
        _logger = logger;
    }

    [Function(nameof(CreateNotionTasksFunction))]
    public async Task RunTimer([TimerTrigger("%Scheduler:Schedule%")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        var today = _clock.Today();
        await _runner.RunAsync(today, cancellationToken);
    }

    [Function("CreateNotionTasksFunctionHttp")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "run")] HttpRequestData request,
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