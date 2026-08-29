using Microsoft.Azure.Functions.Worker;
using NotionTaskScheduler.Services;

namespace NotionTaskScheduler.Functions;

public sealed class CreateNotionTasksFunction
{
    private readonly SchedulerClock _clock;
    private readonly NotionTaskRunner _runner;

    public CreateNotionTasksFunction(
        SchedulerClock clock,
        NotionTaskRunner runner)
    {
        _clock = clock;
        _runner = runner;
    }

    [Function(nameof(CreateNotionTasksFunction))]
    public async Task Run([TimerTrigger("%Scheduler:Schedule%")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        var today = _clock.Today();
        await _runner.RunAsync(today, cancellationToken);
    }
}
