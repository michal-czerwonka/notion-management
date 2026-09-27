using Newtonsoft.Json;
using NotionManagementFunctionApp.SendNotionTaskNotifications;
namespace NotionManagementFunctionApp.TaskXp;
public sealed record TaskXpInput(NotionTaskSnapshot Task, string Source, string SourceEventId, DateTimeOffset OccurredAt);
public sealed class TaskStateDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "task-state";
    [JsonProperty("taskId")] public string TaskId { get; init; } = "";
    [JsonProperty("isCompleted")] public bool IsCompleted { get; init; }
    [JsonProperty("latestOccurredAt")] public DateTimeOffset LatestOccurredAt { get; init; }
    [JsonProperty("completionCycle")] public int CompletionCycle { get; init; }
    [JsonProperty("activeAwardXp")] public int? ActiveAwardXp { get; init; }
    [JsonProperty("activeCompletionId")] public string? ActiveCompletionId { get; init; }
    [JsonProperty("activeCompletionBusinessDate")] public string? ActiveCompletionBusinessDate { get; init; }
    [JsonProperty("activeCompletionAt")] public DateTimeOffset? ActiveCompletionAt { get; init; }
    [JsonProperty("_etag")] public string? ETag { get; init; }
}

public sealed class XpTotalDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "xp-total";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "xp-total";
    [JsonProperty("totalXp")] public int TotalXp { get; init; }
    [JsonProperty("_etag")] public string? ETag { get; init; }
}

public sealed class XpProgressDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "xp-progress";
    [JsonProperty("period")] public string Period { get; init; } = "";
    [JsonProperty("periodStart")] public string PeriodStart { get; init; } = "";
    [JsonProperty("periodEndExclusive")] public string PeriodEndExclusive { get; init; } = "";
    [JsonProperty("signedXp")] public int SignedXp { get; init; }
    [JsonProperty("targetXp")] public int TargetXp { get; init; }
    [JsonProperty("completedTasks", NullValueHandling = NullValueHandling.Ignore)] public IReadOnlyList<CompletedTaskSnapshot>? CompletedTasks { get; init; }
    [JsonProperty("_etag")] public string? ETag { get; init; }
}

public sealed record CompletedTaskSnapshot(
    [property: JsonProperty("id")] string Id,
    [property: JsonProperty("subjectType")] string SubjectType,
    [property: JsonProperty("subjectId")] string SubjectId,
    [property: JsonProperty("taskName")] string TaskName,
    [property: JsonProperty("completedAt")] DateTimeOffset CompletedAt,
    [property: JsonProperty("businessDate")] string BusinessDate,
    [property: JsonProperty("observedEffort")] string? ObservedEffort,
    [property: JsonProperty("projects")] IReadOnlyList<string>? Projects);

public sealed class XpEventDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "xp-event";
    [JsonProperty("taskId")] public string TaskId { get; init; } = "";
    [JsonProperty("taskName")] public string TaskName { get; init; } = "";
    [JsonProperty("subjectType")] public string SubjectType { get; init; } = "task";
    [JsonProperty("changeType")] public string ChangeType { get; init; } = "";
    [JsonProperty("xpAmount")] public int XpAmount { get; init; }
    [JsonProperty("observedEffort")] public string? ObservedEffort { get; init; }
    [JsonProperty("effort")] public string Effort { get; init; } = "";
    [JsonProperty("source")] public string Source { get; init; } = "";
    [JsonProperty("occurredAt")] public DateTimeOffset OccurredAt { get; init; }
    [JsonProperty("recordedAt")] public DateTimeOffset RecordedAt { get; init; }
}

public sealed class DeliveryReceiptDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "delivery-receipt";
    [JsonProperty("source")] public string Source { get; init; } = "";
    [JsonProperty("sourceEventId")] public string SourceEventId { get; init; } = "";
    [JsonProperty("taskId")] public string TaskId { get; init; } = "";
    [JsonProperty("occurredAt")] public DateTimeOffset OccurredAt { get; init; }
    [JsonProperty("receivedAt")] public DateTimeOffset ReceivedAt { get; init; }
}
public sealed record XpHistoryItem(string TaskName, string SubjectType, string ChangeType, int XpAmount, string Effort, DateTimeOffset OccurredAt, string Source);
public sealed record XpHistoryPage(IReadOnlyList<XpHistoryItem> Events, string? ContinuationToken);
public sealed record XpProgressResult(string Period, string PeriodStart, string PeriodEndExclusive, int EarnedXp, int TargetXp, string? PreviousPeriodStart,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CompletedTaskSnapshot>? CompletedTasks);
public sealed record XpChartDay(string Date, int EarnedXp);
public sealed record XpChartResult(string StartDate, string EndDate, int ChartMaxXp, IReadOnlyList<XpChartDay> Days);
public sealed class XpChartDayProjection
{
    [JsonProperty("periodStart")] public string? PeriodStart { get; init; }
    [JsonProperty("signedXp")] public int? SignedXp { get; init; }
}
