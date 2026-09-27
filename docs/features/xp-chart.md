# Feature: XP Chart

## Status

Business discovery complete; technical design not started

## Goal

Help the user assess activity consistency by viewing earned XP for individual days over time.

## Problem statement

The existing XP progress feature shows progress for a selected period. The user needs a view of individual days together to assess activity consistency.

## Initial idea

Add a `Diagram XP` tab next to `Postęp XP`, showing XP for individual days over the last 14, 30, or 90 days. Place both XP tabs below `Dzisiaj`, `Rutyny`, and `Inbox`.

## Business requirements

- Add a new tab named `Diagram XP` next to `Postęp XP`.
- Arrange navigation in two rows: `Dzisiaj`, `Rutyny`, `Inbox` in the top row, and `Postęp XP`, `Diagram XP` in the bottom row, in that order.
- Display a chart of XP for individual days.
- Use one rectangular bar per day, with adjacent bars touching side to side and no gaps. Equal positive daily values across the selected range must form one solid rectangle (for example, 100 XP every day).
- Show a labelled XP scale so the user can estimate daily values visually.
- Read the chart's maximum XP value from the same configuration file that defines the daily XP target. The initial chart maximum is 300 XP.
- Use a fixed XP scale from 0 to the configured maximum; do not automatically scale it to the selected period's values.
- Offer the last 14, 30, and 90 days as selectable ranges.
- Default to the 14-day range when opening `Diagram XP`.
- The selected range determines how many days back the chart displays.
- Retrieve chart data when the tab is opened and allow manual refresh, consistently with XP Progress.
- Use the same loading and error treatment as XP Progress. A retrieval failure must show an error rather than replace the chart with zero-XP values; a failed refresh retains the last successfully displayed chart with an error message.

## User scenarios

- The user opens `Diagram XP` and selects 14, 30, or 90 days to see XP for individual days in that range.

## Business rules

- Each range includes the current day: 14 days means today and the previous 13 days, 30 days means today and the previous 29 days, and 90 days means today and the previous 89 days.
- A business day runs from 03:00 to 03:00 in the Europe/Warsaw time zone, consistently with XP Progress. Today means the current business day; activity before 03:00 belongs to the preceding business day.
- Daily XP must equal the value displayed by XP Progress for the same business day, including the same XP sources, corrections, and display rules.
- Days with 0 XP retain a full daily slot on the time axis with a zero-height bar, leaving a visible gap in the filled chart rather than being omitted.
- Dates without XP data, including dates before available XP history, display 0 XP as if nothing had been completed on those days. Preserve the full selected range; do not distinguish these dates with a no-data label.
- The zero-XP fallback applies to dates without records in successfully retrieved data, not to retrieval failures.
- Cap each bar's displayed height at the configured chart maximum. With a 300 XP maximum, days with 300 XP and 700 XP must look identical. This is a presentation limit only; the underlying daily XP remains consistent with XP Progress.

## Edge cases

- A day's XP exceeds the configured chart maximum: render the same bar height as a day exactly at the maximum, without a distinct overflow appearance.

## Out of scope

- Tap interactions or tooltips showing the exact date and XP of an individual bar in this version.
- A special label or visual marker indicating that the current day is still in progress.

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
| 2026-09-27 | Start business discovery for XP Chart. | The user requested a new daily XP chart feature. |
| 2026-09-27 | Make assessing activity consistency the primary goal of the chart. | The user explicitly confirmed this goal. |
| 2026-09-27 | Add a `Diagram XP` tab next to `Postęp XP` with selectable 14-, 30-, and 90-day ranges. | Explicit initial user requirements. |
| 2026-09-27 | Use two navigation rows: `Dzisiaj`, `Rutyny`, `Inbox` above `Postęp XP`, `Diagram XP`. | The user confirmed the exact arrangement and order. |
| 2026-09-27 | Include today and the preceding days in every range. | The user explicitly chose to include the current day. |
| 2026-09-27 | Always use the 03:00 Europe/Warsaw business-day boundary. | The user confirmed consistency with XP Progress. |
| 2026-09-27 | Use the same daily XP values, sources, corrections, and display rules as XP Progress. | The user accepted the recommendation to keep both views consistent. |
| 2026-09-27 | Default to the 14-day range when opening the chart. | Explicit user decision. |
| 2026-09-27 | Use daily bars touching side to side without gaps. | The user wants equal daily XP values to form a solid rectangle. |
| 2026-09-27 | Retain zero-XP days as empty daily slots on the time axis. | The user accepted the recommendation to make inactivity visible. |
| 2026-09-27 | Display 0 XP for dates without data, including dates before available history. | The user rejected a separate no-data state and wants these dates treated visually as days with no completions. |
| 2026-09-27 | Use the XP scale for approximate visual reading; omit per-bar detail interactions in this version. | The user does not currently need exact values on tap. |
| 2026-09-27 | Use a fixed chart maximum of initially 300 XP, stored in the daily-target configuration file, and cap bar heights at that maximum. | The user rejected automatic scaling and explicitly wants values at and above the maximum to look identical. |
| 2026-09-27 | Retrieve data on tab entry and allow manual refresh, as in XP Progress. | The user confirmed the same refresh behaviour. |
| 2026-09-27 | Reuse XP Progress loading and error behaviour; never interpret retrieval errors as zero XP. | The user accepted the recommendation to distinguish absent records from failed retrieval. |
| 2026-09-27 | Do not specially mark the current day as incomplete. | The user considers it clear that the current day is still in progress. |
| 2026-09-27 | Complete business discovery. | The user approved closing the stage with no unresolved business questions. |
