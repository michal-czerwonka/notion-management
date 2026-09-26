# Feature: Completed Task History

## Status

Technical design complete; ready for implementation

## Goal

Make XP progress explainable by showing period-based completion snapshots after same-business-day corrections, while keeping the results suitable for future analysis.

## Problem statement

The XP progress screen currently shows only the earned XP total. It does not show the completed work behind that result, distinguish routine work from regular tasks, or provide a task-level result that can later be analysed outside the screen.

## Initial idea

Add a completed-task section below the entire current XP progress section. Show completed routine tasks separately from regular tasks, associate all completed tasks with the selected XP period in the same way as earned XP, and keep the results easy to analyse in the future, for example as JSON.

## Business requirements

- The XP progress screen must show completed tasks below the existing progress section.
- Completed routine tasks and completed regular tasks must be presented as separate groups.
- Completed-task results must follow the selected XP period rather than being an unrelated all-time list.
- Day, week, month, and year views must include all task completions attributed to the selected period.
- The period attribution of completed tasks must be consistent with the period attribution used for XP.
- Every displayed task entry must include the task name and completion date and time, plus assigned effort when present.
- Every regular-task entry must preserve and display its completion-time project assignment as secondary, visually de-emphasized text when a project is assigned.
- Every completion must preserve and display the task name captured at completion time; later task renames must not change historical completion snapshots.
- Individual completed-task entries must not display the awarded XP amount.
- Project and effort must be displayed as separate stacked metadata lines below the task name.
- Although a regular task is expected to belong to at most one project in normal business use, every project returned by the source must be preserved; when several are present, each project must be displayed on its own line.
- When project or effort is missing, its metadata line must be omitted; the interface must not display `Brak projektu` or `Brak effortu` placeholders.
- The internal `Medium` XP fallback must not be presented as an assigned effort.
- The displayed result must contain accepted completion snapshots, not a user-facing log of every completion and reopening event.
- Reopening a task during the same business day as its completion must remove that completion from completed-task results as though it had not occurred.
- Reopening a task during a later business day must not change the completion snapshot or XP retained for the earlier business day.
- Completing the task again after a later-day reopening must create another completion snapshot and award XP again.
- The underlying result must remain suitable for future structured analysis, including representation as JSON.
- The first version must provide completed-task data to the application in a structured contract suitable for future analysis.
- The structured result must include the stable regular-task or routine-task identifier, even though the identifier is not displayed in the user interface.
- The initial release must backfill completed-task snapshots for historical periods from the existing durable activity history rather than showing only completions recorded after deployment.
- Legacy completion snapshots reconstructed from Cosmos DB do not need project metadata when the historical event did not capture it.
- The first version does not need a user-facing action to download or copy JSON.
- Every accepted routine completion must be represented as a separate entry; repeated completion of the same routine on different business days must not be collapsed into a count.
- Within both the routine-task and regular-task sections, entries must be grouped by business day in newest-first order.
- Within each business-day group, entries must be ordered by completion time from newest to oldest.

## User scenarios

- A user views XP progress for a period and can see the completed work associated with that result.
- A user can distinguish completed routine tasks from completed regular tasks.
- A user switches to a week, month, or year and sees every task completion attributed to that selected period.
- A user can identify when each task was completed and which effort was assigned, when an effort was present.
- A user can see the project associated with a regular task at completion without the project competing visually with the task name.
- A user renames a task after completion and the earlier completion continues to show its original name.
- A user sees only the metadata actually assigned to a task; missing project and effort lines are absent.
- A user sees every assigned project on a separate line when source data contains multiple project assignments.
- A user reopens an accidentally completed task on the same business day and its completion and XP disappear from that day's result.
- A user reopens a task on a later business day and the earlier completion remains in its historical periods.
- A user completes that task again and sees both accepted completions in a period containing both business days.
- A user completes the same routine on multiple business days and sees a separate dated entry for every accepted completion.
- A user reviews a longer period and sees its newest business day first, with the newest completions first within that day.
- In the future, the user can analyse the period's completed-task result as structured data without reconstructing it from the visual layout.
- Future analysis can associate repeated completions by stable identifier even when a task was renamed or different tasks share the same name.
- After rollout, a user can navigate to an older eligible XP period and see completion snapshots reconstructed from existing history.
- A legacy regular-task completion without stored project data is shown without a project line.

