# Feature: Today task status menu and effort display

## Status

Implementation complete

## Goal

Keep every status option accessible when changing a task status on the Today screen, including for tasks near the bottom of the Android viewport, and make a task's effort visible on that screen.

## Problem statement

The status menu is always positioned below its task. Because it is absolutely positioned, it does not extend the scrollable document, and the list container can clip it. For tasks low in the viewport, some or all status options can therefore be unreachable.

## Initial idea

Open the dropdown below the task by default and open it above the task when there is not enough room below. Also show each regular task's Notion `Effort` value in the Today list.

## Business requirements

- The status menu opens below its task when the complete menu fits in the visible viewport.
- The status menu opens above its task when it does not fit below but fits above.
- All status options remain reachable on constrained viewport heights.
- Existing status selection and persistence behavior remains unchanged.
- The Today list displays the effort selected for each regular Notion task.

## User scenarios

- A user opens the status menu for a task near the top or middle of the Today list and sees it below the task.
- A user opens the status menu for a task near the bottom of the viewport and sees it above the task.
- A user uses a viewport too short for the complete menu on either side and scrolls within the menu to reach every status.
- A user scans the Today list and can identify each task's effort without opening Notion.

## Business rules

- Below is the preferred placement.
- When neither side can contain the complete menu, use the side with more space and constrain the menu height.

## Edge cases

- The list contains only one task or the selected task is the final task.
- The viewport changes size or orientation while the menu is open.
- The user scrolls while the menu is open.
- The configured status list is taller than the available viewport space.

## Out of scope

- Changes to available statuses.
- Changes to other menus or dialogs.
- A broader redesign of the Today screen.

## Technical design

The adaptive dropdown implementation is complete. For effort display, the Function App will include the existing Notion `Effort` select value in each Today Tasks response item. The React API model will accept a nullable effort value, and the Today task's secondary line will render `<project> · <effort>`. A missing or whitespace-only effort will render as `Brak effortu`; the existing XP fallback to `Medium` remains an internal XP rule and is not displayed as a selected Notion value.

Allow the Today list's menu to render outside the list boundary. Constrain an oversized menu to the selected side's available height and enable internal vertical scrolling. Recalculate while the viewport is resized or the page is scrolled.

## Technical decisions

- Extend `TodayTask` in the Function App response with nullable `Effort`, populated from the existing Notion select-property reader.
- Extend the frontend `TodayTask` contract with `effort: string | null`, retain strict validation for supplied values, and accept an omitted field as `null` for safe staged deployment.
- Render effort after the project text in the existing secondary line; use `Brak effortu` for null, empty, or whitespace-only values.
- Existing adaptive-dropdown decisions remain implemented.

## Alternatives considered

- Existing adaptive-dropdown alternatives remain recorded in the implementation history.

## Architecture and data flow

The adaptive-dropdown change is local to `TodayTasksPage` and shared application styles. The effort-display data flow is Notion `Effort` select property → `NotionTodayTask` → `TodayTask` HTTP response → frontend `TodayTask` parser → Today task secondary line. A client receiving a legacy API response without `effort` treats it as a null value.

## Security and operational considerations

No authentication, authorization, data storage, logging, or operational behavior changes are expected. The API response contract will be extended for the Android client.

## Implementation plan

1. Include nullable Notion effort in the Function App's Today Tasks model and response projection.
2. Extend the frontend Today Tasks response parser to validate the nullable field.
3. Render effort in the existing project secondary line with the confirmed missing-value label.
4. Build the Function App and frontend, inspect the complete diff, and update validation results.

## Verification plan

- Verify an effort selected in Notion appears after the project name in the Today list.
- Verify a task without an effort displays `Brak effortu` rather than the XP fallback.
- Verify projectless tasks, status changes, and archiving retain their existing behavior.
- Run the Function App and frontend builds and inspect the final diff.

## Implementation notes

- Added runtime measurement of the open status menu against the visual viewport, with `window.innerHeight` as a compatibility fallback.
- Added placement state that prefers below, flips above when necessary, and uses the roomier side when neither side fits the complete menu.
- Added a calculated maximum height and internal scrolling for constrained menus.
- Recalculate placement on page scroll, visual-viewport scroll or resize, and window resize.
- Limited visible list overflow to the Today task list so the menu is no longer clipped without changing other lists.
- Added `aria-haspopup` and `aria-expanded` to expose the status button's menu state.
- Extended the Today Tasks response with nullable Notion effort and rendered it after the project name.
- Made the frontend accept legacy responses that omit `effort`, rendering the confirmed missing-value label.

## Validation performed

- Ran `dotnet build NotionManagementFunctionApp/NotionManagementFunctionApp.csproj --no-restore` on 2026-09-26. It completed successfully with 0 warnings and 0 errors.
- Ran `npm run build` in `NotionManagementApp` on 2026-09-26. The routine-task configuration validator, `tsc --noEmit`, and the Vite production build completed successfully.
- Ran `git diff --check`; no whitespace errors were reported.
- Inspected the complete implementation diff. It contains only the Today Tasks API response, frontend parsing and rendering, adaptive-dropdown behavior, and feature documentation changes.
- Physical Android viewport and live-Notion verification remain to be performed on a device or emulator.

## Review findings

## Manual acceptance checklist

## Open questions

None.

## Decisions log

| Date | Decision | Reason |
|---|---|---|
| 2026-09-26 | Prefer placement below and fall back above when the menu does not fit. | Preserve the current interaction while keeping low-list options visible. |
| 2026-09-26 | Use the side with more room and internal scrolling if neither side fits. | Ensure every status remains reachable on constrained viewports. |
| 2026-09-26 | Keep the implementation dependency-free and local to the frontend. | The problem is a small layout concern and does not require API or native Android changes. |
| 2026-09-26 | Mark business discovery and technical design complete and implement the confirmed behavior. | The requested placement rule and constrained-viewport fallback are confirmed, and no material questions remain. |
| 2026-09-26 | Complete implementation. | Adaptive placement, overflow handling, build validation, and implementation documentation match the agreed scope. |
| 2026-09-26 | Extend this feature with display of the regular task's Notion `Effort` value on the Today screen. | The user wants effort visible while reviewing today's tasks. |
| 2026-09-26 | Display effort in the existing project secondary line and show `Brak effortu` for a missing value. | Keep the list compact and distinguish the selected Notion value from the XP fallback. |
| 2026-09-26 | Use the existing Notion effort reader and extend the Today Tasks API response. | The data is already retrieved for Task XP, so no new integration or persistence is needed. |
| 2026-09-26 | Treat an omitted effort field from a legacy Today Tasks API response as null. | The new Android client remains usable during a staged backend and APK rollout. |
| 2026-09-26 | Complete the effort-display scope extension. | The API response, client parsing, UI rendering, compatibility handling, and builds match the confirmed behavior. |
