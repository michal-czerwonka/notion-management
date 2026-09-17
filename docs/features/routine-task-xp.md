# Feature: Routine Task XP

## Status

Feature review complete; no unresolved findings; awaiting user acceptance

## Goal

Include completed routine tasks in the existing personal XP progression and its visualisation.

## Problem statement

The XP system currently rewards completed regular tasks, while routine tasks completed in the application's daily routine view do not contribute to XP. This makes the recorded daily progress incomplete.

## Initial idea

Assign an effort value to every routine task in repository configuration, analogously to the `Effort` property used by regular Notion tasks. Completing a routine task should contribute XP according to the existing Task XP rules. Repeated completion-state changes within the same day must not cause the routine to contribute more than once for that day, and the awarded XP must be included in the same daily progress as XP from regular tasks completed that day.

## Business requirements

- Every configured routine task must have an assigned effort value.
- Routine-task effort must be stored with the routine task in repository configuration.
- The same stable routine-task ID may use different configured effort values on different weekdays.
- Routine-task effort must represent the same concept as effort for regular Notion tasks.
- Routine tasks must use the existing effort levels and XP mapping: `Trivial` = 5 XP, `Easy` = 10 XP, `Medium` = 25 XP, `Hard` = 50 XP, and `Epic` = 100 XP.
- Every routine-task configuration entry must contain one supported effort value; routine tasks do not use an effort fallback.
- The application build must fail when a routine task has a missing, empty, or unsupported effort value.
- Completing a routine task must contribute XP to the existing shared personal progression system.
- A routine task must contribute XP at most once for a given business day, even if it is completed, reopened, and completed again during that day.
- Reopening a completed routine task during the same business day must revoke the XP awarded for that day's completion.
- Completing that reopened routine again during the same business day must restore its daily XP award without creating an additional net award.
- Every routine XP award must retain the effort level and XP amount applied when that award was created.
- If configured effort changes after an award, reopening must revoke the original awarded amount; a later recompletion must use the effort and mapping current at the time of recompletion.
- Every durable routine transition must snapshot the routine name and relevant configured values as observed when the transition occurred.
- Historical routine records must not be rewritten or resolved through the current routine configuration.
- Routine-task XP must contribute to the same XP visualisation and daily progress as XP from regular tasks completed during that business day.
- Routine-task XP awards and revocations must contribute to the existing all-time total and day, week, month, and year progress aggregates.
- Routine completion awards and reopening revocations must appear in the existing user-facing XP event history analogously to regular-task XP events, with their routine source identifiable.
- Zero-XP routine transitions, including skip events, must remain in the internal routine audit history and must not appear as XP-affecting events.
- A recurring routine task may contribute XP again on each business day on which it is available and completed.
- Marking a routine task as skipped must contribute 0 XP.
- The system must retain a durable, day-specific record that a routine task was skipped.
- A skip record must remain available after the applicable business day has ended and the routine view has reset for a new day.
- Durable routine history must preserve every state transition rather than only the routine's final state for a business day.
- Durable routine history must be stored in the database.
- Durable routine history must be retained indefinitely; the first scope has no automatic expiration or deletion policy.
- The first scope does not require the user to browse routine history.
- The database must be the source of truth for the current state of each routine occurrence on a business day.
- The Android application is the only routine client in the first scope, but persisted state must support a future web client without creating divergent routine state or duplicate XP.
- Routine XP and durable history start when this feature is deployed; earlier device-local routine states must not be imported or awarded retroactively.
- A routine state change must become effective in the Android UI only after the backend confirms the persisted change.
- If the backend request fails, the application must retain the previously confirmed state and allow the user to retry.
- The backend acceptance time is the authoritative occurrence time for a routine transition and determines its business day and XP periods.
- Retrying the same routine state-change operation must be idempotent.

## User scenarios

- The user completes a routine task and sees its XP included in the current business day's XP progress.
- The user completes a routine task, reopens it, and completes it again during the same business day; the routine does not contribute more than one award for that day.
- The same recurring routine is completed on another business day and contributes XP for that new day.
- The user skips a routine task; no XP is awarded, and the system retains a record that this routine was skipped on that business day.

## Business rules

