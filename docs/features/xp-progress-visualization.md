# Feature: XP Progress Visualization

## Status

Complete; extended manual acceptance pending

## Goal

Make earned XP immediately understandable by showing progress over relevant time periods in the Android application.

## Problem statement

Task XP records individual awards and revocations and exposes the current total, but it does not show whether the user's recent XP activity meets their intended daily pace.

## Initial idea

Show a simple V1 progress bar in the Android application for XP earned in the current day. The user should be able to switch the view to week and month, and should also be able to see XP earned since the beginning of the current year. The initial desired target is 100 XP per day for the upcoming week. A business day starts and ends at 03:00 in the Europe/Warsaw time zone.

## Business requirements

- The Android application must display a visual representation of XP earned in the current day, week, month, and year.
- The first version must use a simple progress-bar-style representation.
- The progress view must support day, week, month, and year filters; day is the default filter.
- The user must be able to navigate from a selected period to earlier periods of the same type.
- The progress visualisation must be a dedicated Android screen available through the right-side application tab.
- V1 must not persist progress data locally in the Android application.
- While progress is being retrieved, the Android application must use its existing loading treatment. If retrieval fails, it must use its existing error treatment rather than showing cached progress.
- The progress screen retrieves data when opened and lets the user manually refresh it.
- The user wants to set a target of 100 XP for every day in the upcoming week.
- The initial 100 XP daily target remains in effect indefinitely until it is changed in the repository configuration.
- The initial 100 XP daily target is effective from 2026-09-17.
- A business day starts and ends at 03:00 in the Europe/Warsaw time zone.
- In V1, the effective-dated daily-target history is configured in a repository file; it does not need to be configurable through the application.
- The daily target must be a positive whole number of XP.
- The repository configuration must include the date from which the daily target becomes effective.
- The user must not be able to select a progress period that is entirely before the configured effective date.
- A period that overlaps the effective date is selectable; its target is calculated only for business days from the effective date onward.
- The target for a week, month, or year is the sum of the daily targets that belong to that period; those periods have no independent targets in V1.

## User scenarios

- A user opens the Android app and sees current-business-day XP progress toward that day's target.
- A user switches the progress view to week or month to see XP accumulated in that period.
- A user switches the progress view to year to see XP accumulated since the beginning of the current year.
- A user navigates to an earlier day, week, month, or year and sees that period's XP progress.
- A user sees the application's existing loading state while progress is loading and its existing error state when progress cannot be retrieved.
- A user opens the right-side application tab to access the dedicated XP progress screen.
- A user manually refreshes the open progress screen to retrieve the latest XP data.
- A user cannot navigate to progress periods that end before the configured effective date.

## Business rules

- XP belongs to the business day, week, month, and year containing its actual XP-event time when interpreted in the Europe/Warsaw time zone with the 03:00 day boundary.
- For a selected period, progress is earned XP divided by the sum of that period's daily targets.
- The target shown by a progress bar covers the complete selected period, not only its elapsed days. For example, a seven-day business week has a 700 XP target when the daily target is 100 XP.
- A completed historical period retains the daily target that applied when that period occurred; later target changes must not recalculate its displayed progress.
- A changed daily target takes effect at the start of the next business day, 03:00 Europe/Warsaw time. A period target is the sum of the daily targets effective on its individual business days.
- When earned XP exceeds the selected period's target, the progress bar may visually extend beyond 100%, uses a success-associated colour, and shows a trophy icon next to the XP counter.
- If the signed XP total for the selected period is negative, the visualised earned XP is shown as 0 XP.
- A business week starts on Monday at 03:00 Europe/Warsaw time and ends immediately before the following Monday at 03:00.
- A business month starts at 03:00 Europe/Warsaw time on the first calendar day of the month.
- A business year starts at 03:00 Europe/Warsaw time on 1 January.

## Edge cases

- A completion occurs between midnight and 02:59 Europe/Warsaw time and belongs to the preceding business day.
- A target is not reached, reached exactly, or exceeded.
- Earned XP exceeds the period target.
- A reopening revokes enough XP for the selected period's signed total to become negative.
- An available period contains no XP events.
- A period crosses a calendar boundary or a daylight-saving-time change.

## Out of scope

- Advanced dashboard visuals and complex screen design beyond a simple V1 progress view.
- Comparing periods with one another.

## Technical design