## Business rules

- Completed tasks use the same business-period model as XP progress.
- A task completion belongs to the day, week, month, and year containing its completion time under the established XP business-period rules.
- The completion entry exposes the task name, completion timestamp, and observed assigned effort when present.
- Task name is an immutable completion-time snapshot rather than a reference resolved from the task's current name.
- Regular-task project assignment is an immutable completion-time snapshot rather than a value resolved from the task's current project assignment.
- Project snapshots retain all source project assignments even though normal business use expects no more than one.
- Historical backfill must apply the same same-business-day correction rule as new completion snapshots whenever the source history contains enough information to identify the completion and reopening events.
- Backfill must not populate a legacy project snapshot from the task's current Notion assignment; unavailable historical project data remains missing.
- Stable task identity is preserved separately from the completion-time name snapshot and is not part of the visible task entry.
- The structured completion result must preserve missing project or observed effort as missing data, while the visible entry omits the corresponding metadata line.
- A regular-task completion remains reversible until the end of its business day.
- A same-business-day reopening invalidates the completion snapshot and reverses its XP.
- After the completion's business day has ended, that completion snapshot and its XP remain attributed to their original periods even if the task is reopened later.
- A reopening on a later business day starts a new completion opportunity. A later completion creates another snapshot using its own completion time and effort and awards XP again.
- The same task may therefore appear more than once and award XP more than once in a week, month, or year when its accepted completions occurred on different business days.
- Every accepted daily occurrence of a routine task is a separate completion snapshot with its own completion time and effort.
- Display grouping uses the established Europe/Warsaw business day rather than midnight-based calendar grouping.

## Edge cases

- A task is completed and reopened within the same business day.
- A task is completed in one period and reopened in a later day, week, month, or year.
- A reopened task is completed again with a different completion time or effort.
- A task is completed, reopened, and completed again within one business day; only the final active completion for that business day remains.
- A period contains multiple accepted completions of the same task from different business days.
- A legacy completion lacks a field introduced by this feature, such as a project snapshot.

## Out of scope

- A user-facing audit timeline of every completion, reopening, and correction event.
- A user-facing JSON download or copy action in the first version.
- Displaying the XP amount awarded by an individual completed task.

## Technical design

### Existing system and constraints (analysis, 2026-09-26)

- The .NET Function App owns Task XP and routine mutations. One Cosmos DB container uses the `personal` partition for delivery receipts, task state, XP events, XP totals, period projections, routine occurrences, and routine transitions. Both mutation paths use a transactional batch and ETag retries.
- `BusinessPeriodCalculator` defines a 03:00 Europe/Warsaw business day and derives day, Monday-based week, month, and year. The progress endpoint validates normalized period starts and eligibility against the first daily-target date.
- Regular-task `xp-event` documents contain stable task ID, completion-time name, observed effort, applied effort, amount, event time, and change type. They do not contain projects or a durable link between an award and its correction. `task-state` retains only the latest state and active award amount; it has no active award timestamp.
- Routine `xp-event` documents contain routine ID, name, effort, amount, and time. Routine transition documents additionally contain business date and prior/target state. Existing routine occurrences preserve only the current daily state and award, so historical accepted completion times must come from events or transitions.
- The Notion task snapshot used for XP currently omits projects. The Today Tasks client already resolves all project relations for a different read path; a completion-time snapshot must use that information on both Android and webhook ingestion paths. Current Notion assignments cannot reconstruct historical projects.
- The React XP screen requests one period aggregate and renders a single progress card. Its API parser validates the aggregate contract. The new result belongs below that card and must load for the same selected period.
- Current regular-task reopening always creates a negative XP event and applies it to the *reopening* period. This conflicts with the confirmed same-business-day correction and later-day retention rules. Implementing the history list alone would leave XP and completion history inconsistent.
- The signed Notion webhook fetches the page's current state and may miss a transient sequence completed entirely before delivery; the existing Task XP feature accepts that source limitation. Historical backfill can reconstruct only transitions actually persisted in Cosmos DB.

