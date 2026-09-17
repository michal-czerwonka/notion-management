using System.Text.Json;
using Microsoft.Extensions.Options;
using NotionManagementFunctionApp.TaskXp;

namespace NotionManagementFunctionApp.RoutineTasks;

public sealed class RoutineTaskConfiguration
{
    private static readonly string[] Weekdays = ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RoutineTaskDefinition>> _schedule;

    public RoutineTaskConfiguration(IOptions<RoutineTaskOptions> options, IOptions<TaskXpOptions> xpOptions)
    {
        var path = Path.GetFullPath(options.Value.ConfigurationPath, AppContext.BaseDirectory);
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        _schedule = Parse(document.RootElement, xpOptions.Value);
    }

    public IReadOnlyList<RoutineTaskDefinition> For(DateOnly businessDate) => _schedule[businessDate.DayOfWeek.ToString().ToLowerInvariant()];

    private static IReadOnlyDictionary<string, IReadOnlyList<RoutineTaskDefinition>> Parse(JsonElement root, TaskXpOptions xp)
    {
        if (root.ValueKind != JsonValueKind.Object) throw Invalid("the top-level value must be an object");
        var properties = root.EnumerateObject().ToArray();
        if (properties.Length != Weekdays.Length || properties.Any(item => !Weekdays.Contains(item.Name, StringComparer.Ordinal))) throw Invalid("all and only supported weekdays are required");
        var schedule = new Dictionary<string, IReadOnlyList<RoutineTaskDefinition>>(StringComparer.Ordinal);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var weekday in Weekdays)
        {
            var value = root.GetProperty(weekday);
            if (value.ValueKind != JsonValueKind.Array) throw Invalid($"weekday '{weekday}' must contain an array");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var tasks = new List<RoutineTaskDefinition>();
            foreach (var task in value.EnumerateArray())
            {
                if (task.ValueKind != JsonValueKind.Object || task.EnumerateObject().Count() != 3) throw Invalid($"weekday '{weekday}' contains an invalid task object");
                var id = RequiredString(task, "id", weekday);
                var name = RequiredString(task, "name", weekday);
                var effort = RequiredString(task, "effort", weekday);
                if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9]+(?:-[a-z0-9]+)*$")) throw Invalid($"routine id '{id}' is not a stable kebab-case identifier");
                if (!ids.Add(id)) throw Invalid($"weekday '{weekday}' repeats id '{id}'");
                if (names.TryGetValue(id, out var knownName) && !string.Equals(knownName, name, StringComparison.Ordinal)) throw Invalid($"routine id '{id}' uses inconsistent names");
                names[id] = name;
                _ = xp.XpFor(effort);
                tasks.Add(new RoutineTaskDefinition(id, name, effort));
            }
            schedule.Add(weekday, tasks);
        }
        return schedule;
    }

    private static string RequiredString(JsonElement task, string property, string weekday)
    {
        if (!task.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) throw Invalid($"weekday '{weekday}' has a task with an invalid '{property}'");
        return value.GetString()!;
    }

    private static TaskXpConfigurationException Invalid(string reason) => new($"Invalid routine task configuration: {reason}.");
}