- Routine-task XP uses the routine's configured effort rather than a manually configured raw XP value.
- The effort applied to a completion is taken from the routine entry for the applicable business day's weekday; effort consistency across weekday occurrences of the same stable ID is not required.
- Routine-task XP uses the same effort vocabulary and effort-to-XP mapping as regular-task XP.
- Missing, empty, and unsupported routine effort values are invalid configuration rather than values that resolve to `Medium`.
- The award is scoped to the combination of a stable routine-task identity and a business day.
- Repeated completion of the same routine within one business day must not create additional earned XP beyond one daily award.
- Routine completion-state transitions follow the same award and revoke semantics as regular tasks: completion awards XP, reopening revokes that award, and recompletion awards it again.
- Configuration changes must not retroactively recalculate an existing routine XP award.
- Routine-task XP and regular-task XP belong to one shared XP total and progress model.
- Routine XP uses the same period attribution and aggregation rules as regular-task XP for day, week, month, year, and all-time totals.
- The existing XP event history contains only routine transitions that change XP; routine audit history retains all transitions.
- Business-day attribution follows the existing routine-task and XP-progress boundary of 03:00 in the Europe/Warsaw time zone.
- Device time and click time do not determine routine event attribution; the backend acceptance timestamp does.
- A skipped state is not an XP award and must not increase the all-time or period XP totals.
- A skip record is associated with the stable routine-task identity and the applicable business day.
- Routine history must not depend solely on the current-day local state in the application.
- Historical routine transition records are append-only and are not automatically deleted due to age.
- Later routine renames, effort changes, or configuration removal do not alter historical records.
- All clients must resolve the current routine state from the shared persisted daily state rather than treating device-local state as authoritative.
- A routine occurrence identified by stable routine ID and business day must have one shared current state and at most one active XP award, regardless of the client that changes it.
- Existing local routine completion and skip state from before the feature launch is not an authoritative historical source and does not create database history or XP.
- The current state, transition-history entry, and any XP award or revocation caused by one routine action must not be presented as successfully changed unless the backend accepts the operation.
- A retried operation that was already accepted must return its confirmed result without creating another logical transition, audit entry, award, or revocation.
- Routine history must preserve the ordered sequence of completion, reopening, recompletion, and skip transitions within a business day.

## Edge cases

- A routine is completed, reopened, and completed again within one business day.
- A routine remains or becomes incomplete after having been completed during the business day.
- A routine is skipped and later moved to another state during the same business day; the earlier skip remains in history.
- The same stable routine-task ID appears in multiple weekday configuration lists.
- The same stable routine-task ID has different effort values in different weekday lists; each occurrence uses its own configured value.
- A routine is completed shortly before or after the 03:00 Europe/Warsaw business-day boundary.
- A configured routine task has a missing, empty, or unsupported effort value; build validation rejects the configuration.
- The same routine occurrence is updated from more than one client; persisted state prevents divergence and duplicate XP effects.
- A state-change request fails or times out; the Android application keeps the last confirmed state and lets the user retry.
- The backend commits a change but its response is lost; retry returns the committed result without duplicating history or XP.
- The feature is deployed while an older application installation contains local routine state; that state is not migrated or rewarded retroactively.

## Out of scope

- Creating a separate effort vocabulary or effort-to-XP mapping for routine tasks.
- Introducing a separate XP total or progress visualisation for routine tasks.
- Awarding XP for a skipped routine task.
- Changing the routine-task schedule, weekday configuration model, or 03:00 Europe/Warsaw business-day boundary.
- A user-facing routine-history screen or history API.
- Implementing a web client for routine tasks in the first scope.
- Backfilling XP or durable routine history for routine actions performed before this feature is deployed.

## Technical design

The current routine implementation is entirely client-side: the React application reads
`NotionManagementApp/src/config/routine-tasks.json`, derives the Europe/Warsaw business
date locally, and stores the current day's statuses in `localStorage`. The Function App
does not currently expose routine endpoints or know the routine configuration.

The existing Task XP implementation already provides the required shared progression
building blocks: a single Cosmos DB container partitioned by `profileId = "personal"`,
transactional updates of the all-time total and day/week/month/year projections, immutable
XP events, the Europe/Warsaw 03:00 business-day calculator, and anonymous read endpoints
used by the Android application.

The recommended direction is to add a focused `RoutineTasks` backend feature area while
reusing the existing Cosmos container, personal partition, business-period calculator,
effort mapping, XP event stream, totals, and progress projections. Routine occurrence
state, idempotency records, and append-only routine transition history would be new
document types. A state-changing request would update all affected documents in one
Cosmos transactional batch.