Extend the existing Task XP projection model rather than querying or duplicating its immutable history. Cosmos DB remains a single logical `personal` partition. Four deterministic period aggregate documents provide constant-cost reads for the Android screen, while existing immutable XP events retain the audit trail. A 03:00 Europe/Warsaw timer writes target snapshots for currently open periods from the append-only repository schedule; therefore, a changed target can contribute correctly to an open period without rewriting a closed period.

## Technical decisions

- Maintain one mutable signed-XP aggregate document for every business day, week, month, and year, alongside the existing immutable `xp-event` audit history and `xp-total` projection.
- Update the delivery receipt, task state, immutable XP event where applicable, `xp-total`, and the four affected period aggregates in the existing single-partition Cosmos DB transactional batch. An XP revocation applies its negative amount to the periods containing the event's business time.
- Read selected-period earned XP by point-reading its aggregate document. A missing aggregate for an eligible period represents a signed total of 0 XP; the API must clamp the visualised earned amount to 0 XP when the signed total is negative.
- At 03:00 Europe/Warsaw every day, run an automatic target reconciler. It recalculates and persists the target of every currently open day, week, month, and year aggregate from the versioned repository configuration; it never changes a closed period's target or any signed XP total.
- Configure daily targets as an append-only, effective-dated sequence in repository configuration. Each item has `effectiveFrom` (`yyyy-MM-dd`, interpreted as a business date) and a positive whole-number `dailyTargetXp`; dates must be unique and strictly increasing. The initial item is `2026-09-17` / `100` XP, and the final item remains effective until another item is appended.
- Expose one anonymous, no-store progress endpoint under the existing `TaskXp:ReadRouteSegment`: `GET /api/task-xp/{segment}/progress?period={day|week|month|year}&periodStart=yyyy-MM-dd`. It returns `period`, `periodStart`, exclusive `periodEndExclusive`, `earnedXp`, `targetXp`, and `previousPeriodStart` (or `null`).

## Alternatives considered

- Query and sum immutable `xp-event` documents for every progress-screen request was rejected. It would make read cost and latency grow with history even though the required result is a single aggregate.
- Reusing only the existing all-time `xp-total` document was rejected. It cannot represent a historical day, week, month, or year.
- Fixing a period target permanently when its aggregate is first created was rejected. A daily-target change partway through an open week, month, or year would produce an incorrect full-period target.
- A manual Android recalculation action was rejected. Correct targets must not depend on a user opening a view or pressing a maintenance control; the scheduled reconciler handles this automatically.

## Architecture and data flow

The existing `progression` container remains the sole persistence boundary. In addition to the existing `delivery-receipt`, `task-state`, `xp-event`, and `xp-total` document types, it will contain `xp-progress` documents. Their IDs are deterministic business-period keys, for example `xp-progress:day:2026-09-17`, `xp-progress:week:2026-09-14`, `xp-progress:month:2026-09`, and `xp-progress:year:2026`. Each stores the fixed `personal` partition key, its period kind and start date, and its signed XP total.

When Task XP records an award or revocation, it derives the event's Europe/Warsaw business day using the 03:00 boundary, then derives the containing Monday-starting week, month, and year. The repository point-reads the four deterministic aggregate documents and includes their create-or-replace operations in the same transactional batch as the existing Task XP state transition. ETag-conflict retry uses the existing retry approach. This preserves consistency between audit history, the all-time total, and period totals without scanning XP-event history when the Android application opens a progress screen.

The selected-period API will point-read exactly one aggregate. It returns a zero signed total for an eligible period whose aggregate does not exist, and exposes only the non-negative earned value required by the visualisation. Each existing aggregate also stores its reconciled `targetXp`; the reconciler derives that value from the configured daily-target schedule for every business day in the period.

`ReconcileXpProgressTargetsFunction` will use the existing Azure Functions timer-trigger pattern with a 03:00 schedule in the Function App's Europe/Warsaw timezone. It derives the current business day and its open week, month, and year, calculates each complete period target from the configured effective-dated daily targets, and upserts the corresponding aggregate documents. The operation is idempotent: a repeated timer execution produces the same target values and does not modify signed XP. The normal Android refresh remains read-only and does not trigger reconciliation.

The reconciler creates target-bearing zero-XP aggregates even when no XP event occurred. On its first successful run, it backfills day, week, month, and year documents from the first configured effective date through the current business day; subsequent runs maintain the current day and its open parent periods. This guarantees that every selectable period can show its target and 0 XP without turning an Android read into a write.