### Approved design

- The separate completion document will include subject type, stable task/routine ID, completion-time name, actual completion timestamp, derived business date, observed effort (nullable), and completion-time project names for regular tasks. Keep an internal reference to the award event or completion cycle so a same-day reopening can invalidate precisely that snapshot. Do not resolve display fields from live Notion/configuration.
- Use the award event ID as the basis of a deterministic completion document ID. Extend regular-task state with the active completion ID, business date, and awarded amount. Create a snapshot and positive XP event atomically on completion. On a same-business-day reopening, delete the active snapshot and write a negative audit event; subtract the original award from all-time XP and the four periods containing the original completion. On a later-day reopening, clear the active state but preserve the snapshot and XP; write a delivery receipt but no XP-affecting event. The next completion starts a new occurrence. Routine batches use their existing daily occurrence to create or invalidate one active completion snapshot for that business day. Retain immutable XP and routine-transition events as the audit trail.
- Keep the existing `/progress` contract unchanged. Add `GET /api/task-xp/{segment}/completions?period=day|week|month|year&periodStart=yyyy-MM-dd&continuationToken=<opaque>` with the same period normalization and eligibility rules. Return period bounds, a page of structured entries, and an optional continuation token. Each entry has a stable completion ID, subject type, stable subject ID, completion-time name, completion timestamp, business date, nullable observed effort, and project-name array. Do not include individual XP. Query completion documents in the `personal` partition by business-date range, then have the client load every page and group/sort entries by business date and completion time (descending, with stable ID as a tie-breaker). This keeps a full year available without one unbounded HTTP response.
- Backfill from existing regular and routine XP events, ordered by subject identity and event time, pairing each award with the following revoke where unambiguous. Verify routine pairs against routine-transition history where available. Derive snapshot IDs from award event IDs, so retries cannot create duplicate completions; preserve absent legacy project data as absent. For unambiguous historic later-day revocations, restore the wrongly subtracted amount to all-time XP and to the four periods containing the old *reopening* event. Keep old immutable events unchanged and record a migration adjustment/marker that explains each projection correction. Never rewrite the original completion period's XP. Leave XP for ambiguous skipped cases unchanged and report possible discrepancies.
- Run the one-time backfill as a restartable maintenance operation before enabling the new client view. First produce a dry-run report of candidate snapshots, skipped/ambiguous sequences, and XP adjustments. Apply deterministic snapshot writes and idempotent projection adjustments with ETag checks and migration markers; record completion only after post-run reconciliation. Deploy the new write behavior before starting the backfill and exclude already materialized award IDs. During the transition, a regular-task state without the new active completion reference must resolve it from unambiguous persisted award history before processing a reopen; report an unresolved case rather than guessing. The operational procedure must specify how to resume after interruption.

## Technical decisions

- Persist each accepted completion as a separate snapshot document in the existing Cosmos DB container and `personal` partition.
- Create or invalidate snapshots in the same transactional batch as XP and state changes. Track the active completion in regular-task state; later-day reopening preserves its snapshot and XP.
- Add a paginated completion endpoint under the existing Task XP read route, while keeping the current progress contract unchanged. The application loads all pages for the selected period and groups routine and regular completions by business day.
- During the one-time historical backfill, reconstruct every completion snapshot that can be determined unambiguously from existing Cosmos DB history. Omit ambiguous legacy cases and list them in the migration report; do not invent a completion or correction. This does not change how new completions are recorded.
- Leave historical XP unchanged for ambiguous legacy cases omitted from the backfill. Report any possible XP-to-snapshot discrepancy for those cases instead of estimating a correction.
- Reconcile unambiguous historic later-day revocations through durable migration adjustments, leaving original XP events intact.

## Alternatives considered

- Deriving every read directly from `xp-event` history would avoid new snapshot writes, but period reads would grow with all-time history and require repeated correction pairing. Historical metadata would still be limited by what events captured. The user selected persisted completion snapshots.
- Embedding all completed tasks in `xp-progress` documents would make simple point reads possible, but year documents would grow without a clear bound and increase write contention and Cosmos item-size risk. The selected separate occurrence documents avoid that growth.

