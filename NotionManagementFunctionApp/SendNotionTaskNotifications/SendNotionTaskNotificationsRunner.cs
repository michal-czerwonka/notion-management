using Microsoft.Extensions.Logging;

namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed class SendNotionTaskNotificationsRunner
{
    private readonly NotionTodayTasksClient _notionTodayTasksClient;
    private readonly NtfyClient _ntfyClient;
    private readonly ILogger<SendNotionTaskNotificationsRunner> _logger;

    public SendNotionTaskNotificationsRunner(
        NotionTodayTasksClient notionTodayTasksClient,
        NtfyClient ntfyClient,
        ILogger<SendNotionTaskNotificationsRunner> logger)
    {
        _notionTodayTasksClient = notionTodayTasksClient;
        _ntfyClient = ntfyClient;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Notion task notification run.");

        var tasks = await _notionTodayTasksClient.GetTodayViewTasksAsync(cancellationToken);
        if (tasks.Count == 0)
        {
            _logger.LogInformation("Notion view contains no tasks. Notification will not be sent.");
            return;
        }

        var message = FormatNotification(tasks);
        var result = await _ntfyClient.SendAsync(message, cancellationToken);

        _logger.LogInformation(
            "Sent ntfy notification with {TaskCount} task(s). NtfyMessageId: {NtfyMessageId}. TopicConfigured: {TopicConfigured}.",
            tasks.Count,
            result.MessageId,
            result.TopicConfigured);
    }

    private static string FormatNotification(IReadOnlyList<NotionTodayTask> tasks)
    {
        var lines = new List<string>
        {
            $"Zadania na dzisiaj: {tasks.Count}",
            ""
        };

        lines.AddRange(tasks.Select(task => $"• {task.Name}"));

        return string.Join(Environment.NewLine, lines);
    }
}