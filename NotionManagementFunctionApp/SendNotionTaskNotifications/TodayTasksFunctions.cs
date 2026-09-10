using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed class TodayTasksFunctions(NotionTodayTasksClient client)
{
    private const string Route = "today-tasks/65aa9c24486c4e8a";

    [Function("GetTodayTasks")]
    public async Task<HttpResponseData> GetAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)] HttpRequestData request, CancellationToken cancellationToken)
    {
        var tasks = await client.GetTodayViewTasksAsync(cancellationToken);
        var statuses = await client.GetStatusOptionsAsync(cancellationToken);
        return await JsonAsync(request, HttpStatusCode.OK, new TodayTasksResponse(tasks.Select(task => new TodayTask(task.PageId, task.Name, task.Status, task.Projects)).ToArray(), statuses), cancellationToken);
    }

    [Function("UpdateTodayTaskStatus")]
    public async Task<HttpResponseData> PatchAsync([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = Route + "/{id}")] HttpRequestData request, string id, CancellationToken cancellationToken)
    {
        UpdateTodayTaskStatusRequest? body;
        try { body = await JsonSerializer.DeserializeAsync<UpdateTodayTaskStatusRequest>(request.Body, cancellationToken: cancellationToken); }
        catch (JsonException) { return await JsonAsync(request, HttpStatusCode.BadRequest, new { error = "Invalid status request." }, cancellationToken); }
        if (string.IsNullOrWhiteSpace(body?.Status)) return await JsonAsync(request, HttpStatusCode.BadRequest, new { error = "A status is required." }, cancellationToken);
        try { await client.UpdateStatusAsync(id, body.Status.Trim(), cancellationToken); }
        catch (ArgumentException) { return await JsonAsync(request, HttpStatusCode.BadRequest, new { error = "Unknown task status." }, cancellationToken); }
        catch (KeyNotFoundException) { return await JsonAsync(request, HttpStatusCode.NotFound, new { error = "Task was not found." }, cancellationToken); }
        return await JsonAsync(request, HttpStatusCode.OK, new { id, status = body.Status.Trim() }, cancellationToken);
    }

    [Function("ArchiveTodayTask")]
    public async Task<HttpResponseData> DeleteAsync([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = Route + "/{id}")] HttpRequestData request, string id, CancellationToken cancellationToken)
    {
        try { await client.ArchiveAsync(id, cancellationToken); }
        catch (KeyNotFoundException) { return await JsonAsync(request, HttpStatusCode.NotFound, new { error = "Task was not found." }, cancellationToken); }
        return request.CreateResponse(HttpStatusCode.NoContent);
    }

    private static async Task<HttpResponseData> JsonAsync(HttpRequestData request, HttpStatusCode status, object body, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(status);
        response.Headers.Add("Cache-Control", "no-store");
        await response.WriteAsJsonAsync(body, cancellationToken);
        return response;
    }
}