## Architecture and data flow

- Android status update or signed Notion webhook -> canonical Notion task snapshot -> Task XP service/repository -> transactional Cosmos write of state, audit event, accepted completion projection, all-time XP, and four period XP aggregates.
- Routine mutation -> routine service/repository -> transactional Cosmos write of daily occurrence, transition audit, accepted completion projection, XP event, all-time XP, and four period XP aggregates when XP changes.
- XP screen -> existing progress endpoint plus paginated period-completions endpoint -> structured completion entries -> two sections grouped by business date.

## Security and operational considerations

- Reuse the existing unguessable Task XP read route segment and `Cache-Control: no-store` behavior; no new authentication scheme or external service is needed.
- Backfill requires a one-time operational path with progress logging, bounded Cosmos reads/writes, restartability, and a dry-run discrepancy report. The report must identify ambiguous skipped cases. The migration must not infer missing historical project assignments from current Notion state.
- Define the migration report format after inspecting representative legacy data. It must identify skipped snapshots and possible XP-to-snapshot discrepancies without inventing missing events or metadata.
- Historic XP projection corrections will have separate durable migration-adjustment records. Existing `xp-event` records remain unchanged, so an old event-only sum is not a complete explanation of a migrated XP total; the adjustment records and report provide that explanation.
- Additional completion documents and indexed period queries increase Cosmos storage and RU consumption. Check query shape and indexing against the existing container before implementation.

## Implementation plan

Approved implementation sequence:

1. Add a completion document model and active completion reference to regular-task state. Capture project names on the canonical Notion snapshot for both Android and webhook paths; preserve every returned relation and observed effort separately from the XP fallback.
2. Update regular-task state and transactional batch writes for same-day invalidation, later-day retention, and subsequent completion. Extend routine batches to create/invalidate daily completion snapshots. Preserve receipt deduplication and ETag retry behavior.
3. Implement a restartable legacy backfill with a dry-run report, deterministic snapshot IDs, per-adjustment migration markers, and historic XP reconciliation. Document the rollout order and run it before enabling the client view.
4. Add the paginated completion endpoint, client parser, and complete-page loading. Render separate routine/regular groups below the progress card with the confirmed metadata and sorting rules.
5. Build both applications, run the manual verification below, inspect the complete diff, and update operational and feature documentation with actual validation.

## Verification plan

- Build the Function App and the React application with their existing commands; inspect the complete diff.
- Manually verify regular completion, same-day reopen/recompletion, later-day reopen/recompletion, routine repeat across days, duplicate delivery, and delayed/out-of-order delivery around 03:00 Europe/Warsaw and period boundaries.
- Check that each visible entry uses the completion-time name, all captured projects, observed effort only, and no per-entry XP; inspect the JSON contract for stable IDs and null/missing metadata.
- Run backfill first in dry-run mode against representative legacy events, including both correction types and missing project data; verify restartability, no duplicate snapshots, and that unambiguous XP period/all-time totals reconcile with accepted completions. Check reported discrepancies for ambiguous cases separately.
- Verify empty and long periods, ordering, refresh/error behavior, and consistency between selected XP and completion-history periods.

## Implementation notes

## Validation performed

## Review findings

## Manual acceptance checklist

## Open questions

None.

## Decisions log