The backend must resolve routine name, weekday entry, configured effort, and XP amount
from repository configuration. These values must not be accepted as authoritative client
input. The existing routine configuration moves to the neutral repository-level
`config/routine-tasks.json` path and remains the single source of truth. The Android build
validates that file, and the Function App includes the same file in its publish artifact
and validates it when loading it.

The Function App returns the complete routine list for its authoritative business day,
including each routine's configured values and confirmed persisted state. Android no
longer treats its bundled schedule or locally calculated business date as authoritative.
This prevents client/server configuration drift and makes the backend-selected business
date explicit in the client contract.

## Technical decisions

- The Function App owns routine schedule resolution for the current backend-calculated
  business day and returns the complete routine list joined with confirmed persisted state.
- Android renders the server response rather than independently resolving its bundled
  schedule and local routine state.
- Routine occurrence updates use optimistic concurrency. A client sends the version of
  the confirmed occurrence state on which its action is based. The backend rejects a new
  operation based on an older version instead of overwriting a newer accepted state.
- Routine read and mutation endpoints remain anonymous behind an unguessable route segment
  in the first scope, consistent with the existing mobile APIs. This is an accepted scope
  limitation and is not treated as authentication.
- Routine endpoints use a dedicated `RoutineTasks:RouteSegment`, separate from the
  existing `TaskXp:ReadRouteSegment`, so mutation access can be configured and rotated
  independently from XP read access.
- Routine configuration is owned at repository level in `config/routine-tasks.json`, not
  by either client or backend project. No generated or manually synchronized second copy
  is introduced.
- Shared XP events gain an additive `subjectType` discriminator with `task` and `routine`
  values. The existing `source` field continues to identify the input channel. Historical
  XP documents without `subjectType` are interpreted as `task`.

The state and HTTP contract is confirmed as follows.

The routine state vocabulary is `pending`, `completed`, and `skipped`. A transition from
`completed` to either non-completed state revokes the active award. A transition from a
non-completed state to `completed` creates an award using the current configured effort.
Transitions between `pending` and `skipped` affect routine audit history but not XP.

The HTTP contract is:

- `GET /api/routine-tasks/{segment}` returns the backend business date and its complete
  configured routine list. Each item contains `id`, `name`, `effort`, `xp`, `state`, and
  `version`. A routine without persisted state is returned as `pending` with version `0`.
- `PUT /api/routine-tasks/{segment}/{routineId}` accepts `operationId`,
  `expectedBusinessDate`, `expectedVersion`, and target `state`. The business date is a
  precondition echoed from the server response, not a client-selected event date.
- A successful mutation returns the current confirmed routine occurrence and whether the
  supplied operation was replayed. A retry of an accepted operation never reapplies its
  transition or XP effect.
- `409 Conflict` is returned when the expected business date or occurrence version is
  stale. The response includes the backend's current business date and, when applicable,
  current occurrence so the row can be refreshed.
- `400 Bad Request` covers malformed operation IDs and unsupported states; `404 Not Found`
  covers a routine not configured for the backend's current business date; transient
  persistence failures return a retryable `503 Service Unavailable` response.
- All responses use `Cache-Control: no-store`.

## Alternatives considered

- Keeping the schedule and day calculation authoritative in Android was rejected because
  a future second client and independently deployed APK versions could disagree with the
  backend configuration used to calculate XP.
- Duplicating routine configuration in frontend and backend projects was rejected because
  synchronized copies can drift. A single neutral repository file is used instead.
- Last-write-wins occurrence updates were rejected because a stale client could silently
  undo a newer transition and its XP effect.
- Encoding routine identity into `source` was rejected because it conflates the kind of
  rewarded subject with the channel through which the event arrived.
- A separate Cosmos container or partition was rejected because XP total and period
  projections must be updated atomically with routine state and history; Cosmos
  transactions cannot span logical partitions.
- Importing the old `localStorage` state was rejected by business scope because it is not a
  complete, shared, auditable source.

## Architecture and data flow

Data flow:

1. Android loads the current routine view from the Function App. The backend calculates
   the current business date from its acceptance time and the existing 03:00
   Europe/Warsaw boundary.
2. The backend resolves the applicable weekday configuration and joins it with one
   persisted occurrence-state document per stable routine ID and business date.
3. Android sends a target state, the version of the last confirmed occurrence state, and
   a client-generated operation ID. The operation ID and expected version are retained for
   retry if the response is lost.
