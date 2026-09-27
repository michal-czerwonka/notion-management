# Feature: XP Chart

## Status

Business discovery, technical design, and implementation complete; manual acceptance pending

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

The existing `xp-progress:day:<yyyy-MM-dd>` Cosmos documents are the chart's data source. They already receive Task XP and routine XP changes through the same write paths as XP Progress. The chart reads their signed totals and applies the same non-negative display rule as the progress endpoint. It does not read or return completed-task snapshots or the append-only XP event history.

Use one read-only `GET /api/task-xp/{segment}/chart?days=14|30|90` request. The Function computes the current business date in Europe/Warsaw with the existing 03:00 boundary, derives the inclusive start date, and queries day aggregates in that date range within the `personal` partition. It returns an ordered entry for every requested date, filling absent records with 0, plus the configured `chartMaxXp`. Query and mapping failures return an error; they never become a successful all-zero response. The client clips bar height only, retaining the unclipped non-negative daily XP in the response.

Response shape: `{ startDate, endDate, chartMaxXp, days: [{ date, earnedXp }] }`, where `endDate` is the inclusive current business date. The server validates `days` against the three allowed values. Responses use the existing unguessable Task XP route segment and `Cache-Control: no-store`. The Android API client validates the count, ascending consecutive dates, whole non-negative XP values, and positive chart maximum before showing the chart.

Add `TaskXp:ChartMaxXp` as a positive integer in the same versioned `deployment/config/development.json` Function App configuration section as `taskXpDailyTargets`, initially 300. Bind and validate it in `TaskXpOptions`, mirror it in the local settings example, and deploy it through the existing Function App settings workflow. Returning the maximum in each chart response avoids a second configuration source in the Android build.

The Android page starts at 14 days each time the tab is entered and uses the existing `CacheHeader` and `useCachedResource` loading, refresh, timestamp, and error states. Give each 14/30/90-day range its own cache key and validate its response in `screenCache`. Use the existing short reuse window on tab entry, while manual refresh forces a request. Extend XP invalidation after a local task or routine change to include chart entries containing the current business day. A failed refresh preserves the last successful chart for that range and displays the error. A first-load failure shows the error without rendering invented zero values. Changing ranges shows only data for the selected range.

Render a fixed-height plot with one width-equal rectangular bar per calendar slot and zero horizontal gaps. A zero value occupies its slot without fill. Label the XP axis from 0 to the configured maximum with fixed ticks derived from the returned maximum, and label the time axis sufficiently to orient the selected range. Clamp the visual height to the maximum. Allow horizontal scrolling when the selected range cannot fit at a readable bar width, especially for 90 days on a narrow phone; keep the labelled XP scale visible while scrolling the time axis and bars.

## Technical decisions

- Allow horizontal scrolling for the 90-day plot on a narrow phone so individual daily bars remain readable. The user confirmed this layout choice.
- Use a dedicated range endpoint that returns the selected daily values and configured chart maximum in one response. The user approved this contract and leaving the existing progress endpoint unchanged.

## Alternatives considered

- Calling the existing daily progress endpoint once per bar would require 14-90 network requests and include completed-task history that the chart does not use. A dedicated range read is recommended.
- Reading XP events and reconstructing each day's total would duplicate existing aggregation logic and make read cost grow with event history. Reuse the day aggregates.
- A fixed chart maximum embedded in Android build configuration would drift from the daily-target configuration. Return the server-configured value with the chart data.
- A chart-specific local state implementation would duplicate the established cache and error handling. Extend the existing cache for the new response type.

## Architecture and data flow

Task and routine writes -> existing transactional day XP aggregates in Cosmos `progression` -> range query in `TaskXpRepository` -> `TaskXpService` -> new HTTP Function route -> Android chart API module -> cached `XpChartPage` -> bars and labelled axes. The Function uses the existing `BusinessPeriodCalculator`; the client uses the business date passed by `App` to key cache entries and react to the 03:00 boundary.

The chart is read-only and needs no new Cosmos documents, migration, target reconciliation, or timer. Pre-history dates without aggregate records are filled with 0. The existing `/progress` contract remains unchanged.

## Security and operational considerations

