using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
namespace NotionManagementFunctionApp.TaskXp;

public sealed class TaskXpRepository
{
    public const string ProfileId = "personal";
    private readonly Container _container;
    public TaskXpRepository(CosmosClient client, IOptions<TaskXpOptions> options) => _container = client.GetContainer(options.Value.Database, options.Value.Container);

    public async Task RecordAsync(TaskXpInput input, string appliedEffort, int awardXp, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var receiptId = "delivery:" + input.Source + ":" + input.SourceEventId;
            if (await ReadOrDefaultAsync<DeliveryReceiptDocument>(receiptId, cancellationToken) is not null) return;
            var state = await ReadOrDefaultAsync<TaskStateDocument>("task-state:" + input.Task.PageId, cancellationToken);
            if (state is not null && input.OccurredAt < state.LatestOccurredAt)
            {
                await CreateReceiptAsync(receiptId, input, cancellationToken);
                return;
            }
            var total = await ReadOrDefaultAsync<XpTotalDocument>("xp-total", cancellationToken) ?? new XpTotalDocument();
            var completed = string.Equals(input.Task.Status, "Zrobione", StringComparison.Ordinal);
            var changed = state is null || state.IsCompleted != completed;
            var now = DateTimeOffset.UtcNow;
            var activeAward = completed ? (changed ? awardXp : state?.ActiveAwardXp) : null;
            var nextState = new TaskStateDocument { Id = "task-state:" + input.Task.PageId, TaskId = input.Task.PageId, IsCompleted = completed, LatestOccurredAt = input.OccurredAt, CompletionCycle = (state?.CompletionCycle ?? 0) + (changed && completed ? 1 : 0), ActiveAwardXp = activeAward };
            XpEventDocument? xpEvent = null;
            var nextTotal = total.TotalXp;
            if (changed && completed) { xpEvent = Event(input, "award", awardXp, appliedEffort, now); nextTotal += awardXp; }
            else if (changed && state?.ActiveAwardXp is int existingAward) { xpEvent = Event(input, "revoke", -existingAward, appliedEffort, now); nextTotal -= existingAward; }
            var totalDocument = new XpTotalDocument { TotalXp = nextTotal, ETag = total.ETag };
            var batch = _container.CreateTransactionalBatch(new PartitionKey(ProfileId)).CreateItem(new DeliveryReceiptDocument { Id = receiptId, Source = input.Source, SourceEventId = input.SourceEventId, TaskId = input.Task.PageId, OccurredAt = input.OccurredAt, ReceivedAt = now });
            if (state is null) batch.CreateItem(nextState); else batch.ReplaceItem(nextState.Id, nextState, new TransactionalBatchItemRequestOptions { IfMatchEtag = state.ETag });
            if (total.ETag is null) batch.CreateItem(totalDocument); else batch.ReplaceItem(totalDocument.Id, totalDocument, new TransactionalBatchItemRequestOptions { IfMatchEtag = total.ETag });
            if (xpEvent is not null) batch.CreateItem(xpEvent);
            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode) return;
            if (response.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.PreconditionFailed) continue;
            throw new CosmosException("Task XP persistence failed.", response.StatusCode, 0, response.ActivityId, response.RequestCharge);
        }
        throw new CosmosException("Task XP persistence conflicted repeatedly.", System.Net.HttpStatusCode.Conflict, 0, null, 0);
    }
    public async Task<int> GetTotalAsync(CancellationToken cancellationToken) => (await ReadOrDefaultAsync<XpTotalDocument>("xp-total", cancellationToken))?.TotalXp ?? 0;
    public async Task<XpHistoryPage> GetEventsAsync(int limit, string? continuationToken, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.profileId = @profileId AND c.type = 'xp-event' ORDER BY c.occurredAt DESC").WithParameter("@profileId", ProfileId);
        using var iterator = _container.GetItemQueryIterator<XpEventDocument>(query, continuationToken, new QueryRequestOptions { PartitionKey = new PartitionKey(ProfileId), MaxItemCount = limit });
        var page = await iterator.ReadNextAsync(cancellationToken);
        return new XpHistoryPage(page.Select(e => new XpHistoryItem(e.TaskName, e.ChangeType, e.XpAmount, e.Effort, e.OccurredAt, e.Source)).ToArray(), page.ContinuationToken);
    }
    private async Task<T?> ReadOrDefaultAsync<T>(string id, CancellationToken cancellationToken) { try { return (await _container.ReadItemAsync<T>(id, new PartitionKey(ProfileId), cancellationToken: cancellationToken)).Resource; } catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return default; } }
    private async Task CreateReceiptAsync(string id, TaskXpInput input, CancellationToken cancellationToken)
    {
        try { await _container.CreateItemAsync(new DeliveryReceiptDocument { Id = id, Source = input.Source, SourceEventId = input.SourceEventId, TaskId = input.Task.PageId, OccurredAt = input.OccurredAt, ReceivedAt = DateTimeOffset.UtcNow }, new PartitionKey(ProfileId), cancellationToken: cancellationToken); }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict) { }
    }
    private static XpEventDocument Event(TaskXpInput input, string changeType, int amount, string effort, DateTimeOffset now) => new() { Id = "xp-event:" + Guid.NewGuid().ToString("N"), TaskId = input.Task.PageId, TaskName = input.Task.Name, ChangeType = changeType, XpAmount = amount, ObservedEffort = input.Task.Effort, Effort = effort, Source = input.Source, OccurredAt = input.OccurredAt, RecordedAt = now };
}
