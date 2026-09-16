# Feature: Task XP

## Status

Implemented locally; infrastructure and end-to-end verification pending

## Goal

Make completed tasks visible as meaningful progress by awarding XP in one shared personal progression system.

## Problem statement

Completing tasks is currently recorded in task-management tools, but the user has no persistent, auditable view of the effort invested or the progress earned from those completions.

## Initial idea

Award XP for completed tasks. The XP amount should be derived from a task effort level instead of being entered manually for every task. The system must retain a history of the events that later support XP calculation and review.

## Business requirements

- The first functional scope is completed tasks only.
- Each completed task can contribute XP to a shared personal progression system.
- A task has an effort level. Effort represents more than duration: it can include difficulty, required focus, and resistance to starting the task.
- The user should not enter a raw XP amount for each task.
- The initial effort-to-XP mapping is: `Trivial` = 5 XP, `Easy` = 10 XP, `Medium` = 25 XP, `Hard` = 50 XP, and `Epic` = 100 XP.
- When a task has no effort value, `Medium` must be applied when XP is calculated.
- The system must retain an audit record for every received activity event relevant to future XP processing.
- An audit record must preserve enough context to establish what happened, when it happened, which task it concerned, and where the event came from.
- Future XP calculation must be based on recorded history, rather than on an unaudited transient action.
- The first release must provide an endpoint that returns event history.
- The first release must provide an endpoint that returns the user's current total XP.
- The event-history endpoint must return only logical events that affect XP, not duplicate or unsuccessful source deliveries.
- Each event-history entry must include the task name, XP change type, signed XP amount, effort, event time, and source.
- Event history must use the actual task completion time when the source provides it. The system recording time remains internal audit information.
- The feature must support task completions originating from both the Android application and direct task updates in Notion.
- Duplicate reports of the same completion must not result in duplicate business effects when XP processing is introduced.

## User scenarios

- A user completes a task through the Android application. The completion is recorded for later XP processing.
- A user marks a task as completed directly in Notion. The completion is recorded for later XP processing.
- The same completion is observed through more than one source. It remains one logical completion.
- A task has no selected effort. The future XP calculation uses the configured default effort while retaining the originally observed value in the audit history.
- A user can retrieve the recorded event history.
- A user can retrieve the current total XP.

## Business rules

- A task is a single, logically completable action. A project is a container of tasks and is not part of the first scope.
- A task is business-complete only when its status is `Zrobione`.
- XP is awarded for completing tasks, not for completing projects in the first scope.
- XP reflects the current completion state of a task. Reopening a completed task revokes its previously awarded XP.
- When a reopened task reaches `Zrobione` again, it receives XP again.
- Changing a task's effort after completion does not change the XP already awarded for that completion.
- After a reopen, a new completion uses the effort value applicable to that new completion.
- Users cannot manually change XP in the first version.
- The effort-to-XP mapping is a business rule and must be changeable without requiring manual XP entry per task.
- A change to the effort-to-XP mapping applies only to future task completions and does not recalculate historical XP.
- Historical activity must be auditable: it must be possible to determine the task, source, time, and reason behind a future XP decision.
- The audit history is append-only from a business perspective; corrections must be represented by later events rather than silently replacing history.
- Internal audit records can include duplicate or unsuccessful deliveries, but they are not exposed through the user-facing event-history endpoint.

## Edge cases

- The same task completion is reported multiple times.
- A completion is first reported through the Android application and later observed in Notion.
- A task is completed directly in Notion without using the application.
- The effort value is missing or empty at completion time.
- Effort changes before or after a completion is observed.
- Events are received late or out of order.

## Out of scope

- XP for project completion.
- XP for routine or habit completion.
- Achievement, medal, streak, and dashboard functionality.
- Manual user-driven XP adjustments.
- Exposing raw source-delivery audit records to users.

## Technical design

