using Microsoft.Extensions.Options;
using NotionManagementFunctionApp.SendNotionTaskNotifications;
namespace NotionManagementFunctionApp.TaskXp;
public sealed class TaskXpService(TaskXpRepository repository, IOptions<TaskXpOptions> options)
{
    public Task RecordAsync(NotionTaskSnapshot task, string source, string sourceEventId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        var effort = string.IsNullOrWhiteSpace(task.Effort) ? "Medium" : task.Effort;
        var configured = options.Value;
        if (effort is not ("Trivial" or "Easy" or "Medium" or "Hard" or "Epic")) throw new TaskXpConfigurationException("Notion Effort must use a supported value.");
        return repository.RecordAsync(new TaskXpInput(task, source, sourceEventId, occurredAt), effort, configured.XpFor(effort), cancellationToken);
    }
    public Task<int> GetTotalAsync(CancellationToken cancellationToken) => repository.GetTotalAsync(cancellationToken);
    public Task<XpHistoryPage> GetEventsAsync(int limit, string? continuationToken, CancellationToken cancellationToken) => repository.GetEventsAsync(limit, continuationToken, cancellationToken);
}
