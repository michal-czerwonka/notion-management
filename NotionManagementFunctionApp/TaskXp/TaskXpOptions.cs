using System.ComponentModel.DataAnnotations;

namespace NotionManagementFunctionApp.TaskXp;

public sealed class TaskXpOptions : IValidatableObject
{
    public const string SectionName = "TaskXp";
    public string Endpoint { get; init; } = "";
    public string? ConnectionString { get; init; }
    [Required] public string Database { get; init; } = "task-xp";
    [Required] public string Container { get; init; } = "progression";
    [Required, MinLength(12)] public string ReadRouteSegment { get; init; } = "";
    public string? NotionWebhookVerificationToken { get; init; }
    [Range(1, int.MaxValue)] public int Trivial { get; init; } = 5;
    [Range(1, int.MaxValue)] public int Easy { get; init; } = 10;
    [Range(1, int.MaxValue)] public int Medium { get; init; } = 25;
    [Range(1, int.MaxValue)] public int Hard { get; init; } = 50;
    [Range(1, int.MaxValue)] public int Epic { get; init; } = 100;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Endpoint) && string.IsNullOrWhiteSpace(ConnectionString)) yield return new("TaskXp requires Endpoint or ConnectionString.");
    }
    public int XpFor(string effort) => effort switch { "Trivial" => Trivial, "Easy" => Easy, "Medium" => Medium, "Hard" => Hard, "Epic" => Epic, _ => throw new TaskXpConfigurationException("Unsupported effort mapping.") };
}
public sealed class TaskXpConfigurationException(string message) : Exception(message);