In progress. The existing Function App has no persistent application data store or Notion change listener. Android task status changes currently go through the anonymous `PATCH /api/today-tasks/.../{id}` endpoint; direct edits in Notion bypass the Function App.

The implementation will add a focused `TaskXp` feature area to the Function App. It will own the effort mapping, Notion task-snapshot interpretation, transactional Cosmos DB persistence, and read models. The existing `TodayTasks` area remains responsible for updating a task in Notion and will pass its confirmed page snapshot to `TaskXp` after a successful status update.

## Technical decisions

- Persist Task XP data in Azure Cosmos DB instead of Azure Table Storage.
- Use Azure Cosmos DB for NoSQL with provisioned throughput and enable the lifetime Free Tier during account creation. Create one shared-throughput database with a maximum of 1,000 RU/s for the initial containers; do not use serverless capacity.
- Ingest direct Notion changes through a signed Notion `page.properties_updated` webhook. Record Android-driven status changes in the Function App immediately after Notion confirms the update.
- Authenticate the deployed Function App to Cosmos DB with its system-assigned managed identity and a least-privilege Cosmos DB data-plane role. Local development uses a Cosmos DB emulator or a local connection string.
- Expose Task XP read endpoints anonymously behind new stable, unguessable route segments, consistent with the existing mobile API. Authentication improvement is out of scope for Task XP.
- Read the Notion task property named `Effort` as a `select` value. The supported values are exactly `Trivial`, `Easy`, `Medium`, `Hard`, and `Epic`; a missing or empty value maps to `Medium` for XP while the missing observed value remains in the audit data.
- Keep the effort-to-XP mapping in Function App configuration (`TaskXp:Xp:*`), validate it at startup, and snapshot the applied effort and XP amount in every XP event. A configuration change therefore affects only future completions.
- Use a fixed internal `personal` progression profile identifier. The existing application has no user identity and the business scope defines one shared personal progression system.

## Alternatives considered

- Azure Table Storage was rejected at the user's preference in favor of Cosmos DB.
- Cosmos DB serverless was rejected because Cosmos DB Free Tier does not apply to serverless accounts.
- Adding user authentication was rejected for this feature because it is explicitly out of scope; the existing anonymous, unguessable-route convention remains.
- Recomputing total XP from the Notion task's current effort was rejected. Every logical XP event snapshots the applied effort and amount so later task edits and mapping changes cannot rewrite history.

## Architecture and data flow

The proposed persistence account is Azure Cosmos DB for NoSQL. Its lifetime Free Tier applies to provisioned throughput, not serverless accounts. The first 1,000 RU/s and 25 GB are included for one opted-in account in an Azure subscription. A shared-throughput database lets the feature's initial containers share that free 1,000 RU/s budget.

The Function App will use the Cosmos DB .NET SDK with `DefaultAzureCredential` in Azure. The Function App's system-assigned identity must receive the `Cosmos DB Built-in Data Contributor` data-plane role scoped to the Task XP database or account. The application does not use Cosmos DB account keys in Azure.

One `task-xp` container, partitioned by the fixed personal progression profile identifier, will hold three document types in the same logical partition: received source deliveries, one completion-state projection per task, and append-only logical XP events. A transactional batch will atomically create the source-delivery audit document, update the task-state and total-XP projections, and create an XP event when a state transition affects XP. The shared partition is deliberate: Cosmos DB transactions are atomic only within one logical partition.

The Notion webhook handler will verify the `X-Notion-Signature` HMAC against the stored verification token, retain its unique Notion event ID for delivery idempotency, then retrieve the current task page from Notion before processing it. It will ignore non-task pages, changes that do not concern the `Status` property, and stale deliveries for business effects, while retaining their delivery audit record. Android-driven status updates will use the confirmed page response from Notion as the same canonical task snapshot. This makes a direct Android update and its later Notion webhook one logical state change.

`TaskXp` will use the following Cosmos DB documents, all with `profileId: "personal"` as the partition key:

