using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;

namespace NotionManagementFunctionApp.CreateNotionTasks;

public sealed class TaskConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _filePath;

    public TaskConfigLoader(IConfiguration configuration)
    {
        _filePath = configuration["Tasks:FilePath"] ?? "CreateNotionTasks/tasks.json";
    }

    public async Task<IReadOnlyList<DueTask>> LoadDueTasksAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(_filePath);
        var tasksFile = await JsonSerializer.DeserializeAsync<TasksFile>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("tasks.json is empty or invalid.");

        Validate(tasksFile);

        return tasksFile.Tasks
            .SelectMany(task => task.Dates
                .Select(rawDate => DateOnly.ParseExact(rawDate, "yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Where(taskDate => taskDate == date)
                .Select(taskDate => new DueTask(task.Id, task.Name, taskDate)))
            .ToArray();
    }

    private static void Validate(TasksFile tasksFile)
    {
        if (tasksFile.Version != 1)
        {
            throw new InvalidOperationException("Unsupported tasks.json version. Expected version 1.");
        }

        if (tasksFile.Tasks is null)
        {
            throw new InvalidOperationException("tasks.json must contain a tasks array.");
        }

        foreach (var task in tasksFile.Tasks)
        {
            if (task is null)
            {
                throw new InvalidOperationException("tasks.json must not contain null task entries.");
            }

            if (string.IsNullOrWhiteSpace(task.Id))
            {
                throw new InvalidOperationException("Each task must have a non-empty id.");
            }

            if (string.IsNullOrWhiteSpace(task.Name))
            {
                throw new InvalidOperationException($"Task '{task.Id}' must have a non-empty name.");
            }

            if (task.Dates is null)
            {
                throw new InvalidOperationException($"Task '{task.Id}' must contain a dates array.");
            }

            foreach (var rawDate in task.Dates)
            {
                if (!DateOnly.TryParseExact(rawDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    throw new InvalidOperationException($"Task '{task.Id}' contains invalid date '{rawDate}'. Expected yyyy-MM-dd.");
                }
            }
        }
    }
}