| Date | Decision | Reason |
|---|---|---|
| 2026-09-26 | Start business discovery for Completed Task History as a separate feature. | The requested capability extends the completed XP Progress Visualization feature with task-level history and future analysis needs. |
| 2026-09-26 | Place completed-task results below the existing XP progress section. | Preserve the current progress summary and add the supporting completed work beneath it. |
| 2026-09-26 | Present routine tasks separately from regular tasks. | Both groups follow the selected XP period and remain distinguishable. |
| 2026-09-26 | Attribute completed-task results to the same selected periods as XP. | Task history should explain the result shown for the selected XP period. |
| 2026-09-26 | Keep the result suitable for future structured analysis such as JSON. | The user wants the accumulated history to remain reusable beyond the immediate UI. |
| 2026-09-26 | Show all task completions attributed to the selected day, week, month, or year. | The task list should follow the selected XP period and explain the work represented by it. |
| 2026-09-26 | Show each task's name, completion date and time, and assigned effort. | These fields are required to understand an individual completion and support later analysis. |
| 2026-09-26 | Treat reopening as a correction only when it occurs in the same business day as completion. | Accidental completion should be reversible during that day without rewriting already closed historical days. |
| 2026-09-26 | Preserve a completion and its XP when the task is reopened on a later business day. | Closed daily results are historical snapshots and must not be changed by later task state. |
| 2026-09-26 | Treat a later-day reopening as enabling a new completion opportunity. | The same task may legitimately award XP and appear in completion results again on a later business day. |
| 2026-09-26 | Keep the existing technical audit history out of the user-facing completed-task result. | Durable events can continue supporting diagnostics and data integrity without adding business-facing noise. |
| 2026-09-26 | Return structured completed-task data in V1 without a user-facing JSON export action. | Preserve easy future analysis while avoiding an export interface before it is needed. |
| 2026-09-26 | Represent every accepted routine completion as a separate entry. | Preserve the date, time, and effort of each occurrence and keep longer-period results analytically useful. |
| 2026-09-26 | Do not show an individual XP amount on completed-task entries. | The list describes completed work; aggregate XP remains visible in the existing progress section. |
| 2026-09-26 | Group both task sections by business day and sort days and entries newest first. | Make longer periods easy to scan while remaining consistent with XP's 03:00 Europe/Warsaw day boundary. |
| 2026-09-26 | Display `Brak effortu` for a regular task completed without an assigned effort. | Superseded later the same day by omitting missing metadata lines entirely. |
| 2026-09-26 | Preserve the task name captured at completion time. | Keep historical results stable and prevent later Notion renames from rewriting earlier completion snapshots. |
| 2026-09-26 | Include a stable task or routine identifier in structured results without displaying it. | Enable reliable analysis of repeated completions despite renames or duplicate task names. |
| 2026-09-26 | Preserve and display a regular task's completion-time project assignment as secondary text. | Make the task's context visible and retain project-level analysis despite later project changes. |
| 2026-09-26 | Stack project and effort as separate secondary lines and omit either line when its value is missing. | Keep entries compact and avoid placeholder text that does not add useful information. |
| 2026-09-26 | Display every assigned project on a separate line if source data contains more than one. | Normal business use expects one project, but the source does not enforce that constraint and the interface must not silently discard data. |
| 2026-09-26 | Backfill completion snapshots for earlier periods from existing durable history during rollout. | Historical XP periods should also explain which tasks were completed instead of the feature starting with an empty history at deployment time. |
| 2026-09-26 | Use the existing Cosmos DB for NoSQL history as the only backfill source; MongoDB is not part of the system or planned scope. | The earlier MongoDB reference was a terminology mistake, and the application already has its durable history in Cosmos DB. |
| 2026-09-26 | Omit project metadata from legacy snapshots when existing Cosmos DB history does not contain it. | Current Notion assignments are not reliable historical evidence; older entries remain valid without a project line. |
| 2026-09-26 | Mark business discovery as complete. | The product goal, visible data, period and correction rules, historical backfill, analysis readiness, and scope boundaries are confirmed with no remaining business questions. |
| 2026-09-26 | Persist a separate accepted-completion snapshot in the existing Cosmos DB container. | The user confirmed durable per-completion documents for predictable period reads and future structured analysis. |
| 2026-09-26 | Backfill only unambiguous legacy completion snapshots and report ambiguous cases. | The user chose a one-time reconstruction that does not infer unsupported historical facts. |
| 2026-09-26 | Leave XP unchanged for ambiguous legacy cases omitted from backfill and report possible discrepancies. | A correction without a reliable event sequence would be speculative. |
| 2026-09-26 | Approve the technical design and complete the design stage. | The user accepted the completion schema, atomic correction flow, paginated read contract, and restartable historical migration plan. |
