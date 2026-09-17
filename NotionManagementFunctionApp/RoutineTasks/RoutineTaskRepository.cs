using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotionManagementFunctionApp.TaskXp;

namespace NotionManagementFunctionApp.RoutineTasks;

public sealed class RoutineTaskRepository
{
    private readonly Container _container;
    private readonly BusinessPeriodCalculator _periods;
    private readonly DailyTargetSchedule _targets;
    private readonly ILogger<RoutineTaskRepository> _logger;

    public RoutineTaskRepository(CosmosClient client, IOptions<TaskXpOptions> options, BusinessPeriodCalculator periods, DailyTargetSchedule targets, ILogger<RoutineTaskRepository> logger)
    {
        _container = client.GetContainer(options.Value.Database, options.Value.Container);
        _periods = periods;
        _targets = targets;
        _logger = logger;
    }

    public Task<RoutineOccurrenceDocument?> GetOccurrenceAsync(DateOnly businessDate, string routineId, CancellationToken cancellationToken) =>
        ReadOrDefaultAsync<RoutineOccurrenceDocument>(OccurrenceId(businessDate, routineId), cancellationToken);

    public async Task<RoutineTaskMutationResult?> ReplayAsync(DateOnly currentBusinessDate, string routineId, RoutineTaskMutationRequest request, CancellationToken cancellationToken)
    {
        var operation = await ReadOrDefaultAsync<RoutineOperationDocument>(OperationId(request.OperationId), cancellationToken);
        if (operation is null) return null;
        if (Matches(operation, routineId, request)) return new RoutineTaskMutationResult(operation.Result, true);
        throw Conflict(currentBusinessDate.ToString("yyyy-MM-dd"), null, "The operation ID was already used for a different request.");
    }

