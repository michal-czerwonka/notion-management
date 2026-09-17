using System.ComponentModel.DataAnnotations;

namespace NotionManagementFunctionApp.RoutineTasks;

public sealed class RoutineTaskOptions
{
    public const string SectionName = "RoutineTasks";
    [Required, MinLength(12)] public string RouteSegment { get; init; } = "";
    [Required] public string ConfigurationPath { get; init; } = "config/routine-tasks.json";
}