The schedule is bound as `TaskXp:DailyTargets` from the versioned deployment configuration and the local-settings template. An appended target must be deployed before 03:00 Europe/Warsaw on its `effectiveFrom` date. The reconciler then computes each open period's `targetXp` by summing the schedule value for every business date in that complete period. Target entries are retained in the repository after they cease being current; the application validates the sequence's shape and ordering, while code review preserves its append-only history.

The Android client requests `progress` with an explicit normalized business-period start. For a week it is Monday, for a month the first calendar day, and for a year 1 January. The Function validates the period type and date, rejects a non-normalized or entirely pre-effective selection, and returns `400` for invalid input or `404` for an unavailable period. The payload is intentionally an aggregate only: it never exposes raw XP events or the target schedule. `previousPeriodStart` is `null` exactly when the preceding same-type period would end on or before the first effective business date, allowing the Android screen to disable its earlier-period navigation.

The Android application adds an `xp-progress` API module and a `XpProgressPage`, selected by a fourth, rightmost `Postęp XP` tab in the existing local `AppView` navigation. It loads on screen entry and on explicit refresh, uses the existing loading and error state treatments, and stores neither responses nor derived progress in local storage. The page offers day, week, month, and year selectors; navigation uses the returned previous start. It computes the display percentage from `earnedXp / targetXp`, renders a simple CSS progress bar, and uses the existing app styling with a success colour and trophy when earned XP exceeds the target.

## Security and operational considerations

- The new read endpoint reuses the existing unguessable Task XP route segment and `Cache-Control: no-store`; it introduces neither an Android secret nor a new authentication model.
- The target reconciler is a timer-triggered internal operation. It must log the business date, affected period keys, operation outcome, and Cosmos request charge without logging route segments, configuration values, or any secrets.
- The repository configuration and deployment workflow must set a 03:00 Europe/Warsaw timer schedule and flatten every `TaskXp:DailyTargets` item into Function App settings. The Android deployment profile receives only the public progress endpoint URL.

## Implementation plan

1. Extend `TaskXpOptions` with validated effective-dated daily-target records and a progress-reconciliation timer schedule. Update the local-settings template, versioned deployment configuration, and deployment workflow to configure the initial `2026-09-17` / `100` target and to flatten the target list into Function App settings.
2. Add a focused Europe/Warsaw business-period calculator using the 03:00 boundary. It maps an event time to its business day and normalizes day, Monday-based week, month, and year starts and exclusive ends.
3. Extend the Cosmos Task XP document model with deterministic `xp-progress` aggregates containing period kind, start, exclusive end, signed XP, and persisted target XP. Extend the existing transactional write path so award and revoke events atomically update the four corresponding aggregates, with the repository's existing ETag retry handling.
4. Implement an idempotent target-reconciliation service and `ReconcileXpProgressTargetsFunction` timer at 03:00 Europe/Warsaw. On first successful execution, backfill zero-XP aggregates from the first effective target date through the current business day; afterward, reconcile the current day, week, month, and year targets from the full effective-dated schedule.
5. Add `GET /api/task-xp/{segment}/progress`. Validate the requested normalized period, enforce the effective-date navigation boundary, point-read its aggregate, and return the approved aggregate contract with no-store headers and safe error responses.
6. Add `NotionManagementApp/src/api/xpProgress.ts` and `XpProgressPage.tsx`. Add `progress` to `AppView`, place `Postęp XP` as the rightmost navigation tab, implement period selection, earlier-period navigation, initial load, manual refresh, loading/error/empty states, and the progress-bar overflow/trophy treatment.
7. Add `VITE_XP_PROGRESS_API_URL` to the local Android profile templates, development configuration, Android build workflow, and application documentation. Keep the URL public and do not add a token or Function key.
8. Update repository documentation with the progress endpoint, target-schedule format, required deployment timing, and timer behaviour. Build both projects, inspect the full diff, and update this feature document with implementation status and actual validation.

## Verification plan