- `delivery-receipt`: internal immutable audit of a received Android operation or Notion webhook delivery, including source event ID, attempt number, source time, received time, task page ID where known, and sanitized payload metadata. It is never returned by the public API.
- `task-state`: one document per Notion task page, containing the latest processed completion state, latest accepted source time, current completion cycle, and the award that must be revoked if the task reopens.
- `xp-event`: immutable, user-visible logical event. It contains task ID and name, `award` or `revoke` change type, signed XP amount, observed effort, applied effort, source, actual source-event time, and recording time.
- `xp-total`: one projection document containing the current signed total.

For a task transition into `Zrobione`, the service resolves effort, creates an `award` event with a positive amount, increments the completion cycle, and increments `xp-total`. For a transition out of `Zrobione`, it creates a `revoke` event with the negative amount recorded for the active award and decrements `xp-total`. Effort edits while the state remains completed produce no XP event. Each of these transitions is committed with the source-event idempotency marker, task-state replacement, and total replacement in one transactional batch. A conflicting concurrent write rereads the projection and retries; a known source event or unchanged completion state has no second business effect.

The public HTTP API will be:

- `GET /api/task-xp/<unguessable-segment>/events?limit=<1..100>&continuationToken=<opaque>` returns `{ events, continuationToken }`, newest actual event time first. Each entry includes `taskName`, `changeType`, signed `xpAmount`, `effort` (the applied effort), `occurredAt`, and `source`.
- `GET /api/task-xp/<unguessable-segment>/total` returns `{ totalXp }`.

Both endpoints set `Cache-Control: no-store`, return no delivery-audit records, and use opaque Cosmos continuation tokens without exposing internal document fields.

## Security and operational considerations

- The Notion webhook endpoint must validate every event using HMAC-SHA256 over the raw request body and constant-time comparison with the verification token. The initial Notion verification request is handled separately and only returns success for its verification handshake.
- Store the Notion webhook verification token in Function App configuration, never in source control or the Android application.
- Cosmos DB Free Tier is only available to one opted-in account per Azure subscription and must be enabled when that account is created. Keep the account in one region, provision no more than 1,000 RU/s in total, and retain less than 25 GB to avoid charges.
- The XP history and total endpoints remain anonymous by confirmed scope. Their complete URLs are public application configuration, so this is obscurity rather than authentication and must not be described as an access-control mechanism.
- Add `Microsoft.Azure.Cosmos`, `Azure.Identity`, and configuration binding support as explicit Function App dependencies. Create the `CosmosClient` as a singleton; do not create a client per request.
- Production configuration contains only the Cosmos endpoint, database/container identifiers, effort mapping, anonymous route segments, and the Notion webhook verification token. The Function App identity receives `Cosmos DB Built-in Data Contributor`; no Cosmos account key is deployed.
- Use the Cosmos DB emulator and a local-only connection string for local runs. Production deployment must not overwrite the managed-identity configuration with a connection string.
- Log source type, source-event ID, task ID, event type, processing outcome, and Cosmos request charge where available. Never log a webhook signature, verification token, Notion token, Cosmos connection string, or raw body.
- Native Notion webhook delivery is asynchronous, may aggregate changes for a page, and may arrive out of order. The handler uses the event timestamp to reject stale business effects and retrieves the latest page state, but it cannot reconstruct an intermediate completed-and-reopened cycle that occurs entirely before any webhook is processed.
- This transient direct-Notion completion → reopen → completion limitation is accepted for V1. Do not add Notion automation or polling solely to address it.

## Implementation plan

