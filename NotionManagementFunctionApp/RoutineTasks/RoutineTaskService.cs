using Microsoft.Extensions.Options;
using NotionManagementFunctionApp.TaskXp;

namespace NotionManagementFunctionApp.RoutineTasks;

public sealed class RoutineTaskService(RoutineTaskConfiguration configuration, RoutineTaskRepository repository, BusinessPeriodCalculator periods, IOptions<TaskXpOptions> xpOptions)
{
    public async Task<RoutineTaskListResult> GetCurrentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var businessDate = periods.BusinessDate(now);
        var tasks = new List<RoutineTaskItem>();
        foreach (var definition in configuration.For(businessDate))
        {
            var occurrence = await repository.GetOccurrenceAsync(businessDate, definition.Id, cancellationToken);
            tasks.Add(new RoutineTaskItem(definition.Id, definition.Name, definition.Effort, xpOptions.Value.XpFor(definition.Effort), occurrence?.State ?? "pending", occurrence?.Version ?? 0));
        }
        return new RoutineTaskListResult(businessDate.ToString("yyyy-MM-dd"), tasks);
    }

    public async Task<RoutineTaskMutationResult> MutateAsync(string routineId, RoutineTaskMutationRequest request, DateTimeOffset acceptedAt, CancellationToken cancellationToken)
    {
        var businessDate = periods.BusinessDate(acceptedAt);
        var replay = await repository.ReplayAsync(businessDate, routineId, request, cancellationToken);
        if (replay is not null) return replay;
        var definition = configuration.For(businessDate).SingleOrDefault(item => string.Equals(item.Id, routineId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException("The routine is not configured for the current business date.");
        return await repository.MutateAsync(businessDate, definition, xpOptions.Value.XpFor(definition.Effort), request, acceptedAt, cancellationToken);
    }
}
