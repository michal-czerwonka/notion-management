using Microsoft.Azure.Functions.Worker;

namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed class SendNotionTaskNotificationsFunction
{
    private readonly SendNotionTaskNotificationsRunner _runner;

    public SendNotionTaskNotificationsFunction(SendNotionTaskNotificationsRunner runner)
    {
        _runner = runner;
    }

    [Function(nameof(SendNotionTaskNotificationsFunction))]
    public async Task RunTimer([TimerTrigger("%Notifications:Schedule%")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        await _runner.RunAsync(cancellationToken);
    }
}