    public async Task<RoutineTaskMutationResult> MutateAsync(DateOnly businessDate, RoutineTaskDefinition definition, int configuredXp, RoutineTaskMutationRequest request, DateTimeOffset acceptedAt, CancellationToken cancellationToken)
    {
        var businessDateText = businessDate.ToString("yyyy-MM-dd");
        var operationDocumentId = OperationId(request.OperationId);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var existingOperation = await ReadOrDefaultAsync<RoutineOperationDocument>(operationDocumentId, cancellationToken);
            if (existingOperation is not null)
            {
                if (Matches(existingOperation, definition.Id, request)) return new RoutineTaskMutationResult(existingOperation.Result, true);
                throw Conflict(businessDateText, null, "The operation ID was already used for a different request.");
            }
            if (!string.Equals(request.ExpectedBusinessDate, businessDateText, StringComparison.Ordinal)) throw Conflict(businessDateText, null, "The routine business date has changed.");

            var occurrence = await GetOccurrenceAsync(businessDate, definition.Id, cancellationToken);
            var currentState = occurrence?.State ?? "pending";
            var currentVersion = occurrence?.Version ?? 0;
            if (request.ExpectedVersion != currentVersion)
            {
                throw Conflict(businessDateText, Item(definition, configuredXp, currentState, currentVersion), "The routine was changed by another request.");
            }

            var stateChanged = !string.Equals(currentState, request.State, StringComparison.Ordinal);
            var nextVersion = currentVersion + (stateChanged ? 1 : 0);
            var activeEffort = occurrence?.ActiveAwardEffort;
            var activeXp = occurrence?.ActiveAwardXp;
            var xpAmount = 0;
            if (stateChanged && string.Equals(currentState, "completed", StringComparison.Ordinal))
            {
                if (activeXp is null || string.IsNullOrWhiteSpace(activeEffort)) throw new InvalidOperationException("A completed routine occurrence has no active XP award snapshot.");
                xpAmount -= activeXp.Value;
                activeXp = null;
                activeEffort = null;
            }
            if (stateChanged && string.Equals(request.State, "completed", StringComparison.Ordinal))
            {
                xpAmount += configuredXp;
                activeXp = configuredXp;
                activeEffort = definition.Effort;
            }

            var resultItem = Item(definition, configuredXp, request.State, nextVersion);
            var nextOccurrence = new RoutineOccurrenceDocument
            {
                Id = OccurrenceId(businessDate, definition.Id), RoutineId = definition.Id, BusinessDate = businessDateText,
                State = request.State, Version = nextVersion, ActiveAwardEffort = activeEffort, ActiveAwardXp = activeXp,
                UpdatedAt = stateChanged ? acceptedAt : occurrence?.UpdatedAt ?? acceptedAt, ETag = occurrence?.ETag
            };
            var operation = new RoutineOperationDocument
            {
                Id = operationDocumentId, OperationId = request.OperationId, RoutineId = definition.Id,
                ExpectedBusinessDate = request.ExpectedBusinessDate, ExpectedVersion = request.ExpectedVersion,
                TargetState = request.State, Result = resultItem, AcceptedAt = acceptedAt
            };

            XpTotalDocument? total = null;
            XpEventDocument? xpEvent = null;
            List<(BusinessPeriod Period, XpProgressDocument? Document)> progress = [];
            if (xpAmount != 0)
            {
                total = await ReadOrDefaultAsync<XpTotalDocument>("xp-total", cancellationToken) ?? new XpTotalDocument();
                var eventEffort = xpAmount > 0 ? definition.Effort : occurrence!.ActiveAwardEffort!;
                xpEvent = new XpEventDocument
                {
                    Id = "xp-event:" + Guid.NewGuid().ToString("N"), TaskId = definition.Id, TaskName = definition.Name,
                    SubjectType = "routine", ChangeType = xpAmount > 0 ? "award" : "revoke", XpAmount = xpAmount,
                    ObservedEffort = definition.Effort, Effort = eventEffort, Source = "routine-api", OccurredAt = acceptedAt, RecordedAt = acceptedAt
                };
                foreach (var period in _periods.ForEvent(acceptedAt)) progress.Add((period, await ReadOrDefaultAsync<XpProgressDocument>(period.Id, cancellationToken)));
            }

            var batch = _container.CreateTransactionalBatch(new PartitionKey(TaskXpRepository.ProfileId)).CreateItem(operation);
            if (occurrence is null) batch.CreateItem(nextOccurrence);
            else batch.ReplaceItem(nextOccurrence.Id, nextOccurrence, new TransactionalBatchItemRequestOptions { IfMatchEtag = occurrence.ETag });
            if (stateChanged)
            {
                batch.CreateItem(new RoutineTransitionDocument
                {
                    Id = $"routine-transition:{businessDateText}:{definition.Id}:{nextVersion}", OperationId = request.OperationId,
                    RoutineId = definition.Id, BusinessDate = businessDateText, RoutineName = definition.Name, Effort = definition.Effort,
                    ConfiguredXp = configuredXp, XpAmount = xpAmount, PreviousState = currentState, TargetState = request.State, AcceptedAt = acceptedAt
                });
            }
            if (xpEvent is not null && total is not null)
            {
                var nextTotal = new XpTotalDocument { TotalXp = total.TotalXp + xpAmount, ETag = total.ETag };
                if (total.ETag is null) batch.CreateItem(nextTotal); else batch.ReplaceItem(nextTotal.Id, nextTotal, new TransactionalBatchItemRequestOptions { IfMatchEtag = total.ETag });
                batch.CreateItem(xpEvent);
                foreach (var item in progress)
                {
                    var existing = item.Document;
                    var next = new XpProgressDocument
                    {
                        Id = item.Period.Id, Period = item.Period.Period, PeriodStart = item.Period.Start.ToString("yyyy-MM-dd"),
                        PeriodEndExclusive = item.Period.EndExclusive.ToString("yyyy-MM-dd"), SignedXp = (existing?.SignedXp ?? 0) + xpAmount,
                        TargetXp = existing?.TargetXp ?? _targets.TargetFor(item.Period), ETag = existing?.ETag
                    };
                    if (existing is null) batch.CreateItem(next); else batch.ReplaceItem(next.Id, next, new TransactionalBatchItemRequestOptions { IfMatchEtag = existing.ETag });
                }
            }

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Recorded routine transition. RoutineId={RoutineId}, BusinessDate={BusinessDate}, State={State}, Version={Version}, XpAmount={XpAmount}, RequestCharge={RequestCharge}", definition.Id, businessDateText, request.State, nextVersion, xpAmount, response.RequestCharge);
                return new RoutineTaskMutationResult(resultItem, false);
            }
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
            {
                _logger.LogWarning("Retrying conflicted routine batch. RoutineId={RoutineId}, BusinessDate={BusinessDate}, Attempt={Attempt}", definition.Id, businessDateText, attempt + 1);
                continue;
            }
            throw new CosmosException("Routine persistence failed.", response.StatusCode, 0, response.ActivityId, response.RequestCharge);
        }
        throw new CosmosException("Routine persistence conflicted repeatedly.", HttpStatusCode.Conflict, 0, null, 0);
    }

    private static bool Matches(RoutineOperationDocument operation, string routineId, RoutineTaskMutationRequest request) =>
        string.Equals(operation.RoutineId, routineId, StringComparison.Ordinal) &&
        string.Equals(operation.ExpectedBusinessDate, request.ExpectedBusinessDate, StringComparison.Ordinal) &&
        operation.ExpectedVersion == request.ExpectedVersion && string.Equals(operation.TargetState, request.State, StringComparison.Ordinal);

    private static RoutineTaskItem Item(RoutineTaskDefinition definition, int xp, string state, int version) => new(definition.Id, definition.Name, definition.Effort, xp, state, version);
    private static string OccurrenceId(DateOnly date, string routineId) => $"routine-occurrence:{date:yyyy-MM-dd}:{routineId}";
    private static string OperationId(string operationId) => "routine-operation:" + operationId;
    private static RoutineTaskConflictException Conflict(string businessDate, RoutineTaskItem? occurrence, string message) => new(message, new RoutineTaskConflictResult(businessDate, occurrence, message));
    private async Task<T?> ReadOrDefaultAsync<T>(string id, CancellationToken cancellationToken)
    {
        try { return (await _container.ReadItemAsync<T>(id, new PartitionKey(TaskXpRepository.ProfileId), cancellationToken: cancellationToken)).Resource; }
        catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound) { return default; }
    }
}