4. The backend recalculates the business date at request acceptance, validates that the
   routine exists for that date, and derives its name, effort, and XP from configuration.
5. In one Cosmos transactional batch, the backend creates the idempotency record and
   immutable routine-transition snapshot, updates the occurrence state, and, when XP
   changes, creates the shared XP event and updates the all-time and four period
   projections.
6. The confirmed occurrence state and its new version are returned to Android. Android
   changes the displayed state only after this response; on failure it retains the
   previously confirmed state and offers retry with the same operation ID.
7. If the expected version is stale, the backend does not create a transition or XP
   effect. It returns a conflict response containing the current confirmed occurrence so
   Android can refresh the affected row and let the user decide whether to act again.

The routine documents are:

- `routine-occurrence`: one current-state projection per routine ID and business date,
  including a monotonically increasing version and the active award snapshot needed for
  an exact revoke;
- `routine-transition`: an immutable snapshot of every accepted state transition,
  including operation ID, routine ID, business date, routine name, configured effort,
  relevant XP amount, previous state, target state, and backend acceptance time;
- `routine-operation`: an idempotency record keyed by the client operation ID and holding
  the confirmed response needed to replay a successful result.

The existing `xp-event` contract gains the confirmed `subjectType` discriminator. The
event-history response exposes it as an additive field. Existing XP documents without the
field are mapped to `task`; no Cosmos data migration is required.

The backend checks idempotency before concurrency. If `operationId` already exists with the
same routine, business date, expected version, and target state, the request is a replay
and no write is repeated. Reusing an operation ID with a different request fingerprint is
a conflict. A new operation whose expected version is stale is rejected without an audit
transition or XP event.

Every accepted state change increments the domain version. A new operation targeting the
already-current state creates no routine transition and no XP event; it only receives the
current confirmed result. Internal Cosmos ETags still protect the transactional batch
against races between the occurrence, total, and progress projection reads and writes.

The accepted transition timestamp is captured once by the Function App and used for its
business date, routine audit record, XP event, and progress periods. If the 03:00 boundary
passes after Android loaded the screen, `expectedBusinessDate` causes the old-day mutation
to be rejected rather than silently applied to the next occurrence.

## Security and operational considerations

- Routine read and mutation endpoints follow the repository's existing anonymous,
  unguessable-route convention. This is not authentication: the route is embedded in the
  APK and anyone who learns it could read or change routine state and XP. Introducing user
  authentication remains future work outside this feature.
- Client input is limited to routine identity, requested target state, operation ID, and
  concurrency preconditions. Names, effort values, XP amounts, authoritative business
  dates, and timestamps are server-derived.
- Keeping every routine transition indefinitely increases Cosmos storage over time. The
  current single-profile volume is expected to be small, but request charge and document
  growth should be logged and monitored.
- Reusing the existing `personal` logical partition preserves atomic XP updates and avoids
  a cross-partition consistency problem. It also retains the current single-user scaling
  assumption.
- No pre-feature `localStorage` state is uploaded. Once the server-backed flow is enabled,
  the old local value is ignored and may be removed locally without migration.

## Implementation plan

1. Move the routine schedule to `config/routine-tasks.json`, add required `effort` values,
   update the existing Node validator and frontend build path, and include the same file in
   the Function App build and publish outputs. Add backend loading validation for weekdays,
   object shape, IDs, names, effort values, duplicate weekday IDs, and cross-weekday name
   consistency.
2. Add `RoutineTasks` options for configuration path and the dedicated route segment.
   Register the configuration loader, service, and repository in the existing Function App
   dependency-injection setup. Reuse `BusinessPeriodCalculator` and the Task XP effort
   mapping rather than duplicating those rules.
3. Add Cosmos models for `routine-occurrence`, `routine-transition`, and
   `routine-operation`. Store all documents under `profileId = "personal"`; snapshot the
   configured routine data on every transition and the complete applied award on the
   occurrence while it remains completed.
4. Extend the shared XP event model and public history item with `subjectType`. Map a
   missing persisted value to `task` and set `routine` on new routine award and revoke
   events.
5. Implement the routine transaction workflow: resolve backend time and business date,
   validate the applicable configured entry, check operation replay/fingerprint, verify
   the expected version, derive the XP delta, read total and four period projections, and
   commit the operation, transition, occurrence, XP event, total, and projections in one
   transactional batch with bounded ETag-conflict retry.
