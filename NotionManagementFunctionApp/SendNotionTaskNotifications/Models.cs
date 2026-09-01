namespace NotionManagementFunctionApp.SendNotionTaskNotifications;

public sealed record NotionTodayTask(string PageId, string Name, string Status, string? Url);

public sealed record NotionViewReference(string Id, string Name);

public sealed record NtfyPublishResult(string? MessageId, bool TopicConfigured);

public sealed record SendNotionTaskNotificationsResult(int TaskCount, bool NotificationSent, string? NtfyMessageId);
