using Microsoft.Extensions.Logging;
using NotionTaskScheduler.Models;

namespace NotionTaskScheduler.Services;

public sealed class NotionTaskRunner
{
    private readonly TaskConfigLoader _taskConfigLoader;
    private readonly NotionClient _notionClient;
    private readonly ILogger<NotionTaskRunner> _logger;

    public NotionTaskRunner(
        TaskConfigLoader taskConfigLoader,
        NotionClient notionClient,
        ILogger<NotionTaskRunner> logger)
    {
        _taskConfigLoader = taskConfigLoader;
        _notionClient = notionClient;
        _logger = logger;
    }

    public async Task<RunNotionTasksResult> RunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Notion task creation for {Date}.", date);

        var dueTasks = await _taskConfigLoader.LoadDueTasksAsync(date, cancellationToken);
        _logger.LogInformation("Found {TaskCount} task(s) due on {Date}.", dueTasks.Count, date);

        var created = 0;
        var failed = 0;
        var errors = new List<string>();
        var createdTasks = new List<CreatedNotionTask>();

        foreach (var task in dueTasks)
        {
            try
            {
                var createdPage = await _notionClient.CreateTaskAsync(task, cancellationToken);
                created++;
                createdTasks.Add(new CreatedNotionTask(
                    task.Id,
                    task.Date,
                    createdPage.PageId,
                    createdPage.Url,
                    createdPage.PublicUrl,
                    createdPage.CreatedTime,
                    createdPage.ParentType,
                    createdPage.ParentId));

                _logger.LogInformation(
                    "Created Notion task for configured task id '{TaskId}' and date {Date}. PageId: {PageId}. Url: {Url}. PublicUrl: {PublicUrl}. CreatedTime: {CreatedTime}. ParentType: {ParentType}. ParentId: {ParentId}.",
                    task.Id,
                    task.Date,
                    createdPage.PageId,
                    createdPage.Url,
                    createdPage.PublicUrl,
                    createdPage.CreatedTime,
                    createdPage.ParentType,
                    createdPage.ParentId);
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Task '{task.Id}' for {task.Date}: {ex.Message}");

                _logger.LogError(ex, "Failed to create Notion task for configured task id '{TaskId}' and date {Date}.", task.Id, task.Date);
            }
        }

        _logger.LogInformation("Finished Notion task creation for {Date}. Created: {CreatedCount}. Failed: {FailedCount}.", date, created, failed);

        return new RunNotionTasksResult(date, dueTasks.Count, created, failed, createdTasks, errors);
    }
}