1. Provision Azure Cosmos DB for NoSQL manually in the existing resource group: select provisioned throughput, opt into Free Tier at account creation, use one region, create database `task-xp` with shared throughput at 1,000 RU/s, and create container `progression` partitioned by `/profileId`. Enable the Function App system-assigned identity and assign it the Cosmos DB Built-in Data Contributor data-plane role at the database scope.
2. Add the Cosmos DB endpoint, database/container names, effort mapping, anonymous read-route segment, and Notion webhook verification token to local configuration templates, deployment configuration, and the Function App deployment workflow. Document the required manual Notion webhook subscription for `page.properties_updated` and its verification handshake.
3. Add `TaskXp` domain records, configuration validation, `CosmosClient` registration through `DefaultAzureCredential`, and local emulator connection-string registration. Implement a Cosmos repository that creates delivery receipts and executes the state/event/total transactional batch with ETag conflict retries.
4. Extend the Notion task client to return a canonical task snapshot containing page ID, name, `Status`, `Effort`, data-source ownership, and `last_edited_time`. Reuse that snapshot after Android status updates; do not issue a second Notion read for that path.
5. Integrate `TaskXp` into the existing Android-facing status update flow. On a successful Notion update, submit the canonical snapshot with source `android`; surface an error if durable XP recording fails so a retry can reconcile the confirmed task state without duplicate XP.
6. Add the dedicated Notion webhook HTTP function. Handle the initial verification request, verify signed events using the raw body and constant-time comparison, persist delivery receipts, filter to `page.properties_updated` events for the configured task data source and `Status` property, retrieve the canonical page snapshot, and submit it with source `notion`.
7. Add anonymous total-XP and paged event-history functions. Map only logical `xp-event` documents to the documented response contract and translate configuration, Notion, Cosmos, validation, and cancellation failures into safe HTTP responses with no secret leakage.
8. Update repository and deployment documentation with Cosmos setup, managed-identity role assignment, local emulator setup, effort-property schema, webhook subscription/verification, endpoint contracts, Free Tier guardrails, and operational troubleshooting.
9. Build the Function App, inspect the complete diff, and manually verify both source paths, deduplication, reopen/recomplete, empty effort, effort changes, mapping changes, pagination, invalid webhook signatures, and transient Cosmos/Notion failures. Update this feature document with results and deviations before implementation is declared complete.

## Verification plan

- Build: `dotnet build NotionManagement.sln`.
- Local infrastructure: start Azurite as already required by the Function App and the Cosmos DB emulator for Task XP; confirm the Function App connects through the local credential configuration.
- Android source: change a task from a non-complete status to `Zrobione` through the existing API; verify one positive event and updated total. Retry the same action and verify no duplicate XP.
- Notion source: change `Status` directly in Notion and verify a signed webhook produces one equivalent event. Verify the later webhook for an Android-originated change has no duplicate business effect.
- Lifecycle: reopen a completed task and verify a negative event using the original applied amount; complete it again after changing effort and verify a new positive event with the new applied mapping.
- Defaults and history: complete a task with no `Effort`, verify `Medium` and 25 XP are applied while audit data retains the missing observed value; change mapping configuration and verify earlier event amounts remain unchanged.
- API: verify total, newest-first history ordering, paging, required event fields, absence of raw delivery records, no-store headers, invalid continuation-token behavior, and safe error responses.
- Security and resilience: verify rejected invalid signatures do not create XP events; simulate duplicate, stale, and failed Notion deliveries; verify retry records no duplicate business effect. Confirm logs contain no secrets.

## Implementation notes

Implemented the `TaskXp` Cosmos DB feature area, options validation, singleton Cosmos client, transactional state/event/total persistence with receipt idempotency and ETag retry, anonymous total/history endpoints, and the signed Notion webhook endpoint. Android status updates now record the confirmed Notion page snapshot immediately after a successful update. Added local/deployment configuration templates and deployment secret wiring.

Task XP logs the safe operational context needed to diagnose Cosmos writes and reads: source, source-event ID, task ID, status, progression outcome, Cosmos status code, activity ID, and request charge. It does not log webhook signatures, verification tokens, Notion tokens, or connection strings.

The Cosmos account, managed-identity role assignment, Notion webhook subscription, and production configuration values remain manual operational steps and were not performed from this repository.

