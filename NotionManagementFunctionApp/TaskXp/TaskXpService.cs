using Microsoft.Extensions.Options;
using NotionManagementFunctionApp.SendNotionTaskNotifications;
namespace NotionManagementFunctionApp.TaskXp;
public sealed class TaskXpService(TaskXpRepository repository, IOptions<TaskXpOptions> options, BusinessPeriodCalculator periods, DailyTargetSchedule targets)
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
    public BusinessPeriod CurrentPeriod(string period, DateTimeOffset now) => periods.For(period, periods.BusinessDate(now));
    public BusinessPeriod? ParsePeriod(string period, string periodStart)
    {
        if (!DateOnly.TryParseExact(periodStart, "yyyy-MM-dd", out var start)) return null;
        try
        {
            var normalized = periods.For(period, start);
            return normalized.Start == start ? normalized : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }
    public bool IsEligible(BusinessPeriod period) => period.EndExclusive > targets.FirstEffectiveDate;
    public Task<XpProgressResult> GetProgressAsync(BusinessPeriod period, CancellationToken cancellationToken) => repository.GetProgressAsync(period, cancellationToken);
    public Task ReconcileTargetsAsync(DateTimeOffset now, CancellationToken cancellationToken) => repository.ReconcileTargetsAsync(periods.BusinessDate(now), cancellationToken);
}
