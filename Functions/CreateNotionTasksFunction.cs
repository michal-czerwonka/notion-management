using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NotionTaskScheduler.Services;

namespace NotionTaskScheduler.Functions;

public sealed class CreateNotionTasksFunction
{
    private readonly SchedulerClock _clock;
    private readonly TaskConfigLoader _taskConfigLoader;
    private readonly NotionClient _notionClient;
    private readonly ILogger<CreateNotionTasksFunction> _logger;

    public CreateNotionTasksFunction(
        SchedulerClock clock,
        TaskConfigLoader taskConfigLoader,
        NotionClient notionClient,
        ILogger<CreateNotionTasksFunction> logger)
    {
        _clock = clock;
        _taskConfigLoader = taskConfigLoader;
        _notionClient = notionClient;
        _logger = logger;
    }

    [Function(nameof(CreateNotionTasksFunction))]
    public async Task Run([TimerTrigger("%Scheduler:Schedule%")] TimerInfo timerInfo, CancellationToken cancellationToken)
    {
        var today = _clock.Today();
        _logger.LogInformation("Starting Notion task creation for {Date}.", today);

        var dueTasks = await _taskConfigLoader.LoadDueTasksAsync(today, cancellationToken);
        _logger.LogInformation("Found {TaskCount} task(s) due on {Date}.", dueTasks.Count, today);

        var created = 0;
        var failed = 0;

        foreach (var task in dueTasks)
        {
            try
            {
                await _notionClient.CreateTaskAsync(task, cancellationToken);
                created++;

                _logger.LogInformation("Created Notion task for configured task id '{TaskId}' and date {Date}.", task.Id, task.Date);
            }
            catch (Exception ex)
            {
                failed++;

                _logger.LogError(ex, "Failed to create Notion task for configured task id '{TaskId}' and date {Date}.", task.Id, task.Date);
            }
        }

        _logger.LogInformation("Finished Notion task creation for {Date}. Created: {CreatedCount}. Failed: {FailedCount}.", today, created, failed);
    }
}
