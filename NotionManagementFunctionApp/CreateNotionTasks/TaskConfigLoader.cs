using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;

namespace NotionManagementFunctionApp.CreateNotionTasks;

public sealed class TaskConfigLoader
{
    private const string TaskDateFormat = "dd.MM";
    private const int CommonYear = 2023;

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
            .Where(task => IsDueOn(task, date))
            .Select(task => new DueTask(task.Id, task.Name, date))
            .ToArray();
    }

    private static bool IsDueOn(ScheduledTask task, DateOnly date)
    {
        return MatchesDate(task, date) || MatchesRule(task, date);
    }

    private static bool MatchesDate(ScheduledTask task, DateOnly date)
    {
        return task.Dates.Any(rawDate =>
        {
            if (!TryParseMonthDay(rawDate, out var monthDay))
            {
                return false;
            }

            return monthDay.Month == date.Month && monthDay.Day == date.Day;
        });
    }

    private static bool MatchesRule(ScheduledTask task, DateOnly date)
    {
        return task.Rules.Any(rule => rule.Type.Trim().ToLowerInvariant() switch
        {
            "daily" => true,
            "weekly" => MatchesWeeklyRule(rule, date),
            "monthly" => MatchesMonthlyRule(rule, date),
            _ => false
        });
    }

    private static bool MatchesWeeklyRule(TaskScheduleRule rule, DateOnly date)
    {
        return TryParseDayOfWeek(rule.DayOfWeek, out var dayOfWeek) && date.DayOfWeek == dayOfWeek;
    }

    private static bool MatchesMonthlyRule(TaskScheduleRule rule, DateOnly date)
    {
        return rule.Day == date.Day;
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
            ValidateTask(task);
        }
    }

    private static void ValidateTask(ScheduledTask? task)
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

        if (task.Rules is null)
        {
            throw new InvalidOperationException($"Task '{task.Id}' must contain a rules array.");
        }

        if (task.Dates.Count == 0 && task.Rules.Count == 0)
        {
            throw new InvalidOperationException($"Task '{task.Id}' must contain at least one date or rule.");
        }


        foreach (var rule in task.Rules)
        {
            ValidateRule(task.Id, rule);
        }
    }

    private static void ValidateRule(string taskId, TaskScheduleRule? rule)
    {
        if (rule is null)
        {
            throw new InvalidOperationException($"Task '{taskId}' must not contain null rule entries.");
        }

        if (string.IsNullOrWhiteSpace(rule.Type))
        {
            throw new InvalidOperationException($"Task '{taskId}' contains a rule without type.");
        }

        switch (rule.Type.Trim().ToLowerInvariant())
        {
            case "daily":
                return;
            case "weekly":
                if (!TryParseDayOfWeek(rule.DayOfWeek, out _))
                {
                    throw new InvalidOperationException($"Task '{taskId}' contains invalid weekly rule dayOfWeek '{rule.DayOfWeek}'.");
                }

                return;
            case "monthly":
                if (rule.Day is null or < 1 or > 31)
                {
                    throw new InvalidOperationException($"Task '{taskId}' contains invalid monthly rule day '{rule.Day}'. Expected 1-31.");
                }

                return;
            default:
                throw new InvalidOperationException($"Task '{taskId}' contains unsupported rule type '{rule.Type}'. Expected daily, weekly, or monthly.");
        }
    }


    private static bool TryParseMonthDay(string rawDate, out MonthDay monthDay)
    {
        monthDay = default;

        if (!DateOnly.TryParseExact(
            $"{rawDate}.{CommonYear}",
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

    private static bool TryParseDayOfWeek(string? rawDayOfWeek, out DayOfWeek dayOfWeek)
    {
        if (Enum.TryParse(rawDayOfWeek, ignoreCase: true, out dayOfWeek))
        {
            return true;
        }

        return TryParsePolishDayOfWeek(rawDayOfWeek, out dayOfWeek);
    }

    private static bool TryParsePolishDayOfWeek(string? rawDayOfWeek, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = default;

        var normalized = rawDayOfWeek?.Trim().ToLowerInvariant();
        dayOfWeek = normalized switch
        {
            "poniedzialek" => DayOfWeek.Monday,
            "poniedziałek" => DayOfWeek.Monday,
            "wtorek" => DayOfWeek.Tuesday,
            "sroda" => DayOfWeek.Wednesday,
            "środa" => DayOfWeek.Wednesday,
            "czwartek" => DayOfWeek.Thursday,
            "piatek" => DayOfWeek.Friday,
            "piątek" => DayOfWeek.Friday,
            "sobota" => DayOfWeek.Saturday,
            "niedziela" => DayOfWeek.Sunday,
            _ => default
        };

        return normalized is "poniedzialek" or "poniedziałek" or "wtorek" or "sroda" or "środa" or "czwartek" or "piatek" or "piątek" or "sobota" or "niedziela";
    }

    private readonly record struct MonthDay(int Month, int Day);
}