- Reuse the existing route-segment authorization check and no-store response helper. No new secret or permission is required.
- Project only the day date and signed XP from Cosmos, with a partition-key and date-range filter. Paginate until the complete bounded range is read; do not retrieve completed-task snapshots.
- Reject unsupported ranges and invalid configuration. Log query failures and Cosmos request charge without logging the route segment.
- Keep existing XP Progress behaviour intact. The new endpoint is additive; the Android build needs a chart URL in its local templates and development build workflow.

## Implementation plan

Implementation sequence:

1. Add and validate `TaskXp:ChartMaxXp = 300` in Task XP options, local settings example, versioned deployment configuration, and Function App settings workflow.
2. Add a bounded daily-aggregate range read to the existing repository. Compute business-day dates with `BusinessPeriodCalculator`, project only dates and signed totals, fill missing dates, apply the XP Progress non-negative display rule, and return the maximum without clipping source values.
3. Expose and validate the chart HTTP route under the existing Task XP route segment. Keep the progress endpoint contract unchanged and use the existing no-store and safe-error conventions.
4. Add the chart API URL to Android environment examples, development configuration, and Android build workflow. Add a typed API client with response validation.
5. Extend screen-cache validation and XP invalidation for chart range keys. Add a `Diagram XP` page using the existing cache header and refresh/error behaviour.
6. Arrange the main navigation in the two confirmed rows and render the daily bars, XP scale, and time labels, with horizontal scrolling for ranges too wide for a phone screen.
7. Update repository API/configuration documentation. Build the Function App and Android app, inspect the full diff, and record actual validation in this document during implementation.

## Verification plan

- Build `NotionManagement.sln` and run `npm run build` from `NotionManagementApp` during implementation.
- Check 14, 30, and 90 consecutive dates including today, across month/year and both Warsaw daylight-saving transitions, including requests before and after 03:00.
- Compare chart values with `GET /progress?period=day` for days with task XP, routine XP, corrections/revocations, zero XP, and a negative signed aggregate. Confirm missing pre-history records become 0 only on a successful read.
- Confirm unsupported ranges fail validation, Cosmos failures surface as errors, and a failed refresh retains the last successfully displayed chart.
- Check exact heights for 0, a positive value, the configured maximum, and a value above it; adjacent equal-height positive bars touch without gaps. Check the labelled fixed scale and the agreed 90-day phone layout.
- Verify local XP changes invalidate chart and progress caches, and a new business day shifts the displayed range. Check the deployed configuration supplies the same maximum that the Function returns.

## Implementation notes

- Added the authenticated-by-route chart read under the existing Task XP segment. It reads bounded day aggregates, reports query charges, rejects malformed aggregates, fills missing dates only after a successful read, and applies the same non-negative display rule as XP Progress.
- Added `TaskXp:ChartMaxXp` to versioned development configuration, deployment app settings, and the local settings example. The Android API receives the configured maximum with each chart response and clips only bar height.
- Added independent 14-, 30-, and 90-day cached views, response and persisted-cache validation, XP invalidation, manual refresh, and retained successful data on refresh errors. The navigation now has the agreed two rows.
- No data migration or new dependency was required. The existing progress endpoint remains unchanged.

## Validation performed

- `dotnet build NotionManagement.sln` passed with 0 warnings and 0 errors.
- `npm run build -- --mode development-phone --outDir <writable build directory>` passed, including the existing routine validation, TypeScript check, and Vite bundle. The standard `dist` path was inaccessible in this sandbox, so the same build ran with an alternate output directory.
- `git diff --check` passed. Reviewed the full change set against the confirmed requirements and implementation plan.
- Live Cosmos/API comparisons, daylight-saving boundary requests, and on-device visual/manual acceptance checks have not been run in this environment.

## Review findings

## Manual acceptance checklist

## Open questions

None at present.

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
| 2026-09-27 | Allow horizontal scrolling for a 90-day chart on a narrow phone. | The user chose readable daily bars over fitting all 90 days within one viewport. |
| 2026-09-27 | Use one dedicated chart range endpoint, returning the configured scale maximum with daily values. | The user approved one request per selected range without changing XP Progress. |
| 2026-09-27 | Complete technical design. | The user confirmed there are no remaining open questions and approved closing this stage. |
