using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;

namespace NotionManagementFunctionApp.CreateNotionTasks;

public sealed class TaskConfigLoader
{
    private const string TaskDateFormat = "dd.MM";
    private const int LeapYear = 2024;

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
                .Select(ParseMonthDay)
                .Where(monthDay => monthDay.Month == date.Month && monthDay.Day == date.Day)
                .Select(_ => new DueTask(task.Id, task.Name, date)))
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
                if (!TryParseMonthDay(rawDate, out _))
                {
                    throw new InvalidOperationException($"Task '{task.Id}' contains invalid date '{rawDate}'. Expected dd.MM.");
                }
            }
        }
    }

    private static MonthDay ParseMonthDay(string rawDate)
    {
        return TryParseMonthDay(rawDate, out var monthDay)
            ? monthDay
            : throw new InvalidOperationException($"Invalid task date '{rawDate}'. Expected dd.MM.");
    }

    private static bool TryParseMonthDay(string rawDate, out MonthDay monthDay)
    {
        monthDay = default;

        if (!DateOnly.TryParseExact(
            $"{rawDate}.{LeapYear}",
            $"{TaskDateFormat}.yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate))
        {
            return false;
        }

        monthDay = new MonthDay(parsedDate.Month, parsedDate.Day);
        return true;
    }

    private readonly record struct MonthDay(int Month, int Day);
}