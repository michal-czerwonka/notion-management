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

    public async Task<SendNotionTaskNotificationsResult> RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Notion task notification run.");

        var tasks = await _notionTodayTasksClient.GetTodayViewTasksAsync(cancellationToken);
        var message = FormatNotification(tasks);
        var result = await _ntfyClient.SendAsync(message, cancellationToken);

        _logger.LogInformation(
            "Sent ntfy notification with {TaskCount} task(s). NtfyMessageId: {NtfyMessageId}. TopicConfigured: {TopicConfigured}.",
            tasks.Count,
            result.MessageId,
            result.TopicConfigured);

        return new SendNotionTaskNotificationsResult(tasks.Count, NotificationSent: true, result.MessageId);
    }

    private static string FormatNotification(IReadOnlyList<NotionTodayTask> tasks)
    {
        if (tasks.Count == 0)
        {
            return "brak zadań, trzeba uzupełnić Notion";
        }

        var lines = new List<string>
        {
            $"Zadania na dzisiaj: {tasks.Count}",
            ""
        };

        foreach (var task in tasks)
        {
            lines.Add($"• **{EscapeMarkdown(task.Name)}**");
            lines.Add($"  Status: {EscapeMarkdown(task.Status)}");
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string EscapeMarkdown(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }
}
