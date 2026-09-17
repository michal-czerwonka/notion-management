using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace NotionManagementFunctionApp.TaskXp;

public sealed class ReconcileXpProgressTargetsFunction(TaskXpService taskXp, ILogger<ReconcileXpProgressTargetsFunction> logger)
{
    [Function("ReconcileXpProgressTargetsFunction")]
    public async Task RunAsync([TimerTrigger("%TaskXp:ProgressReconciliationSchedule%")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            await taskXp.ReconcileTargetsAsync(now, cancellationToken);
            logger.LogInformation("Reconciled Task XP progress targets. IsPastDue={IsPastDue}", timerInfo.IsPastDue);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Task XP progress target reconciliation failed.");
            throw;
        }
    }
}
