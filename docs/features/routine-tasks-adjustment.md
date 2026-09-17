# Feature: Routine Tasks Adjustment

## Status

Implementation complete

## Goal

Make the routine-task schedule easy to read and maintain per weekday without changing the existing daily routine experience.

## Problem statement

The current configuration is task-oriented: every task contains the weekdays on which it appears. The user wants to manage the schedule from the opposite perspective by opening a weekday and seeing or editing that day's complete task list.

## Initial idea

Keep a configuration file in the repository organized by weekday. Every weekday contains its own list of task objects, which can gain more properties later. At the start of each new business day, at 03:00 in the Europe/Warsaw time zone, the configured list for that weekday should appear in the existing routine-task view.

## Business requirements

- Routine tasks must be configurable by day of the week.
- The top-level configuration structure must be organized by weekday rather than by task.
- Every weekday must contain a list of task objects.
- All seven weekdays must be explicitly present, including weekdays with no tasks.
- Task entries must remain objects so that more task properties can be added later.
- In the initial version, every task object contains only `id` and `name`.
- The initial configuration source must be a file stored in the repository.
- A new business day starts at 03:00 in the Europe/Warsaw time zone.
- The routine tasks configured under the new weekday must appear in the existing routine-task view when that business day starts.
- The existing completion and skip behavior must remain unchanged.

## User scenarios

- The user opens the routine-task view during a business day and sees the tasks configured for that business day's weekday.
- The business-day boundary passes at 03:00 Europe/Warsaw time and the routine-task view changes to the tasks configured for the new weekday.

## Business rules

- Weekday selection follows the Europe/Warsaw calendar after applying the 03:00 business-day boundary.
- A business day uses the task list nested under its weekday in the configuration.
- When the same conceptual task occurs on multiple weekdays, its complete task object is duplicated under every applicable weekday.
- Weekday task lists do not reference a shared task-definition catalog.
- A weekday with no configured routine tasks has an explicit empty task list.
- Tasks are displayed in the same order in which their objects occur in the configured weekday list.
- The same conceptual routine uses the same stable `id` in every weekday list where it occurs.
- Task IDs must be unique within each individual weekday list.
- Every occurrence of the same stable `id` must use the same `name` across all weekday lists.

## Edge cases

- A weekday has no routine tasks; its configuration contains an empty list and the existing empty state is shown.
- The same stable task ID may occur on different weekdays, but it must not occur more than once within one weekday.
- Reusing a stable task ID with a different name on another weekday is invalid configuration.

## Out of scope

- Creating routine tasks as persistent Notion tasks or adding them to the main task list.
- Changing the existing routine-task completion or skip behavior.
- Changing the 03:00 Europe/Warsaw business-day boundary.
- Adding new routine-task properties beyond `id` and `name`.

## Technical design

The change is confined to the React application. The repository configuration remains bundled with the application and is reshaped from a task-oriented array into a weekday-oriented object. The routine page selects one list directly by the existing business-period weekday instead of filtering every configured task.

Proposed configuration contract:

```json
{
  "monday": [
    { "id": "example-task", "name": "Example task" }
  ],
  "tuesday": [],
  "wednesday": [],
  "thursday": [],
  "friday": [],
  "saturday": [],
  "sunday": []
}
```

The application-level types should model this as `Record<DayOfWeek, RoutineTask[]>`, where `RoutineTask` initially contains only `id` and `name`. No backend, API, native Android, persistence format, or UI contract changes are required.

## Technical decisions

- Keep `src/config/routine-tasks.json` as the version-controlled source and change only its internal structure.
- Reuse the existing `getPeriod` calculation as the sole source of the active Warsaw business date and weekday.
- Select the configured list by `schedule[period.weekday]`; preserve its array order without sorting or copying.
- Preserve the existing local-storage key and `StoredRoutineState` shape. Stable task IDs make the saved completion and skip statuses compatible with the reshaped configuration.
- Do not add a runtime data service, backend endpoint, state-management library, or configuration dependency for this static schedule.
- Add a dependency-free Node.js validation script and run it as part of `npm run build`. It must fail the local and CI/APK build with a precise message for structural errors, missing or extra weekday keys, malformed task entries, duplicate IDs within a weekday, and inconsistent names for a repeated stable ID.

## Alternatives considered

- A shared catalog of task definitions referenced by weekday lists was rejected. Independent weekday lists are simpler to read and edit, and configuration duplication is acceptable.
- Keeping the task-oriented array and generating a weekday view only for maintainers was rejected because the confirmed requirement makes the repository configuration itself weekday-oriented.
- Moving the schedule to the Function App or Notion was rejected for the initial version because remote configuration and synchronization are outside the agreed scope.
- Relying only on a TypeScript type assertion is not recommended because it cannot enforce cross-entry rules such as per-day ID uniqueness and consistent names across weekdays.

## Architecture and data flow

1. Vite bundles `routine-tasks.json` with the React application as it does today.
2. `RoutineTasksPage` calculates the active business period using the existing Europe/Warsaw and 03:00 logic.
3. The active `DayOfWeek` indexes the schedule object and returns that day's configured array.
4. React renders the entries in configuration order and resolves completion or skip state by stable task ID from the unchanged local-storage record.
5. At the next 03:00 boundary, the existing timer calculates a new period, resets state for the new period key, and the new weekday selects its own configured list.

The format change does not require a persisted-state migration. The stored data contains only a period key and task statuses keyed by stable ID; it does not persist `daysOfWeek` or the task objects themselves.

