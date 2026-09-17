# Feature: XP Progress Visualization

## Status

Business discovery complete; technical design not started

## Goal

Make earned XP immediately understandable by showing progress over relevant time periods in the Android application.

## Problem statement

Task XP records individual awards and revocations and exposes the current total, but it does not show whether the user's recent XP activity meets their intended daily pace.

## Initial idea

Show a simple V1 progress bar in the Android application for XP earned in the current day. The user should be able to switch the view to week and month, and should also be able to see XP earned since the beginning of the current year. The initial desired target is 100 XP per day for the upcoming week. A business day starts and ends at 02:00 in the Europe/Warsaw time zone.

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
- A business day starts and ends at 02:00 in the Europe/Warsaw time zone.
- In V1, the daily target is a single value configured in a repository file; it does not need to be configurable through the application.
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

- XP belongs to the business day, week, month, and year containing its actual XP-event time when interpreted in the Europe/Warsaw time zone with the 02:00 day boundary.
- For a selected period, progress is earned XP divided by the sum of that period's daily targets.
- The target shown by a progress bar covers the complete selected period, not only its elapsed days. For example, a seven-day business week has a 700 XP target when the daily target is 100 XP.
- A completed historical period retains the daily target that applied when that period occurred; later target changes must not recalculate its displayed progress.
- A changed daily target takes effect at the start of the next business day, 02:00 Europe/Warsaw time. A period target is the sum of the daily targets effective on its individual business days.
- When earned XP exceeds the selected period's target, the progress bar may visually extend beyond 100%, uses a success-associated colour, and shows a trophy icon next to the XP counter.
- If the signed XP total for the selected period is negative, the visualised earned XP is shown as 0 XP.
- A business week starts on Monday at 02:00 Europe/Warsaw time and ends immediately before the following Monday at 02:00.
- A business month starts at 02:00 Europe/Warsaw time on the first calendar day of the month.
- A business year starts at 02:00 Europe/Warsaw time on 1 January.

## Edge cases

- A completion occurs between midnight and 01:59 Europe/Warsaw time and belongs to the preceding business day.
- A target is not reached, reached exactly, or exceeded.
- Earned XP exceeds the period target.
- A reopening revokes enough XP for the selected period's signed total to become negative.
- An available period contains no XP events.
- A period crosses a calendar boundary or a daylight-saving-time change.

## Out of scope

- Advanced dashboard visuals and complex screen design beyond a simple V1 progress view.
- Comparing periods with one another.

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
- What initial effective date should be configured for the 100 XP daily target?

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