6. Add the routine GET and PUT HTTP functions with validation, no-store responses, safe
   error bodies, conflict payloads, and operational logging that excludes secrets and raw
   request bodies.
7. Add a typed frontend routine API using `VITE_ROUTINE_TASKS_API_URL`. Replace
   `localStorage` schedule/state ownership in `RoutineTasksPage` with the server response,
   confirmed-only state updates, per-row pending controls, conflict refresh, and retry of a
   failed request with the same operation ID.
8. Remove use of pre-feature local routine state without uploading it. Keep the scheduled
   boundary refresh, but make it reload the server response rather than calculating and
   installing a new local state.
9. Add `RoutineTasks` configuration to local templates, deployment configuration, Function
   App settings, Android environment profiles, APK workflow generation, and project
   documentation. Generate a route segment distinct from the Task XP read segment.
10. Build both projects, inspect the complete diff, manually exercise the transaction and
    client flows, and record actual validation and deviations in this feature document.

## Verification plan

- Run `dotnet build NotionManagement.sln` and `npm run build` in
  `NotionManagementApp`.
- Verify valid configuration is consumed from the neutral path by both projects and that
  missing, empty, or unsupported effort fails the Android build and backend configuration
  load.
- Load routines before and after the 03:00 Europe/Warsaw boundary and verify the backend
  business date, weekday schedule, default `pending` state, and refresh behavior.
- Complete a routine and verify one transition, one `routine` XP award, the configured
  effort snapshot, the all-time total, and day/week/month/year aggregates.
- Reopen and recomplete within one business day; verify exact revocation of the active
  award, use of current effort on recompletion, and no more than one active net award.
- Exercise `pending` to `skipped`, `skipped` to `pending`, `skipped` to `completed`, and
  `completed` to `skipped`; verify every actual transition is retained and only completion
  boundary changes create XP events.
- Retry an accepted operation ID after a simulated lost response and verify no duplicate
  transition or XP effect. Reuse the ID with a different payload and verify conflict.
- Send two updates with the same expected version and verify only the first is accepted;
  confirm the second returns current state without audit or XP changes.
- Keep a screen open across the business-day boundary and verify an old-day mutation is
  rejected and followed by a reload of the new day.
- Change effort after an award, then reopen and recomplete; verify revoke uses the original
  snapshot and recompletion uses the new configured effort.
- Verify XP history identifies regular events as `task` and routine events as `routine`,
  including legacy documents without the new field.
- Simulate backend failure and verify Android preserves the last confirmed state, exposes
  retry, and does not apply an optimistic local state change.
- Confirm older `localStorage` data is neither uploaded nor converted into routine history
  or XP.

## Implementation notes

- Moved the schedule to repository-level `config/routine-tasks.json` and added the
  confirmed effort values. The frontend build validator and backend startup loader both
  validate the shared file; the Function App project links it into build and publish
  output.
- Added the `RoutineTasks` backend area with authoritative business-day schedule reads,
  versioned occurrence state, immutable transition history, durable operation replay,
  conflict responses, and atomic routine/XP Cosmos transactional batches.
- Extended XP events and history with the additive `subjectType` field. Existing events
  without the field continue to resolve as `task`; routine awards and revocations use
  `routine`.
- Replaced frontend schedule and `localStorage` state ownership with the typed routine API.
  State changes render only after server confirmation; failed writes retain their
  operation ID for retry, while conflicts refresh confirmed server state.
- Added the dedicated routine route and API URL to local examples, development deployment
  settings, GitHub Function App deployment, and Android APK generation.
- No production-scope deviations from the confirmed implementation plan were required.
- The post-review implementation pass prevents late mutation, conflict, and list responses
  from replacing a different business day or a newer occurrence version. Concurrent list
  loads are sequenced, and a stale list response preserves newer confirmed row versions.
- Updated the client documentation to describe the shared effort configuration, Cosmos-backed
  state, server-owned business date, retry behavior, and routine API deployment setting.
- Removed the extra EOF blank lines reported by the review whitespace check.

## Validation performed

- `dotnet build NotionManagement.sln` succeeded with zero warnings and zero errors.
- `npm run build` in `NotionManagementApp` succeeded, including shared routine
  configuration validation, TypeScript checking, and the Vite production build.
- `dotnet publish NotionManagementFunctionApp/NotionManagementFunctionApp.csproj
  --configuration Release --output artifacts/function-app/routine-task-xp-verify`
  succeeded, and the published artifact contained `config/routine-tasks.json`.