## Validation performed

`dotnet build NotionManagement.sln` completed successfully with no warnings or errors on 2026-09-16.

Manual Cosmos emulator, Notion API, and webhook verification have not been run because their credentials and infrastructure are not available in this workspace.


## Review findings

Not started.

## Manual acceptance checklist

Not started.

## Open questions

None.

## Decisions log

| Date | Decision | Reason |
|---|---|---|
| 2026-09-16 | Start Task XP with completed tasks only. | Keep the first scope focused before introducing project and routine progression. |
| 2026-09-16 | Record activity events before introducing XP calculation. | Preserve auditable facts that can support later XP processing. |
| 2026-09-16 | Task effort determines XP rather than per-task manual XP entry. | Reduce manual work and keep rewards consistent. |
| 2026-09-16 | Use `Medium` when a task effort is missing or empty. | Provide a predictable fallback without blocking XP calculation. |
| 2026-09-16 | Treat status `Zrobione` as the business completion of a task. | Define one unambiguous point at which a task qualifies for XP. |
| 2026-09-16 | Reopening a task revokes XP; completing it again awards XP again. | XP must represent the task's current completion state. |
| 2026-09-16 | Use the initial mapping Trivial/Easy/Medium/Hard/Epic = 5/10/25/50/100 XP. | Establish a simple and visible reward scale for the first version. |
| 2026-09-16 | Do not recalculate historical XP when effort changes after completion. | Keep completed-task history and progress stable and explainable. |
| 2026-09-16 | Do not allow manual user-driven XP changes in V1. | Keep XP changes tied to defined task-state rules. |
| 2026-09-16 | Provide event-history and total-XP endpoints in the first release. | Make the recorded activity and current progress available without introducing a dashboard. |
| 2026-09-16 | Expose only XP-affecting logical events in event history. | Keep the user-facing history meaningful while retaining complete internal audit records. |
| 2026-09-16 | Include task name, XP change type and amount, effort, time, and source in event history. | Provide enough context for a user to understand every visible XP change. |
| 2026-09-16 | Show the actual completion time in history when available. | Represent when the user completed the task rather than when the system observed it. |
| 2026-09-16 | Apply mapping changes only to future completions. | Preserve the stability and auditability of historical XP. |
| 2026-09-16 | Use Azure Cosmos DB rather than Azure Table Storage for Task XP persistence. | User preference. |
| 2026-09-16 | Use Cosmos DB for NoSQL provisioned throughput with the lifetime Free Tier, rather than serverless. | Free Tier does not apply to serverless; the initial workload should fit within 1,000 RU/s and 25 GB. |
| 2026-09-16 | Receive direct Notion task changes through a signed `page.properties_updated` webhook and record Android updates after Notion confirms them. | Covers both required event sources and uses Notion as the canonical task state. |
| 2026-09-16 | Use the Function App's system-assigned managed identity for Cosmos DB data access. | Avoids a production Cosmos DB account key or connection string. |
| 2026-09-16 | Keep Task XP read endpoints anonymous behind unguessable routes. | Match the current MVP; authentication improvement is outside this feature's scope. |
| 2026-09-16 | Read task effort from the Notion `Effort` select property with the five configured options. | User created the property with the required schema. |
| 2026-09-16 | Store the XP mapping in application configuration and snapshot its applied values in logical events. | Mapping changes must affect only future completions. |
| 2026-09-16 | Use one fixed `personal` Cosmos DB profile partition. | The feature is explicitly one shared personal progression system and has no user identity. |
| 2026-09-16 | Accept the native Notion webhook limitation for a rapid direct-Notion completion → reopen → completion sequence. | Native aggregated webhooks expose the final current state, not the transient prior states; extra automation or polling is disproportionate for V1. |
| 2026-09-16 | Complete and accept technical design and implementation planning. | The architecture, contracts, persistence, security model, limitations, verification, and implementation sequence are ready for a separate implementation stage. |
