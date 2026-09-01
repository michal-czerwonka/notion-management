using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed class SendNotionTaskNotificationsFunction
{
    private readonly SendNotionTaskNotificationsRunner _runner;
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<SendNotionTaskNotificationsFunction> _logger;

    public SendNotionTaskNotificationsFunction(
        SendNotionTaskNotificationsRunner runner,
        TelemetryClient telemetryClient,
        ILogger<SendNotionTaskNotificationsFunction> logger)
    {
        _runner = runner;
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    [Function(nameof(SendNotionTaskNotificationsFunction))]
    public async Task RunTimer([TimerTrigger("%Notifications:Schedule%")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        await _runner.RunAsync(cancellationToken);
    }

    [Function("SendNotionTaskNotificationsFunctionHttp")]
    public async Task<HttpResponseData> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "notifications/run")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner.RunAsync(cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual Notion task notification run failed.");
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["FunctionName"] = "SendNotionTaskNotificationsFunctionHttp",
                ["Trigger"] = "Http"
            });

            var response = request.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = ex.Message }, cancellationToken);

            return response;
        }
    }
}
