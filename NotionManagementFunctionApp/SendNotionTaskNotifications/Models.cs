namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed record NotionTodayTask(string PageId, string Name, string Status, IReadOnlyList<string> Projects, string? Url);
public sealed record TodayTask(string Id, string Name, string Status, IReadOnlyList<string> Projects);
public sealed record TodayTaskStatus(string Name, string Color);
public sealed record TodayTasksResponse(IReadOnlyList<TodayTask> Tasks, IReadOnlyList<TodayTaskStatus> Statuses);
public sealed record UpdateTodayTaskStatusRequest(string? Status);

public sealed record NotionViewReference(string Id, string Name);

public sealed record NtfyPublishResult(string? MessageId, bool TopicConfigured);

public sealed record SendNotionTaskNotificationsResult(int TaskCount, bool NotificationSent, string? NtfyMessageId);