- Build the Function App with `dotnet build NotionManagement.sln` and the Android application with `npm run build` from `NotionManagementApp`.
- Verify business-period calculation at 03:00 Europe/Warsaw, including pre-03:00 events, Monday/week boundaries, month/year boundaries, and both daylight-saving-time transitions.
- Verify an award and a revocation update the all-time total plus exactly the matching day, week, month, and year aggregates in one transactional batch. Verify duplicate and stale deliveries do not alter any aggregate.
- Verify the first reconciliation backfills zero-XP aggregates from 2026-09-17, repeated runs are idempotent, and a mixed target schedule produces the correct open week, month, and year targets without changing closed documents.
- Verify the progress endpoint's default and historical day/week/month/year responses, an empty eligible period, a negative signed period total clamped to 0, exact target completion, target overflow, invalid periods, and the pre-effective navigation boundary.
- Verify the Android screen's automatic load, manual refresh, period selection, earlier navigation disablement, loading/error treatment, zero-XP state, and over-target trophy/success appearance. Confirm no progress values are placed in local storage.

## Implementation notes

- Added Europe/Warsaw 03:00 business-period calculation and effective-dated target validation.
- Award and revoke writes now update deterministic day, week, month, and year aggregates in the existing Cosmos transactional batch.
- Added the scheduled target reconciler, aggregate progress endpoint, Android progress screen, deployment settings, and documentation.
- The progress endpoint returns 0 earned XP and calculates the complete selected-period target from the configured effective-dated schedule when an eligible aggregate document does not exist.
- Fixed period switching so a previously selected day start is not sent as the start of a week, month, or year request.

## Validation performed

- `dotnet build NotionManagement.sln` completed successfully with 0 warnings and 0 errors.
- `npm run build` in `NotionManagementApp` completed successfully.
- `npm run build` in `NotionManagementApp` completed successfully after the post-deployment period-switching fix.
- Inspected the complete working-tree diff and ran `git diff --check` successfully.
- During the 2026-09-17 feature review, `dotnet build NotionManagement.sln` completed successfully with 0 warnings and 0 errors.
- During the 2026-09-17 feature review, `npm run build` in `NotionManagementApp` completed successfully. The first sandboxed attempt was blocked by an `EPERM` error while Vite cleaned the existing `dist/assets` directory; the same build succeeded outside the sandbox.
- The review compared the feature branch with the available local `main` using `git diff main...HEAD`. The local branch was not fetched, so its freshness relative to the remote was not verified.

## Review findings

- Review date: 2026-09-17
- Comparison base: `main...HEAD`
- Resolved: Historical refresh now retains the selected `periodStart`; a failed refresh keeps the last successfully displayed historical result visible with its error message.
- Accepted: The V1 progress fill remains capped at 100%. When XP exceeds the target, the success colour, trophy, and exact XP counter are the approved visual indication; an extended bar is not required.
- No blocking or important findings remain.
- Readiness: accepted by the user after confirming the deployed primary flow. Extended manual acceptance remains pending and does not block completion.

## Manual acceptance checklist

- [x] Open the rightmost `Postęp XP` tab and confirm the current business day loads by default.
- [x] Switch between day, week, month, and year and confirm each result shows the normalized period and its complete-period target.
- [ ] Navigate to an earlier period, press refresh, and confirm the same historical period remains selected with freshly retrieved data.
- [ ] Confirm earlier-period navigation becomes disabled at the effective-date boundary and an overlapping week, month, or year includes targets only from 2026-09-17 onward.
- [ ] Confirm an eligible period with no XP events displays 0 XP rather than an error.
- [ ] Confirm XP before 03:00 Europe/Warsaw is attributed to the preceding business day, including around both daylight-saving-time transitions.
- [ ] Confirm values below, exactly at, and above the target render correctly; above-target progress uses the success colour, trophy, and exact XP counter.
- [ ] Confirm a negative signed period total is displayed as 0 XP.
- [ ] Confirm initial loading, manual-refresh loading, and retrieval failures use the existing treatments and do not show stale progress as current data.
- [ ] Confirm progress responses and derived progress values are not stored in Android local storage.
- [ ] Confirm an award and revocation update the all-time total and the matching day, week, month, and year aggregates once; duplicate and stale deliveries must not alter aggregates.
- [ ] Confirm the first target reconciliation backfills zero-XP periods, repeated reconciliation is idempotent, and a mixed effective-dated schedule updates only open-period targets.

## Open questions

None.

## Decisions log