- `deployment/config/development.json` was parsed successfully with PowerShell
  `ConvertFrom-Json`.
- `git diff --check` completed without whitespace errors.
- After the review fixes, `dotnet build NotionManagement.sln` again succeeded with zero
  warnings and zero errors, and `npm run build` again completed configuration validation,
  TypeScript checking, and the Vite production build successfully.
- Cosmos-backed runtime scenarios and on-device Android acceptance flows were not run in
  this workspace; they remain manual acceptance work for the feature review stage.

## Review findings

Review date: 2026-09-17

Re-review date: 2026-09-17

Comparison base: `main...HEAD`

The local `main` branch was used as required. Its freshness relative to the remote was not
verified because the review did not fetch or otherwise alter Git state.

### Important — Late routine responses can replace a newer or different-day client state

`RoutineTasksPage` applies successful mutation results and same-day conflict occurrences to
whatever list is current when the response arrives. It does not verify that the current list
still has the request's `expectedBusinessDate`, nor that the response version is at least as
new as the currently rendered occurrence. A scheduled 03:00 reload, manual refresh, or update
from another client can therefore install a newer list before an older mutation response
arrives; the late response then replaces that newer row. Across the business-day boundary it
can render the previous day's occurrence in the new day's list. The backend remains protected
by its date and version preconditions, but the Android UI can temporarily present stale or
wrong-day confirmed state and send a follow-up request from that stale version.

Recommendation: apply mutation and conflict responses only when the currently rendered
business date matches the response/request business date and the returned occurrence is not
older than the currently rendered version. Otherwise discard the late row update and reload
the authoritative list. Cover the race between a mutation, manual refresh, scheduled boundary
refresh, and a second-client update during the next implementation pass.

Status: resolved in the post-review implementation pass. Mutation and conflict results are
guarded by business date and occurrence version; concurrent list loads are sequenced and
cannot lower an already rendered occurrence version.

### Important — Client documentation contradicts the implemented source of truth

`NotionManagementApp/README.md` first states that the Function App and repository-level
`config/routine-tasks.json` are authoritative, but the following paragraphs still direct
maintainers to the deleted `src/config/routine-tasks.json`, say entries contain only `id` and
`name`, and claim completion and skip state remain only in `localStorage` with no Function App
events. This contradicts the feature's central configuration, persistence, and XP data-flow
decisions and can cause incorrect future configuration or maintenance work.

Recommendation: replace the obsolete paragraphs with the current shared configuration shape,
required `effort`, backend-persisted confirmed state, retry behavior, and 03:00 server-owned
business-day refresh.

Status: resolved in the post-review implementation pass. The obsolete local configuration
and `localStorage` descriptions were replaced with the implemented server-backed flow.

### Suggestion — Current diff does not pass the recorded whitespace check

`git diff --check main...HEAD` reports trailing blank lines at the ends of
`RoutineTaskConfiguration.cs`, `RoutineTaskModels.cs`, and `RoutineTaskOptions.cs`. This does
not affect runtime behavior, but it contradicts the implementation-stage validation note that
the whitespace check passed.

Recommendation: remove the three extra EOF blank lines and rerun `git diff --check`.

Status: resolved in the post-review implementation pass. The extra EOF blank lines were
removed.

### Re-review outcome

No new blocking, important, or suggestion findings were identified. The post-review changes
resolve all previously recorded findings. The feature is not marked ready until the user
accepts the review result and the remaining manual acceptance work is acknowledged.

### Review validation

- `dotnet build NotionManagement.sln` succeeded with zero warnings and zero errors.
- The routine configuration validator and `tsc --noEmit` succeeded.
- Vite production bundling succeeded when directed to
  `artifacts/review-routine-task-xp-vite`.
- The standard `npm run build` reached Vite after successful configuration and TypeScript
  validation, but Vite could not remove the existing `NotionManagementApp/dist/assets`
  directory because Windows returned `EPERM`. This appears to be a local filesystem lock; it
  prevented validating the standard output-directory cleanup path in this review.
- `git diff --check main...HEAD` reported the three whitespace issues described above.
- Cosmos-backed and on-device Android scenarios were not executed.

### Re-review validation

- `dotnet build NotionManagement.sln` succeeded with zero warnings and zero errors.
- The routine configuration validator and `tsc --noEmit` succeeded.
- Vite production bundling of the updated client succeeded when directed to
  `artifacts/review-routine-task-xp-vite-rerun`.
