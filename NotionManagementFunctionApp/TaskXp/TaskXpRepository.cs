using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
namespace NotionManagementFunctionApp.TaskXp;

public sealed class TaskXpRepository
{
    public const string ProfileId = "personal";
    private readonly Container _container;
    private readonly ILogger<TaskXpRepository> _logger;
    private readonly BusinessPeriodCalculator _periods;
    private readonly DailyTargetSchedule _targets;
    public TaskXpRepository(CosmosClient client, IOptions<TaskXpOptions> options, ILogger<TaskXpRepository> logger, BusinessPeriodCalculator periods, DailyTargetSchedule targets)
    {
        _container = client.GetContainer(options.Value.Database, options.Value.Container);
        _logger = logger;
        _periods = periods;
        _targets = targets;
    }

    public async Task RecordAsync(TaskXpInput input, string appliedEffort, int awardXp, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recording Task XP delivery. Source={Source}, SourceEventId={SourceEventId}, TaskId={TaskId}, Status={Status}, OccurredAt={OccurredAt}", input.Source, input.SourceEventId, input.Task.PageId, input.Task.Status, input.OccurredAt);
        try
        {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var receiptId = "delivery:" + input.Source + ":" + input.SourceEventId;
            if (await ReadOrDefaultAsync<DeliveryReceiptDocument>(receiptId, cancellationToken) is not null)
            {
                _logger.LogInformation("Ignoring duplicate Task XP delivery. Source={Source}, SourceEventId={SourceEventId}, TaskId={TaskId}", input.Source, input.SourceEventId, input.Task.PageId);
                return;
            }
            var state = await ReadOrDefaultAsync<TaskStateDocument>("task-state:" + input.Task.PageId, cancellationToken);
            if (state is not null && input.OccurredAt < state.LatestOccurredAt)
            {
                await CreateReceiptAsync(receiptId, input, cancellationToken);
                _logger.LogInformation("Recorded stale Task XP delivery without changing progression. Source={Source}, SourceEventId={SourceEventId}, TaskId={TaskId}", input.Source, input.SourceEventId, input.Task.PageId);
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
            var progress = xpEvent is null ? [] : await ReadProgressAsync(_periods.ForEvent(xpEvent.OccurredAt), cancellationToken);
            var batch = _container.CreateTransactionalBatch(new PartitionKey(ProfileId)).CreateItem(new DeliveryReceiptDocument { Id = receiptId, Source = input.Source, SourceEventId = input.SourceEventId, TaskId = input.Task.PageId, OccurredAt = input.OccurredAt, ReceivedAt = now });
            if (state is null) batch.CreateItem(nextState); else batch.ReplaceItem(nextState.Id, nextState, new TransactionalBatchItemRequestOptions { IfMatchEtag = state.ETag });
            if (total.ETag is null) batch.CreateItem(totalDocument); else batch.ReplaceItem(totalDocument.Id, totalDocument, new TransactionalBatchItemRequestOptions { IfMatchEtag = total.ETag });
            if (xpEvent is not null) batch.CreateItem(xpEvent);
            if (xpEvent is not null)
            {
                foreach (var period in _periods.ForEvent(xpEvent.OccurredAt))
                {
                    var existing = progress.Single(item => item.Period.Id == period.Id).Document;
                    var next = new XpProgressDocument
                    {
                        Id = period.Id, Period = period.Period, PeriodStart = period.Start.ToString("yyyy-MM-dd"),
                        PeriodEndExclusive = period.EndExclusive.ToString("yyyy-MM-dd"), SignedXp = (existing?.SignedXp ?? 0) + xpEvent.XpAmount,
                        TargetXp = existing?.TargetXp ?? _targets.TargetFor(period), ETag = existing?.ETag
                    };
                    if (existing is null) batch.CreateItem(next); else batch.ReplaceItem(next.Id, next, new TransactionalBatchItemRequestOptions { IfMatchEtag = existing.ETag });
                }
            }
            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Recorded Task XP delivery. Source={Source}, SourceEventId={SourceEventId}, TaskId={TaskId}, Changed={Changed}, XpAmount={XpAmount}, StatusCode={StatusCode}, ActivityId={ActivityId}, RequestCharge={RequestCharge}", input.Source, input.SourceEventId, input.Task.PageId, changed, xpEvent?.XpAmount ?? 0, (int)response.StatusCode, response.ActivityId, response.RequestCharge);
                return;
            }
            if (response.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.PreconditionFailed)
            {
                _logger.LogWarning("Retrying conflicted Task XP batch. Attempt={Attempt}, Source={Source}, SourceEventId={SourceEventId}, TaskId={TaskId}, StatusCode={StatusCode}, ActivityId={ActivityId}", attempt + 1, input.Source, input.SourceEventId, input.Task.PageId, (int)response.StatusCode, response.ActivityId);
                continue;
            }
            throw new CosmosException("Task XP persistence failed.", response.StatusCode, 0, response.ActivityId, response.RequestCharge);
        }
        throw new CosmosException("Task XP persistence conflicted repeatedly.", System.Net.HttpStatusCode.Conflict, 0, null, 0);
        }
        catch (CosmosException exception)
        {
            _logger.LogError(exception, "Task XP Cosmos write failed. Source={Source}, SourceEventId={SourceEventId}, TaskId={TaskId}, StatusCode={StatusCode}, ActivityId={ActivityId}, RequestCharge={RequestCharge}", input.Source, input.SourceEventId, input.Task.PageId, (int)exception.StatusCode, exception.ActivityId, exception.RequestCharge);
            throw;
        }
    }
    public async Task<int> GetTotalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var total = (await ReadOrDefaultAsync<XpTotalDocument>("xp-total", cancellationToken))?.TotalXp ?? 0;
            _logger.LogInformation("Read Task XP total. TotalXp={TotalXp}", total);
            return total;
        }
        catch (CosmosException exception)
        {
            _logger.LogError(exception, "Task XP Cosmos total read failed. StatusCode={StatusCode}, ActivityId={ActivityId}, RequestCharge={RequestCharge}", (int)exception.StatusCode, exception.ActivityId, exception.RequestCharge);
            throw;
        }
    }
    public async Task<XpHistoryPage> GetEventsAsync(int limit, string? continuationToken, CancellationToken cancellationToken)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.profileId = @profileId AND c.type = 'xp-event' ORDER BY c.occurredAt DESC").WithParameter("@profileId", ProfileId);
            using var iterator = _container.GetItemQueryIterator<XpEventDocument>(query, continuationToken, new QueryRequestOptions { PartitionKey = new PartitionKey(ProfileId), MaxItemCount = limit });
            var page = await iterator.ReadNextAsync(cancellationToken);
            _logger.LogInformation("Read Task XP event history. EventCount={EventCount}, HasContinuationToken={HasContinuationToken}, RequestCharge={RequestCharge}", page.Count, page.ContinuationToken is not null, page.RequestCharge);
            return new XpHistoryPage(page.Select(e => new XpHistoryItem(e.TaskName, e.ChangeType, e.XpAmount, e.Effort, e.OccurredAt, e.Source)).ToArray(), page.ContinuationToken);
        }
        catch (CosmosException exception)
        {
            _logger.LogError(exception, "Task XP Cosmos event-history read failed. StatusCode={StatusCode}, ActivityId={ActivityId}, RequestCharge={RequestCharge}", (int)exception.StatusCode, exception.ActivityId, exception.RequestCharge);
            throw;
        }
    }
    public async Task<XpProgressResult> GetProgressAsync(BusinessPeriod period, CancellationToken cancellationToken)
    {
        var document = await ReadOrDefaultAsync<XpProgressDocument>(period.Id, cancellationToken);
        var previous = Previous(period);
        return new XpProgressResult(period.Period, period.Start.ToString("yyyy-MM-dd"), period.EndExclusive.ToString("yyyy-MM-dd"), Math.Max(0, document?.SignedXp ?? 0), document?.TargetXp ?? _targets.TargetFor(period), previous?.Start.ToString("yyyy-MM-dd"));
    }

    public async Task ReconcileTargetsAsync(DateOnly businessDate, CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<string, BusinessPeriod>();
        for (var date = _targets.FirstEffectiveDate; date <= businessDate; date = date.AddDays(1))
        {
            foreach (var period in _periods.ForEvent(ToBusinessInstant(date))) candidates.TryAdd(period.Id, period);
        }
        foreach (var period in candidates.Values) await ReconcileTargetAsync(period, period.EndExclusive > businessDate, cancellationToken);
    }

    private async Task ReconcileTargetAsync(BusinessPeriod period, bool isOpen, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var existing = await ReadOrDefaultAsync<XpProgressDocument>(period.Id, cancellationToken);
            if (existing is not null && !isOpen) return;
            var document = new XpProgressDocument
            {
                Id = period.Id, Period = period.Period, PeriodStart = period.Start.ToString("yyyy-MM-dd"), PeriodEndExclusive = period.EndExclusive.ToString("yyyy-MM-dd"),
                SignedXp = existing?.SignedXp ?? 0, TargetXp = _targets.TargetFor(period), ETag = existing?.ETag
            };
            try
            {
                var response = existing is null
                    ? await _container.CreateItemAsync(document, new PartitionKey(ProfileId), cancellationToken: cancellationToken)
                    : await _container.ReplaceItemAsync(document, document.Id, new PartitionKey(ProfileId), new ItemRequestOptions { IfMatchEtag = existing.ETag }, cancellationToken);
                _logger.LogInformation("Reconciled Task XP period target. PeriodId={PeriodId}, RequestCharge={RequestCharge}", period.Id, response.RequestCharge);
                return;
            }
            catch (CosmosException exception) when (exception.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.PreconditionFailed && attempt < 3) { }
        }
        throw new CosmosException("Task XP target reconciliation conflicted repeatedly.", System.Net.HttpStatusCode.Conflict, 0, null, 0);
    }

    private BusinessPeriod? Previous(BusinessPeriod period)
    {
        var previous = period.Period switch
        {
            "day" => _periods.For("day", period.Start.AddDays(-1)),
            "week" => _periods.For("week", period.Start.AddDays(-7)),
            "month" => _periods.For("month", period.Start.AddMonths(-1)),
            "year" => _periods.For("year", period.Start.AddYears(-1)),
            _ => null
        };
        return previous is not null && previous.EndExclusive > _targets.FirstEffectiveDate ? previous : null;
    }

    private async Task<List<(BusinessPeriod Period, XpProgressDocument? Document)>> ReadProgressAsync(IReadOnlyList<BusinessPeriod> periods, CancellationToken cancellationToken)
    {
        var result = new List<(BusinessPeriod, XpProgressDocument?)>();
        foreach (var period in periods) result.Add((period, await ReadOrDefaultAsync<XpProgressDocument>(period.Id, cancellationToken)));
        return result;
    }
    private static DateTimeOffset ToBusinessInstant(DateOnly date) => new(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
    private async Task<T?> ReadOrDefaultAsync<T>(string id, CancellationToken cancellationToken) { try { return (await _container.ReadItemAsync<T>(id, new PartitionKey(ProfileId), cancellationToken: cancellationToken)).Resource; } catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return default; } }
    private async Task CreateReceiptAsync(string id, TaskXpInput input, CancellationToken cancellationToken)
    {
        try { await _container.CreateItemAsync(new DeliveryReceiptDocument { Id = id, Source = input.Source, SourceEventId = input.SourceEventId, TaskId = input.Task.PageId, OccurredAt = input.OccurredAt, ReceivedAt = DateTimeOffset.UtcNow }, new PartitionKey(ProfileId), cancellationToken: cancellationToken); }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict) { }
    }
    private static XpEventDocument Event(TaskXpInput input, string changeType, int amount, string effort, DateTimeOffset now) => new() { Id = "xp-event:" + Guid.NewGuid().ToString("N"), TaskId = input.Task.PageId, TaskName = input.Task.Name, ChangeType = changeType, XpAmount = amount, ObservedEffort = input.Task.Effort, Effort = effort, Source = input.Source, OccurredAt = input.OccurredAt, RecordedAt = now };
}
