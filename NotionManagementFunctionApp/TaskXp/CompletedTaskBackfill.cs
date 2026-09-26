using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NotionManagementFunctionApp.RoutineTasks;

namespace NotionManagementFunctionApp.TaskXp;

public sealed record BackfillIssue(string EventId, string Reason);
public sealed record BackfillReport(int CandidateSnapshots, int ExistingSnapshots, int AppliedSnapshots,
    int CandidateXpAdjustments, int AppliedXpAdjustments, long LargestProjectedItemBytes, long LargestProjectedBatchBytes,
    IReadOnlyList<BackfillIssue> Skipped);

public sealed class CompletedTaskBackfill
{
    private readonly Container _container;
    private readonly BusinessPeriodCalculator _periods;
    private readonly DailyTargetSchedule _targets;

    public CompletedTaskBackfill(CosmosClient client, IOptions<TaskXpOptions> options, BusinessPeriodCalculator periods, DailyTargetSchedule targets)
    {
        _container = client.GetContainer(options.Value.Database, options.Value.Container);
        _periods = periods;
        _targets = targets;
    }

    public async Task<BackfillReport> RunAsync(bool apply, CancellationToken cancellationToken)
    {
        var events = await ReadEventsAsync(cancellationToken);
        var transitions = await ReadTransitionsAsync(cancellationToken);
        var issues = new List<BackfillIssue>();
        var snapshots = new List<CompletedTaskSnapshot>();
        var corrections = new List<(XpEventDocument Revoke, int Amount)>();
        foreach (var group in events.GroupBy(item =>
        {
            var subjectType = string.IsNullOrWhiteSpace(item.SubjectType) ? "task" : item.SubjectType;
            return (SubjectType: subjectType, item.TaskId,
                RoutineBusinessDate: subjectType == "routine" ? _periods.BusinessDate(item.OccurredAt).ToString("yyyy-MM-dd") : "");
        }))
        {
            XpEventDocument? active = null;
            foreach (var item in group.OrderBy(item => item.OccurredAt).ThenBy(item => item.RecordedAt).ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                if (item.ChangeType == "award")
                {
                    if (active is not null)
                    {
                        var day = _periods.For("day", _periods.BusinessDate(active.OccurredAt));
                        var document = await ReadAsync<XpProgressDocument>(day.Id, cancellationToken);
                        if (document?.CompletedTasks?.Any(snapshot => snapshot.Id == active.Id) == true)
                            snapshots.Add(Snapshot(active, group.Key.SubjectType));
                        else issues.Add(new BackfillIssue(active.Id, "A second award appeared without a matching revoke and no persisted snapshot; the earlier award is ambiguous."));
                    }
                    active = item;
                    continue;
                }
                if (item.ChangeType != "revoke") continue;
                if (active is null)
                {
                    issues.Add(new BackfillIssue(item.Id, "No preceding award was found; XP was left unchanged."));
                    continue;
                }
                if (item.OccurredAt < active.OccurredAt || item.XpAmount != -active.XpAmount)
                {
                    issues.Add(new BackfillIssue(item.Id, "The revoke cannot be paired unambiguously with the preceding award; XP was left unchanged."));
                    active = null;
                    continue;
                }
                if (group.Key.SubjectType == "routine" && !HasMatchingRoutineTransition(transitions, active))
                {
                    issues.Add(new BackfillIssue(active.Id, "Routine transition does not confirm this award."));
                    active = null;
                    continue;
                }
                if (_periods.BusinessDate(item.OccurredAt) != _periods.BusinessDate(active.OccurredAt))
                {
                    snapshots.Add(Snapshot(active, group.Key.SubjectType));
                    if (!await HasAdjustmentAsync(item.Id, cancellationToken)) corrections.Add((item, active.XpAmount));
                }
                active = null;
            }
            if (active is not null)
            {
                if (group.Key.SubjectType == "routine" && !HasMatchingRoutineTransition(transitions, active))
                    issues.Add(new BackfillIssue(active.Id, "Routine transition does not confirm this award."));
                else snapshots.Add(Snapshot(active, group.Key.SubjectType));
            }
        }

        var existing = 0;
        var applied = 0;
        long largest = 0;
        long largestBatch = 0;
        var projectedSizes = new Dictionary<string, long>();
        foreach (var snapshot in snapshots)
        {
            var periods = _periods.ForEvent(snapshot.CompletedAt).Where(period => period.Period != "year").ToArray();
            var accepted = await IsAcceptedAsync(snapshot, cancellationToken);
            if (accepted is null)
            {
                issues.Add(new BackfillIssue(snapshot.Id, "The award event is missing from current history; no snapshot was inferred."));
                continue;
            }
            if (!accepted.Value)
            {
                issues.Add(new BackfillIssue(snapshot.Id, "A same-business-day revoke invalidated this completion."));
                if (apply) await ReconcileSnapshotAsync(snapshot, periods, cancellationToken);
                continue;
            }
            var present = await ReadAsync<XpProgressDocument>(periods[0].Id, cancellationToken);
            if (present?.CompletedTasks?.Any(item => item.Id == snapshot.Id) == true)
            {
                if (!apply) existing++;
                else
                {
                    var result = await ReconcileSnapshotAsync(snapshot, periods, cancellationToken);
                    if (result is null) issues.Add(new BackfillIssue(snapshot.Id, "A same-business-day correction invalidated this completion during backfill."));
                    else existing++;
                }
                continue;
            }
            long batchBytes = 0;
            foreach (var period in periods)
            {
                var document = await ReadAsync<XpProgressDocument>(period.Id, cancellationToken);
                if (!projectedSizes.TryGetValue(period.Id, out var bytes))
                    bytes = document is null ? 200 : System.Text.Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(document));
                bytes += System.Text.Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(snapshot)) + 1;
                projectedSizes[period.Id] = bytes;
                largest = Math.Max(largest, bytes);
                batchBytes += bytes;
                if (bytes >= 2_000_000)
                {
                    issues.Add(new BackfillIssue(snapshot.Id, $"Projected {period.Id} item exceeds the Cosmos 2 MB limit."));
                    if (apply) throw new InvalidOperationException($"Projected {period.Id} item exceeds the Cosmos 2 MB limit.");
                }
            }
            largestBatch = Math.Max(largestBatch, batchBytes);
            if (batchBytes >= 2_000_000)
            {
                issues.Add(new BackfillIssue(snapshot.Id, "Projected transactional batch exceeds the Cosmos 2 MB limit."));
                if (apply) throw new InvalidOperationException("Projected transactional batch exceeds the Cosmos 2 MB limit.");
            }
            if (apply)
            {
                var result = await ReconcileSnapshotAsync(snapshot, periods, cancellationToken);
                if (result is null) issues.Add(new BackfillIssue(snapshot.Id, "A same-business-day correction invalidated this completion during backfill."));
                else if (result.Value) applied++;
                else existing++;
            }
        }
        var adjustmentsApplied = 0;
        if (apply)
        {
            foreach (var correction in corrections)
            {
                if (await AdjustXpAsync(correction.Revoke, correction.Amount, cancellationToken)) adjustmentsApplied++;
            }
            foreach (var snapshot in snapshots)
            {
                var periods = _periods.ForEvent(snapshot.CompletedAt).Where(period => period.Period != "year").ToArray();
                await ReconcileSnapshotAsync(snapshot, periods, cancellationToken);
            }
            foreach (var correction in corrections)
                if (!await HasAdjustmentAsync(correction.Revoke.Id, cancellationToken))
                    throw new InvalidOperationException($"Backfill reconciliation found missing XP adjustment {correction.Revoke.Id}.");
            await _container.UpsertItemAsync(new BackfillCompletionDocument
            {
                Id = "completed-task-backfill:latest", CompletedAt = DateTimeOffset.UtcNow,
                CandidateSnapshots = snapshots.Count, Skipped = issues.Count
            }, new PartitionKey(TaskXpRepository.ProfileId), cancellationToken: cancellationToken);
        }
        return new BackfillReport(snapshots.Count, existing, applied, corrections.Count, adjustmentsApplied, largest, largestBatch, issues);
    }

    private CompletedTaskSnapshot Snapshot(XpEventDocument item, string subjectType) =>
        new(item.Id, subjectType, item.TaskId, item.TaskName, item.OccurredAt,
            _periods.BusinessDate(item.OccurredAt).ToString("yyyy-MM-dd"), item.ObservedEffort, null);

    private bool HasMatchingRoutineTransition(IReadOnlyList<NotionManagementFunctionApp.RoutineTasks.RoutineTransitionDocument> transitions, XpEventDocument award) =>
        !transitions.Any(item => item.RoutineId == award.TaskId && item.BusinessDate == _periods.BusinessDate(award.OccurredAt).ToString("yyyy-MM-dd")) ||
        transitions.Any(item => item.RoutineId == award.TaskId && item.AcceptedAt == award.OccurredAt && item.XpAmount == award.XpAmount);

    private async Task<bool?> IsAcceptedAsync(CompletedTaskSnapshot snapshot, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.profileId = @profileId AND c.type = 'xp-event' AND c.taskId = @subjectId")
            .WithParameter("@profileId", TaskXpRepository.ProfileId).WithParameter("@subjectId", snapshot.SubjectId);
        using var iterator = _container.GetItemQueryIterator<XpEventDocument>(query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(TaskXpRepository.ProfileId), MaxItemCount = 100 });
        var events = new List<XpEventDocument>();
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync(cancellationToken))
            {
                var subjectType = string.IsNullOrWhiteSpace(item.SubjectType) ? "task" : item.SubjectType;
                if (subjectType == snapshot.SubjectType &&
                    (subjectType != "routine" || _periods.BusinessDate(item.OccurredAt).ToString("yyyy-MM-dd") == snapshot.BusinessDate))
                    events.Add(item);
            }
        }
        var ordered = events.OrderBy(item => item.OccurredAt).ThenBy(item => item.RecordedAt)
            .ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var index = Array.FindIndex(ordered, item => item.Id == snapshot.Id && item.ChangeType == "award");
        if (index < 0) return null;
        for (var next = index + 1; next < ordered.Length; next++)
        {
            if (ordered[next].ChangeType == "award") break;
            if (ordered[next].ChangeType == "revoke")
                return _periods.BusinessDate(ordered[next].OccurredAt) != _periods.BusinessDate(snapshot.CompletedAt);
        }
        return true;
    }

    private async Task<CompletionGuard> ReadGuardAsync(CompletedTaskSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.SubjectType == "routine")
        {
            var occurrenceId = $"routine-occurrence:{snapshot.BusinessDate}:{snapshot.SubjectId}";
            var occurrence = await ReadAsync<RoutineOccurrenceDocument>(occurrenceId, cancellationToken)
                ?? throw new InvalidOperationException($"Routine occurrence {occurrenceId} is missing during backfill.");
            return new CompletionGuard(null, occurrence);
        }
        var stateId = "task-state:" + snapshot.SubjectId;
        var state = await ReadAsync<TaskStateDocument>(stateId, cancellationToken)
            ?? throw new InvalidOperationException($"Task state {stateId} is missing during backfill.");
        return new CompletionGuard(state, null);
    }

    private async Task<bool?> ReconcileSnapshotAsync(CompletedTaskSnapshot snapshot, IReadOnlyList<BusinessPeriod> periods, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            // Read the state before history so its ETag also guards corrections arriving during validation.
            var guard = await ReadGuardAsync(snapshot, cancellationToken);
            var accepted = await IsAcceptedAsync(snapshot, cancellationToken);
            if (accepted is null) throw new InvalidOperationException($"Award event {snapshot.Id} disappeared during backfill.");
            var documents = new List<(BusinessPeriod Period, XpProgressDocument? Existing, XpProgressDocument Next)>();
            var added = false;
            foreach (var period in periods)
            {
                var existing = await ReadAsync<XpProgressDocument>(period.Id, cancellationToken);
                var items = (existing?.CompletedTasks ?? []).ToList();
                var present = items.Any(item => item.Id == snapshot.Id);
                if (accepted.Value && !present) { items.Add(snapshot); added = true; }
                if (!accepted.Value && present) items.RemoveAll(item => item.Id == snapshot.Id);
                if (accepted.Value == present) continue;
                var next = new XpProgressDocument { Id = period.Id, Period = period.Period,
                    PeriodStart = period.Start.ToString("yyyy-MM-dd"), PeriodEndExclusive = period.EndExclusive.ToString("yyyy-MM-dd"),
                    SignedXp = existing?.SignedXp ?? 0, TargetXp = existing?.TargetXp ?? _targets.TargetFor(period), CompletedTasks = items };
                if (System.Text.Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(next)) >= 2_000_000)
                    throw new InvalidOperationException($"Backfill would exceed Cosmos item size for {period.Id}.");
                documents.Add((period, existing, next));
            }
            if (documents.Count == 0) return accepted.Value ? false : null;
            var batch = _container.CreateTransactionalBatch(new PartitionKey(TaskXpRepository.ProfileId));
            if (guard.Task is not null)
                batch.ReplaceItem(guard.Task.Id, guard.Task, new TransactionalBatchItemRequestOptions { IfMatchEtag = guard.Task.ETag });
            else if (guard.Routine is not null)
                batch.ReplaceItem(guard.Routine.Id, guard.Routine, new TransactionalBatchItemRequestOptions { IfMatchEtag = guard.Routine.ETag });
            foreach (var item in documents)
            {
                if (item.Existing is null) batch.CreateItem(item.Next);
                else batch.ReplaceItem(item.Period.Id, item.Next, new TransactionalBatchItemRequestOptions { IfMatchEtag = item.Existing.ETag });
            }
            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode) return accepted.Value ? added : null;
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed) continue;
            throw new CosmosException("Completion backfill failed.", response.StatusCode, 0, response.ActivityId, response.RequestCharge);
        }
        throw new InvalidOperationException($"Completion backfill repeatedly conflicted for {snapshot.Id}.");
    }

    private sealed record CompletionGuard(TaskStateDocument? Task, RoutineOccurrenceDocument? Routine);

    private async Task<bool> AdjustXpAsync(XpEventDocument revoke, int amount, CancellationToken cancellationToken)
    {
        var markerId = "completion-backfill-adjustment:" + revoke.Id;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (await HasAdjustmentAsync(revoke.Id, cancellationToken)) return false;
            var total = await ReadAsync<XpTotalDocument>("xp-total", cancellationToken) ?? throw new InvalidOperationException("XP total is missing.");
            var periods = new List<(BusinessPeriod Period, XpProgressDocument Existing)>();
            foreach (var period in _periods.ForEvent(revoke.OccurredAt))
                periods.Add((period, await ReadAsync<XpProgressDocument>(period.Id, cancellationToken) ?? throw new InvalidOperationException($"XP period {period.Id} is missing.")));
            var batch = _container.CreateTransactionalBatch(new PartitionKey(TaskXpRepository.ProfileId))
                .CreateItem(new BackfillAdjustmentDocument { Id = markerId, RevokeEventId = revoke.Id, Amount = amount, AppliedAt = DateTimeOffset.UtcNow })
                .ReplaceItem(total.Id, new XpTotalDocument { TotalXp = total.TotalXp + amount }, new TransactionalBatchItemRequestOptions { IfMatchEtag = total.ETag });
            foreach (var item in periods)
            {
                var old = item.Existing;
                batch.ReplaceItem(old.Id, new XpProgressDocument { Id = old.Id, Period = old.Period, PeriodStart = old.PeriodStart,
                    PeriodEndExclusive = old.PeriodEndExclusive, SignedXp = old.SignedXp + amount,
                    TargetXp = old.TargetXp, CompletedTasks = old.CompletedTasks }, new TransactionalBatchItemRequestOptions { IfMatchEtag = old.ETag });
            }
            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode) return true;
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed) continue;
            throw new CosmosException("XP backfill adjustment failed.", response.StatusCode, 0, response.ActivityId, response.RequestCharge);
        }
        throw new InvalidOperationException($"XP adjustment repeatedly conflicted for {revoke.Id}.");
    }

    private Task<bool> HasAdjustmentAsync(string eventId, CancellationToken cancellationToken) =>
        ExistsAsync("completion-backfill-adjustment:" + eventId, cancellationToken);

    private async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) => await ReadAsync<BackfillAdjustmentDocument>(id, cancellationToken) is not null;

    private async Task<T?> ReadAsync<T>(string id, CancellationToken cancellationToken)
    {
        try { return (await _container.ReadItemAsync<T>(id, new PartitionKey(TaskXpRepository.ProfileId), cancellationToken: cancellationToken)).Resource; }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound) { return default; }
    }

    private async Task<List<T>> QueryAsync<T>(string type, CancellationToken cancellationToken)
    {
        var result = new List<T>();
        var query = new QueryDefinition("SELECT * FROM c WHERE c.profileId = @profileId AND c.type = @type")
            .WithParameter("@profileId", TaskXpRepository.ProfileId).WithParameter("@type", type);
        using var iterator = _container.GetItemQueryIterator<T>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(TaskXpRepository.ProfileId), MaxItemCount = 100 });
        while (iterator.HasMoreResults) result.AddRange(await iterator.ReadNextAsync(cancellationToken));
        return result;
    }

    private Task<List<XpEventDocument>> ReadEventsAsync(CancellationToken cancellationToken) => QueryAsync<XpEventDocument>("xp-event", cancellationToken);
    private Task<List<NotionManagementFunctionApp.RoutineTasks.RoutineTransitionDocument>> ReadTransitionsAsync(CancellationToken cancellationToken) => QueryAsync<NotionManagementFunctionApp.RoutineTasks.RoutineTransitionDocument>("routine-transition", cancellationToken);
}

public sealed class BackfillAdjustmentDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "completion-backfill-adjustment";
    [JsonProperty("revokeEventId")] public string RevokeEventId { get; init; } = "";
    [JsonProperty("amount")] public int Amount { get; init; }
    [JsonProperty("appliedAt")] public DateTimeOffset AppliedAt { get; init; }
}

public sealed class BackfillCompletionDocument
{
    [JsonProperty("id")] public string Id { get; init; } = "";
    [JsonProperty("profileId")] public string ProfileId { get; init; } = TaskXpRepository.ProfileId;
    [JsonProperty("type")] public string Type { get; init; } = "completed-task-backfill";
    [JsonProperty("completedAt")] public DateTimeOffset CompletedAt { get; init; }
    [JsonProperty("candidateSnapshots")] public int CandidateSnapshots { get; init; }
    [JsonProperty("skipped")] public int Skipped { get; init; }
}