| Date | Decision | Reason |
|---|---|---|
| 2026-09-17 | Start business discovery for XP Progress Visualization. | The user wants a simple Android V1 view of XP earned over day, week, month, and year, with a 02:00 Europe/Warsaw business-day boundary. |
| 2026-09-17 | Use one repository-configured daily XP target in V1. | The user does not need in-app target configuration at this stage. |
| 2026-09-17 | Derive week, month, and year targets by summing daily targets. | Avoid independent period targets in V1. |
| 2026-09-17 | Start a business week on Monday at 02:00 Europe/Warsaw time. | User decision. |
| 2026-09-17 | Include year as a fourth selectable progress period. | User decision. |
| 2026-09-17 | Start business months and years at the 02:00 Europe/Warsaw boundary on the first calendar day and 1 January respectively. | Keep every displayed period consistent with the business-day boundary. |
| 2026-09-17 | Show progress against the full target of the selected period. | The user wants a seven-day week to use a 700 XP target at 100 XP per day. |
| 2026-09-17 | Allow navigation to earlier periods of each type. | The user wants historical visualisation. |
| 2026-09-17 | Exclude comparisons between periods. | V1 is a visualisation of one selected period only. |
| 2026-09-17 | Preserve the target applicable to historical periods. | The user does not want later configuration changes to recalculate past progress. |
| 2026-09-17 | Apply changed daily targets from the next business-day boundary. | Avoid a target changing within an already started business day. |
| 2026-09-17 | Visually extend progress beyond 100% and celebrate it with a success colour and trophy icon. | User decision. |
| 2026-09-17 | Clamp negative signed period XP to 0 in the visualisation. | The progress view must not show a negative earned XP value. |
| 2026-09-17 | Restrict daily targets to positive whole XP values. | User decision. |
| 2026-09-17 | Keep the initial 100 XP daily target in effect until configuration changes it. | The user does not want it to expire after the upcoming week. |
| 2026-09-17 | Do not persist progress data locally in Android V1. | User decision. |
| 2026-09-17 | Use the Android application's existing loading and error treatments for progress retrieval. | User decision. |
| 2026-09-17 | Place XP progress on a dedicated screen in the right-side Android tab. | User decision. |
| 2026-09-17 | Retrieve progress on screen entry and allow manual refresh. | User accepted the recommendation. |
| 2026-09-17 | Configure an effective date for the daily target and prevent selection of earlier periods. | User decision; it limits historical progress to the configured target period. |
| 2026-09-17 | Allow periods that overlap the effective date and calculate their target only from that date. | User accepted the recommendation. |
| 2026-09-17 | Make the initial 100 XP daily target effective from 2026-09-17. | User decision. |
| 2026-09-17 | Show 0 XP rather than an error for an available period with no XP events. | User accepted the recommendation. |
| 2026-09-17 | Complete business discovery. | The confirmed scope has no remaining open questions. |
| 2026-09-17 | Use 03:00 Europe/Warsaw as the business-day boundary. | User changed the original 02:00 requirement. 03:00 occurs once on both daylight-saving-time transition days, so no special DST boundary rule is needed. |
| 2026-09-17 | Store mutable XP aggregates for each day, week, month, and year. | User accepted point-read period aggregates over queries that sum the growing immutable XP-event history. |
| 2026-09-17 | Recalculate targets for open XP periods automatically at 03:00 Europe/Warsaw. | User accepted automatic reconciliation so mixed daily targets within a week, month, or year are summed correctly without a manual Android action. |
| 2026-09-17 | Store daily targets as an append-only effective-dated repository configuration sequence. | User accepted retained target history so reconciliation can sum the daily values that applied within an open period. |
| 2026-09-17 | Use a single aggregate progress endpoint with normalized selected periods. | User accepted the `progress` contract, including period start/end, earned and target XP, and server-provided earlier-period navigation. |
| 2026-09-17 | Complete technical design. | The user confirmed that the technical design is complete and implementation will continue in a separate chat. |
| 2026-09-17 | Complete feature review with changes required. | Two important Android UI findings remain unresolved: historical refresh loses the selected period, and over-target progress is visually capped at 100%. |
| 2026-09-17 | Accept capped V1 progress fill for results above target. | The success colour, trophy, and exact XP counter are sufficient; visual bar extension is not required. |
| 2026-09-17 | Resolve historical-period refresh finding. | The selected normalized period start is retained for refreshes and after failed requests. |
| 2026-09-17 | Mark feature complete with extended acceptance pending. | User confirmed the deployed primary flow; remaining manual checks will be completed later. |