## Security and operational considerations

- The schedule contains no secrets and remains a compile-time application asset.
- Configuration mistakes are developer/deployment errors rather than user input. The build should preferably reject a missing weekday, malformed task, duplicate ID within a weekday, or conflicting names for one stable ID before an APK is produced.
- The existing timezone implementation relies on the platform `Intl` time-zone database. This feature does not change that dependency or its daylight-saving-time behavior.
- No network, authentication, authorization, privacy, logging, or performance changes are introduced. Direct weekday lookup is negligible in cost and avoids the existing full-list filter.

## Implementation plan

1. Reshape `src/config/routine-tasks.json` into an object containing all seven English weekday keys and duplicate each complete `{ id, name }` task object under every applicable weekday.
2. Update the routine schedule types in `RoutineTasksPage.tsx` to represent the weekday-keyed object and remove `daysOfWeek` from `RoutineTask`.
3. Replace the filtering `useMemo` with direct lookup by `period.weekday`; keep rendering, storage, status transitions, rollover scheduling, and empty-state behavior unchanged.
4. Add the agreed configuration validation mechanism for structural and cross-weekday rules.
5. Update `NotionManagementApp/README.md` to describe the new format and validation workflow.
6. Run the frontend build, inspect the complete diff, and manually verify representative weekday, empty-day, status, and 03:00 rollover scenarios.

## Verification plan

- Run `npm run build` in `NotionManagementApp` and require TypeScript and Vite to succeed.
- Confirm all seven weekday properties are present and each value is an array.
- Confirm every task has non-empty string `id` and `name` values and no unsupported initial properties if strict validation is selected.
- Confirm IDs are unique within each weekday and every repeated ID has the same name across weekdays.
- Verify a business time before 03:00 selects the preceding calendar weekday and a time at or after 03:00 selects the current calendar weekday, including a Europe/Warsaw daylight-saving period.
- Verify the displayed count and ordering match the selected weekday array.
- Verify an empty weekday shows the existing empty state.
- Verify completing, uncompleting, skipping, and undoing a skip still persist for the active period and reset at the next business-day boundary.
- Inspect the final diff to ensure no backend, native Android, API, or unrelated UI behavior changed.

## Implementation notes

- Technical design was marked complete on 2026-09-17 and implementation started from the approved plan.
- Reshaped `src/config/routine-tasks.json` into seven explicit weekday lists, preserving the existing four daily routines and their stable IDs.
- Replaced task filtering in `RoutineTasksPage` with direct weekday lookup while preserving the existing business-period calculation, local-storage state, completion, skip, and rollover logic.
- Added `scripts/validate-routine-tasks.mjs` and made it the first `npm run build` step. It validates the schedule structure and all confirmed ID and name integrity rules without external dependencies.
- Updated the frontend README with the new schedule format and build validation behavior.

## Validation performed

- Ran `npm run build` in `NotionManagementApp` on 2026-09-17. The routine-task validator, `tsc --noEmit`, and the Vite production build completed successfully.
- Inspected the final change set. It contains no backend, API, native Android, persistence-format, or unrelated UI changes.

## Review findings

## Manual acceptance checklist

## Open questions

None.

## Decisions log

| Date | Decision | Reason |
|---|---|---|
| 2026-09-17 | Store the initial routine-task schedule in a repository configuration file. | The user wants configuration to remain version-controlled for now. |
| 2026-09-17 | Use 03:00 Europe/Warsaw as the start of a new business day. | The user explicitly defined this daily boundary. |
| 2026-09-17 | Allow a distinct routine-task schedule for each weekday. | The routine list must reflect recurring responsibilities that vary by weekday. |
| 2026-09-17 | Organize the configuration by weekday, with a list of task objects nested under each day. | The user wants to maintain the full schedule from the perspective of a selected day rather than a task. |
| 2026-09-17 | Keep the existing routine view, daily rollover, completion, and skip behavior unchanged. | This feature adjusts configuration orientation only. |
| 2026-09-17 | Duplicate complete task objects when the same task occurs on multiple weekdays. | The user accepts duplication and does not need shared task definitions or references. |
| 2026-09-17 | Require all seven weekday sections, using an empty list when a day has no tasks. | A complete explicit schedule is easier to understand and maintain. |
| 2026-09-17 | Keep only `id` and `name` in the initial task object. | No additional task properties are needed yet; object form preserves future extensibility. |
| 2026-09-17 | Use weekday-list order as the task display order. | The repository configuration should directly control the order visible to the user. |
| 2026-09-17 | Reuse one stable `id` for the same conceptual routine across weekdays and require IDs to be unique within a weekday. | Preserve task identity across the schedule while preventing ambiguous duplicate entries on one day. |
| 2026-09-17 | Require the same `name` for every occurrence of a stable task ID. | One identifier must consistently represent one conceptual routine across the entire schedule. |
| 2026-09-17 | Mark business discovery as complete. | The confirmed scope and business rules contain no remaining material business questions. |
| 2026-09-17 | Start technical design. | Business discovery is complete; the existing frontend architecture and configuration flow have been inspected. |
| 2026-09-17 | Validate the schedule with a dependency-free Node.js script invoked by `npm run build`. | Reject invalid repository configuration before local or CI/APK bundling, without adding an application runtime dependency. |
| 2026-09-17 | Mark technical design as complete and start implementation. | The approved design has no material open questions. |
| 2026-09-17 | Complete implementation. | The weekday-oriented configuration, build validation, direct lookup, documentation, and production build validation match the approved plan. |
