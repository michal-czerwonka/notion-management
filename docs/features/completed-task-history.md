# Feature: Completed Task History

## Status

Business discovery complete; ready for technical design

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
- Every displayed task entry must include the task name, completion date and time, and assigned effort.
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
- A user can identify when each task was completed and which effort was assigned to it.
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
- The completion entry exposes the task name, completion timestamp, and assigned effort.
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

## Technical decisions

## Alternatives considered

## Architecture and data flow

## Security and operational considerations

## Implementation plan

## Verification plan

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
| 2026-09-26 | Present routine tasks separately from the other requested completed-task group. | The user wants the two kinds of completed work to remain distinguishable; the exact period scope of the second group is still open. |
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