- `git diff --check main...HEAD` completed without output.
- The standard `npm run build` again reached Vite after successful configuration and
  TypeScript validation, but the local Windows environment still returned `EPERM` while Vite
  tried to remove the existing `NotionManagementApp/dist/assets` directory. The alternative
  output build confirms bundling, but the locked standard output directory remains a local
  validation limitation.
- Cosmos-backed runtime scenarios and on-device Android acceptance flows were not executed.

## Manual acceptance checklist

- [ ] Start with no occurrence document for the current business day and confirm the backend
  returns every configured routine as `pending`, version `0`, with the expected effort and XP.
- [ ] Complete a routine and confirm the UI changes only after the response, one transition and
  one `routine` XP award are stored, and all-time/day/week/month/year values increase by the
  configured amount.
- [ ] Confirm the XP event-history endpoint exposes routine awards and revocations with
  `subjectType = routine`, while legacy regular-task events without the field resolve as
  `subjectType = task`.
- [ ] Reopen and recomplete a routine on the same business day; confirm the original award is
  revoked exactly once and only one active net award remains.
- [ ] Change configured effort after completion, then reopen and recomplete; confirm revocation
  uses the original snapshot and recompletion uses the new effort and XP mapping.
- [ ] Exercise `pending -> skipped`, `skipped -> pending`, `skipped -> completed`, and
  `completed -> skipped`; confirm every actual transition remains in routine audit history and
  only completion-boundary transitions create XP events.
- [ ] Retry an accepted operation after losing its response and confirm the same result is
  replayed without another transition or XP change. Reuse that operation ID with a different
  payload and confirm a conflict.
- [ ] Send two operations from the same expected version and confirm only the first is accepted;
  the second returns current authoritative state without audit or XP side effects.
- [ ] Simulate a backend failure and confirm Android keeps the last confirmed state and retries
  with the same operation ID.
- [ ] Race a mutation with manual refresh, another-client update, and the scheduled 03:00
  refresh; confirm no late response can replace a newer occurrence or place the previous day's
  occurrence into the new day's list.
- [ ] Keep the routine screen open across 03:00 Europe/Warsaw and confirm an old-day mutation is
  rejected or safely discarded and the new business-day schedule is loaded.
- [ ] Confirm pre-feature `localStorage` state is neither uploaded nor converted into database
  history or XP.
- [ ] Confirm missing, empty, and unsupported effort values fail frontend build validation and
  backend startup configuration loading.
- [ ] Restart the Function App and application, then confirm completed and skipped current-day
  state remains available from Cosmos DB.

## Open questions

None.

## Decisions log

