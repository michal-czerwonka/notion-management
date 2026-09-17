using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NotionManagementFunctionApp.RoutineTasks;

public sealed class RoutineTaskFunctions(RoutineTaskService routines, IOptions<RoutineTaskOptions> options, ILogger<RoutineTaskFunctions> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Function("GetRoutineTasks")]
    public async Task<HttpResponseData> GetAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "routine-tasks/{segment}")] HttpRequestData request, string segment, CancellationToken cancellationToken)
    {
        if (!Authorize(segment)) return request.CreateResponse(HttpStatusCode.NotFound);
        try { return await JsonAsync(request, HttpStatusCode.OK, await routines.GetCurrentAsync(DateTimeOffset.UtcNow, cancellationToken), cancellationToken); }
        catch (CosmosException exception)
        {
            logger.LogError(exception, "Routine list persistence read failed. StatusCode={StatusCode}, ActivityId={ActivityId}", (int)exception.StatusCode, exception.ActivityId);
            return await JsonAsync(request, HttpStatusCode.ServiceUnavailable, new { error = "Routine tasks are temporarily unavailable." }, cancellationToken);
        }
    }

    [Function("UpdateRoutineTask")]
    public async Task<HttpResponseData> PutAsync([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "routine-tasks/{segment}/{routineId}")] HttpRequestData request, string segment, string routineId, CancellationToken cancellationToken)
    {
        if (!Authorize(segment)) return request.CreateResponse(HttpStatusCode.NotFound);
        RoutineTaskMutationRequest? body;
        try { body = await JsonSerializer.DeserializeAsync<RoutineTaskMutationRequest>(request.Body, JsonOptions, cancellationToken); }
        catch (JsonException) { return await JsonAsync(request, HttpStatusCode.BadRequest, new { error = "The request body is invalid." }, cancellationToken); }
        if (body is null || !Guid.TryParseExact(body.OperationId, "D", out _) || body.ExpectedVersion < 0 || !DateOnly.TryParseExact(body.ExpectedBusinessDate, "yyyy-MM-dd", out _) || body.State is not ("pending" or "completed" or "skipped"))
        {
            return await JsonAsync(request, HttpStatusCode.BadRequest, new { error = "operationId, expectedBusinessDate, expectedVersion, and a supported state are required." }, cancellationToken);
        }
        try { return await JsonAsync(request, HttpStatusCode.OK, await routines.MutateAsync(routineId, body, DateTimeOffset.UtcNow, cancellationToken), cancellationToken); }
        catch (KeyNotFoundException) { return await JsonAsync(request, HttpStatusCode.NotFound, new { error = "The routine is not configured for the current business date." }, cancellationToken); }
        catch (RoutineTaskConflictException exception) { return await JsonAsync(request, HttpStatusCode.Conflict, exception.Conflict, cancellationToken); }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Routine occurrence is inconsistent. RoutineId={RoutineId}", routineId);
            return await JsonAsync(request, HttpStatusCode.ServiceUnavailable, new { error = "The routine change could not be saved." }, cancellationToken);
        }
        catch (CosmosException exception)
        {
            logger.LogError(exception, "Routine mutation persistence failed. RoutineId={RoutineId}, StatusCode={StatusCode}, ActivityId={ActivityId}", routineId, (int)exception.StatusCode, exception.ActivityId);
            return await JsonAsync(request, HttpStatusCode.ServiceUnavailable, new { error = "The routine change could not be saved. Retry the same operation." }, cancellationToken);
        }
    }

    private bool Authorize(string segment) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(segment), Encoding.UTF8.GetBytes(options.Value.RouteSegment));
    private static async Task<HttpResponseData> JsonAsync(HttpRequestData request, HttpStatusCode status, object body, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(status);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(body, cancellationToken);
        return response;
    }
}
