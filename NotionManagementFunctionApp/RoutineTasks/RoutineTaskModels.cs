using Newtonsoft.Json;

namespace NotionManagementFunctionApp.RoutineTasks;

public sealed record RoutineTaskDefinition(string Id, string Name, string Effort);
public sealed record RoutineTaskItem(string Id, string Name, string Effort, int Xp, string State, int Version);
public sealed record RoutineTaskListResult(string BusinessDate, IReadOnlyList<RoutineTaskItem> Tasks);
public sealed record RoutineTaskMutationRequest(string OperationId, string ExpectedBusinessDate, int ExpectedVersion, string State);
public sealed record RoutineTaskMutationResult(RoutineTaskItem Occurrence, bool Replayed);
public sealed record RoutineTaskConflictResult(string BusinessDate, RoutineTaskItem? Occurrence, string Error);

public sealed class RoutineOccurrenceDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXp.TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "routine-occurrence";
    [JsonProperty("routineId")] public string RoutineId { get; init; } = "";
    [JsonProperty("businessDate")] public string BusinessDate { get; init; } = "";
    [JsonProperty("state")] public string State { get; init; } = "pending";
    [JsonProperty("version")] public int Version { get; init; }
    [JsonProperty("activeAwardEffort")] public string? ActiveAwardEffort { get; init; }
    [JsonProperty("activeAwardXp")] public int? ActiveAwardXp { get; init; }
    [JsonProperty("updatedAt")] public DateTimeOffset UpdatedAt { get; init; }
    [JsonProperty("_etag")] public string? ETag { get; init; }
}

public sealed class RoutineTransitionDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXp.TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "routine-transition";
    [JsonProperty("operationId")] public string OperationId { get; init; } = "";
    [JsonProperty("routineId")] public string RoutineId { get; init; } = "";
    [JsonProperty("businessDate")] public string BusinessDate { get; init; } = "";
    [JsonProperty("routineName")] public string RoutineName { get; init; } = "";
    [JsonProperty("effort")] public string Effort { get; init; } = "";
    [JsonProperty("configuredXp")] public int ConfiguredXp { get; init; }
    [JsonProperty("xpAmount")] public int XpAmount { get; init; }
    [JsonProperty("previousState")] public string PreviousState { get; init; } = "";
    [JsonProperty("targetState")] public string TargetState { get; init; } = "";
    [JsonProperty("acceptedAt")] public DateTimeOffset AcceptedAt { get; init; }
}

public sealed class RoutineOperationDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXp.TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "routine-operation";
    [JsonProperty("operationId")] public string OperationId { get; init; } = "";
    [JsonProperty("routineId")] public string RoutineId { get; init; } = "";
    [JsonProperty("expectedBusinessDate")] public string ExpectedBusinessDate { get; init; } = "";
    [JsonProperty("expectedVersion")] public int ExpectedVersion { get; init; }
    [JsonProperty("targetState")] public string TargetState { get; init; } = "";
    [JsonProperty("result")] public RoutineTaskItem Result { get; init; } = new("", "", "", 0, "pending", 0);
    [JsonProperty("acceptedAt")] public DateTimeOffset AcceptedAt { get; init; }
}

public sealed class RoutineTaskConflictException(string message, RoutineTaskConflictResult conflict) : Exception(message)
{
    public RoutineTaskConflictResult Conflict { get; } = conflict;
}