| Date | Decision | Reason |
|---|---|---|
| 2026-09-17 | Start business discovery for Routine Task XP as a separate feature. | The feature extends both routine-task completion and the existing XP progression/visualisation. |
| 2026-09-17 | Store an effort value on every configured routine task. | Routine XP should be derived analogously to XP for regular Notion tasks. |
| 2026-09-17 | Allow at most one routine-task XP award per stable routine ID and business day. | Completion, reopening, and recompletion during one day must not multiply the earned XP. |
| 2026-09-17 | Include routine-task XP in the existing shared progress visualisation for the applicable business day. | Routine work should contribute alongside regular tasks completed that day. |
| 2026-09-17 | Let the same routine earn XP again on a later business day. | The daily occurrence is independently completable and rewardable. |
| 2026-09-17 | Revoke routine XP when a completed routine is reopened and restore it when the routine is completed again. | Match the existing regular-task XP lifecycle while ensuring that completion cycling cannot create more than one net daily award. |
| 2026-09-17 | Reuse the regular-task effort levels and mapping: Trivial/Easy/Medium/Hard/Epic = 5/10/25/50/100 XP. | Keep routine and regular tasks in one consistent progression system. |
| 2026-09-17 | Reject missing, empty, or unsupported routine effort during the application build; do not fall back to Medium. | Every configured routine must explicitly declare its effort, and configuration mistakes must not be hidden. |
| 2026-09-17 | Award 0 XP for a skipped routine and retain a day-specific skip record. | Skipping is not completion, but the user wants an auditable trace that the routine was intentionally skipped that day. |
| 2026-09-17 | Retain routine skip records as durable history beyond the end of the business day. | The user wants the record to remain available after the daily routine state resets. |
| 2026-09-17 | Preserve every routine state transition in durable history, not only the final daily outcome. | A skip or other meaningful action must remain auditable even if the routine state changes again later that day. |
| 2026-09-17 | Store routine transition history durably in the database without a user-facing history view in the first scope. | The record must survive daily resets, but the user does not currently need to browse it. |
| 2026-09-17 | Snapshot effort and XP on each routine award; revoke the original amount and use current effort only for a later recompletion. | Preserve correct totals and match the established regular-task XP lifecycle without retroactive recalculation. |
| 2026-09-17 | Allow the same stable routine ID to have different effort values on different weekdays. | Routine difficulty may depend on the day; the applicable weekday entry determines the award. |
| 2026-09-17 | Retain routine transition history indefinitely. | The user wants a permanent audit trail and no automatic retention cutoff in the current scope. |
| 2026-09-17 | Use the database as the shared source of truth for current daily routine state. | Android is the only initial client, but central state prevents future web and Android clients from diverging or awarding duplicate XP. |
| 2026-09-17 | Apply routine state changes in the Android UI only after backend confirmation; retain the previous state on failure and allow retry. | Keep the UI consistent with durable state, transition history, and XP effects without introducing offline synchronisation in the current scope. |
| 2026-09-17 | Include routine awards and revocations in the shared all-time, day, week, month, and year XP values. | Routine XP is part of the same progression system and differs only by its source. |
| 2026-09-17 | Expose routine awards and revocations in the existing XP event history, while keeping zero-XP transitions only in routine audit history. | Match regular-task XP semantics without polluting XP history with events that do not change XP. |
| 2026-09-17 | Do not import or award historical routine completions from the previous device-local state. | Earlier local data is not a complete auditable source; routine XP and durable history begin with the deployed feature. |
| 2026-09-17 | Store immutable historical snapshots and do not resolve old records through current routine configuration. | Names and configured values must reflect the moment of the event and must not change retroactively. |
| 2026-09-17 | Use backend acceptance time as the authoritative routine event time. | Provide one trusted basis for business-day and XP-period attribution across current and future clients. |
| 2026-09-17 | Make routine state-change retries idempotent. | A lost response and retry must not duplicate the logical transition, durable audit record, or XP effect. |
| 2026-09-17 | Mark business discovery as complete. | The confirmed business scope, rules, boundaries, and edge cases have no remaining material open questions. |
| 2026-09-17 | Let the Function App resolve and return the complete routine list and persisted state for its authoritative current business day. | A server-owned response prevents client/server schedule drift and keeps configuration, business-day attribution, persisted state, and XP calculation consistent. |
| 2026-09-17 | Reject routine updates based on a stale occurrence version and return the current state. | Optimistic concurrency prevents an older client view from silently overwriting a newer accepted transition while preserving safe retry of the same operation. |
| 2026-09-17 | Keep routine APIs anonymous behind an unguessable route segment for the first scope. | This matches the current mobile API architecture and avoids expanding the feature into authentication, while accepting that an embedded route is obscurity rather than access control. |
| 2026-09-17 | Move the single routine configuration file to repository-level `config/routine-tasks.json`. | Both the Android build and Function App consume the configuration, so neutral ownership avoids a backend dependency on a frontend-owned path and prevents duplicate sources of truth. |
| 2026-09-17 | Give routine endpoints a dedicated `RoutineTasks:RouteSegment`. | Routine mutations can be configured and rotated independently without changing the existing Task XP read URLs. |
| 2026-09-17 | Add `subjectType` (`task` or `routine`) to shared XP events and interpret the field as `task` when absent. | The XP subject remains identifiable without overloading the existing event-source channel or migrating historical Cosmos documents. |
| 2026-09-17 | Use `pending`, `completed`, and `skipped` routine states and an idempotent, versioned PUT contract with business-date preconditioning. | Explicit target state, operation identity, and concurrency preconditions make retries safe, prevent stale overwrites, and stop an old-day screen from changing a new-day occurrence. |
| 2026-09-17 | Mark technical design as complete and the feature as ready for implementation. | The architecture, API contract, persistence model, concurrency and idempotency rules, rollout changes, implementation sequence, and verification plan are confirmed with no remaining open questions. |
| 2026-09-17 | Assign `Easy` to school/work packing, `Medium` to kitchen cleanup, and `Hard` to the workout on every configured weekday. | The user confirmed the initial effort values required to implement explicit routine XP configuration. |
