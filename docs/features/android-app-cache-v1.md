# Feature: Android App Cache V1

## Status

Review fixes implemented; Android manual acceptance pending; not ready

## Goal

Make repeated visits to Android app screens feel immediate while avoiding unnecessary API requests.

## Problem statement

The Android app currently reloads a screen when it is reopened, including after switching tabs or restarting the app. Previously displayed data is not consistently shown while a fresh request is in progress. The Today tasks screen can show its empty state before the initial request completes.

## Initial idea

Keep each screen's previously loaded state available during the same business day. Avoid another API request when the same screen is reopened within one minute of fetching its data, including after an app restart. On a later visit that requires fresh data, show the saved state first and replace it when the request succeeds. Put a subtle data timestamp, a manual refresh control, and a small request-status indicator at the top of every screen. Show a loading state when a new business day has no cached data.

## Business requirements

- Apply the feature to every Android app screen.
- When a screen is reopened within one minute of its last successful data fetch, show the previously loaded state without sending another API request, including after switching tabs or restarting the app.
- When a screen is reopened later on the same day, show its previously loaded state before fresh data arrives, then replace it with the fresh result.
- At the top of each screen, show a subtle `Dane z <date and time>` label and a small refresh icon. Format the date and time as `dd.MM.yyyy HH:mm:ss` in `Europe/Warsaw`, regardless of the device time zone.
- The `Dane z` timestamp is the time of the last successful fetch of the full data shown on that screen. A failed refresh keeps the previous timestamp when previously fetched data remains visible.
- Before the first successful fetch, show `Dane z —` in place of a timestamp. During the request the status indicator is yellow; after a failure it is red with the failure message.
- Activating the refresh icon forces a fresh API request regardless of the one-minute interval.
- When a new day has no usable cached data, show a loading message while the initial request runs. This must also work on the Today tasks screen.
- Do not display data fetched in a previous business day while the first request for the new business day is in progress; show the loading state instead.
- If a data request fails, show an error message such as `Nie udało się pobrać danych`.
- If a refresh fails and data from the current business day is already visible, keep that data visible alongside the error.
- Show a small green indicator when no request is in progress and there is no current request error, a yellow indicator while a request is in progress, and a red indicator when the request fails. Show the failure message next to or beneath the red indicator.
- After a successful user-initiated data change, reset the one-minute freshness interval for affected screen data so a fresh request is allowed immediately.
- After a successful data change, refresh the currently visible affected screen immediately. Refresh other affected screens when they are next opened, without applying their previous one-minute interval.
- On the XP progress screen, each selected period and period start has its own cached result and one-minute freshness interval.
- Restore the last selected XP period and period start when returning to the XP screen during the same business day, including after an app restart.
- On the first opening of XP Progress in a new business day, select and fetch the current business day, even if the last saved selection was a historical period.
- If XP Progress remains open at the 03:00 Warsaw business-day boundary, automatically switch it to `Today` and fetch the new day's data.
- Add a `Today` button on the XP screen that switches the selection to the current business day.
- If the app remains open at the 03:00 business-day boundary, the visible screen automatically hides the previous day's data, shows its loading state, and requests current-day data. Other screens refresh when next opened.
- Expiration of the one-minute freshness interval alone does not trigger an automatic request while a screen remains open. The interval is checked when the user returns to the screen or app; manual refresh, successful data changes, and the 03:00 boundary remain separate refresh triggers.

## User scenarios

- The user loads a screen, closes the app, and reopens that screen within one minute: the earlier state appears and no new API request is made.
- The user switches to another tab and returns within one minute: the earlier state appears and no new API request is made.
- The user returns to a screen later on the same day: the earlier state appears immediately, followed by fresh data when the request succeeds.
- The user taps the refresh icon: the screen requests fresh data immediately.
- The user opens a screen on a new day without usable cached data: a loading message appears until the request completes or fails.
- The user returns to XP Progress after switching tabs or restarting the app during the same business day: the last selected period and start date are restored.
- The user opens XP Progress for the first time in a new business day: the screen selects `Today` and fetches that day's data.
- The user keeps XP Progress open with a historical period selected across 03:00: the screen switches to `Today`, hides the previous day's data, and fetches the new business day.
- The user taps `Today` on XP Progress: the view switches to the current Warsaw business day.

## Business rules

- A completed data request starts the normal one-minute request interval whether it succeeded or failed. Successful user changes invalidate the affected interval.
- All screens use a business day that changes at 03:00 in `Europe/Warsaw`. Same-day cache reuse follows that business-day boundary, including across app restarts.
- Data from a previous business day is not usable as the visible screen state after the 03:00 boundary.
- A failed refresh must not replace still-usable data from the current business day.
- A failed refresh does not advance the displayed data timestamp.
- A retry changes the request indicator from red to yellow while the new request is in progress; a successful retry changes it to green.
- After a failed request, manual refresh remains available immediately as the explicit retry action.
- After a failed request, re-entering the screen or app more than one minute later automatically retries the request; re-entering sooner does not. Manual refresh can retry at any time.
- The one-minute request suppression does not apply to screen data affected by a successful user-initiated change until that data has been refreshed.
- Switching between XP period selections uses the cached data and freshness interval for the specific selection being opened.
- XP selection restoration applies only within the business day in which that selection was last used. The first XP opening on a later business day selects `Today` and makes a fresh request.
- The 03:00 boundary resets a visible XP screen to `Today`, even when a historical period was selected before the boundary.
- `Today` selects the `day` period for the current Warsaw business date; normal cache freshness rules apply to that selection.
- The visible screen's prior-business-day data becomes unusable at the 03:00 boundary even without navigation.
- Passing the one-minute freshness limit while the screen stays open does not change the displayed data or start a request by itself.

## Edge cases

- A request fails while a previous state is visible.
- A request fails when the screen has no usable cached data.
- The user changes a task or Inbox item while a cached screen is visible.
- The user taps refresh repeatedly or moves between screens during an in-flight request.
- The device goes offline or the local day changes while the app remains open.
- The XP screen changes its selected period, which changes the data being displayed.
- The saved XP selection points to a historical period when the user first opens XP Progress on a new business day.
- A historical XP period remains selected when the 03:00 boundary occurs while that screen is visible.

## Out of scope


## Technical design

### Existing implementation

- The Android app is a React/TypeScript UI hosted by Capacitor. `App.tsx` conditionally mounts one of four pages; switching tabs unmounts the previous page. There is no router, shared data store, or persistence layer for screen data.
- Each page currently owns its GET request and loading/error state. Inbox cancels its mount request, Routines rejects older responses with a generation counter, and Today/XP have no equivalent protection. XP changes the requested period in component state; its GET response includes a normalized `periodStart`.
- Inbox and Today mutate their displayed lists locally after successful writes. Routines merges confirmed occurrences by server version. Cache invalidation and response ordering must be handled together.
- The Function App remains authoritative for all screen data. The routine endpoint includes `businessDate`; the other GET responses do not. The XP endpoint accepts a period and optional normalized `periodStart`, and includes the normalized start in its response. API requests already use `cache: 'no-store'`.
- The backend and existing Routines page use the 03:00 `Europe/Warsaw` business-day concept. The client needs one shared calculation for all pages and a boundary trigger that also checks the day when the WebView resumes after suspension.

### Chosen design

- Add a small shared client cache for complete GET results, request metadata, and per-entry invalidation. Keep one entry for Inbox, Today, and Routines, plus entries keyed by XP period and normalized period start. Store the business date, last successful full-fetch instant, last completed attempt instant, and the last request error independently. A failed attempt retains usable data and its successful-fetch timestamp.
- Hydrate the cache before deciding whether to fetch on page entry. Reuse same-business-day data when the last completed attempt is less than one minute old and the entry is not invalidated. A manual refresh and a successful affected mutation bypass that interval. Deduplicate concurrent GETs for the same key; prevent an older response from replacing a newer request or a confirmed write.
- Persist versioned cache entries in app-private IndexedDB so a process restart preserves results and request intervals. XP history and the number of visited periods can grow; IndexedDB needs no new package. An in-memory mirror keeps tab switches immediate. Treat missing, corrupt, or unavailable storage as a cache miss and keep the API usable.
- Use the official Capacitor App plugin's `appStateChange` event when the Android activity becomes active, alongside initial mount and an in-foreground 03:00 timer. The app currently has no lifecycle plugin. The user approved adding `@capacitor/app` in the compatible v8 line.
- Persist the XP selection (`period`, normalized `periodStart`, last-used business date) separately from cached results. On entry, compare its last-used date with the current Warsaw business date: restore the selection only on a match; otherwise select today's `day` period and fetch it. `Today` derives the current Warsaw business date, selects its `day` entry, and follows the normal freshness check during that day; it does not force a request when that entry is still fresh. Period tabs continue to select the current period of their type, while the previous-period control selects an explicit start.
- Use one shared page-header control for `Dane z`, refresh, and the green/yellow/red request indicator with accessible status text. Keep mutation errors separate from GET errors. Show an empty state only after a successful empty GET; before that show loading or the GET error.
- Derive the business date from the device instant in the Warsaw time zone, matching the backend's 03:00 rule. On boundary or resume, hide prior-day entries before rendering, invalidate outstanding prior-day GET responses, and fetch the visible page. If that page is XP Progress, select `Today` before fetching. Use the same formatter for all data timestamps. A device with an incorrect clock can still disagree with the server; Routines' returned business date should be checked before accepting its result.


## Technical decisions

- Use one shared client-side cache, persisted in versioned IndexedDB, with separate entries and request state for each screen and XP selection. Keep the existing backend endpoints authoritative and their HTTP `no-store` behavior.
- Use the approved official `@capacitor/app` v8 dependency to check the business date and freshness when the Android activity becomes active. Use a foreground timer for the 03:00 boundary.
- Keep the confirmed XP selection within one business day; select and fetch `Today` on the first opening of a new day and when an already-visible XP screen crosses 03:00. Provide a `Today` control.
- Invalidate affected cache entries after confirmed writes, refresh the visible affected page immediately, and preserve confirmed local changes against older or temporarily stale GET results.


## Alternatives considered

- `localStorage` would require less code but has a relatively small synchronous quota, which is a poor fit for multiple XP history responses. IndexedDB is the chosen store.
- Keeping four page-local caches would duplicate freshness, business-day, persistence, and race handling. One small shared data cache fits the existing app without adding a state-management framework.
- Adding a caching library would introduce a dependency and still require custom business-day, mutation, and XP-key rules. A focused cache module is preferable for four GET resources.
- DOM `visibilitychange` and `focus` events avoid a native dependency, but are less direct for Android activity resume. The [official Capacitor App plugin](https://capacitorjs.com/docs/apis/app) exposes an Android lifecycle event for this purpose and was approved by the user.


## Architecture and data flow

Flow: page entry or app resume -> calculate business date -> read current-day entry -> render cached data -> decide whether a GET is due -> fetch through existing API module -> validate response -> commit data and attempt metadata -> persist -> update the page. Manual refresh and successful writes mark affected keys as due before the same GET path runs. The API modules remain responsible for HTTP and response validation; the cache owns reuse and request state; pages own input and mutation UI.

Cache record: `{ key, businessDate, data?, fetchedAt?, attemptedAt?, lastError?, invalidated }`. `data` is a complete validated GET result; `fetchedAt` changes only after a successful GET, while `attemptedAt` changes when any GET completes. In-flight state is memory-only. Persist error metadata so a failed request suppresses automatic retry for one minute even after restart. The storage version permits discarding old or invalid records safely. A cache entry is usable only when its business date equals the current Warsaw business date.

On entry, a current-day entry renders before a due request starts. A GET is due if there is no completed attempt, the entry was invalidated, or at least 60 seconds have passed since `attemptedAt`. Manual refresh bypasses the interval. A single in-flight GET per key satisfies repeated refresh taps without duplicate HTTP calls. Every request carries a key, business date, and generation; completion is ignored if any of these are no longer current. At 03:00 the visible page first drops the old result, then selects the new day where applicable and fetches it. Resume runs the same day-change check before the freshness check.

After a confirmed write, update the visible cached result with the confirmed change when possible, persist that local correction, invalidate affected entries, and request the visible entry immediately. An older in-flight GET cannot overwrite the write. For Inbox and Today, retain the confirmed correction until a subsequent GET reflects it, so a temporarily stale Notion list does not undo a successful action. For Routines, retain the existing server-version merge and conflict behavior. A failed GET leaves the confirmed change and its prior `fetchedAt` visible. Mutation errors remain separate from GET request status.

Invalidation map: Inbox create/edit/delete -> Inbox; Inbox move to Today -> Inbox and Today; Today status/archive -> Today and current-business-date XP entries; routine transition -> Routines and current-business-date XP entries. The XP backend applies Android status changes to the current business date and reverses a completion only for a same-day correction. Routine mutations are likewise bound to the current business date. For each cached XP entry, invalidate it when its period contains that business date; an older historical period remains usable. Including XP for Today archive is conservative because its endpoint does not directly record XP, but later Notion events may change progress.


## Security and operational considerations

The persisted results include personal task and Inbox text. Browser storage is inside the Android app's private data area and should be cleared when the app's data is cleared. Do not log response bodies or cache contents. Cache data is advisory, never used as an authoritative write input; routine writes keep their existing server version and operation ID checks. A storage failure should reduce performance, not prevent a fresh request. No backend migration or API change appears necessary.


## Implementation plan

Implementation sequence:

1. Add the approved `@capacitor/app` v8 dependency and sync the Android project. Add a shared Warsaw business-date and timestamp helper. Move the routine page's boundary logic into the shared app lifecycle, schedule the next 03:00 boundary while foregrounded, and recheck on native app activation.
2. Add a versioned IndexedDB cache for the four GET resources and a separate XP-selection record. Keep a memory mirror, handle unavailable storage as a cache miss, and discard entries from earlier business days. Implement per-key freshness, request state, concurrent-request deduplication, and generation checks.
3. Convert Inbox, Today, and Routines to the shared GET path. Preserve confirmed local mutations and Routine version/conflict handling. Add the cross-screen invalidation map and immediate refresh of the currently visible affected page.
4. Convert XP Progress to selection keys `(period, periodStart)`, restore the selection within one business day, select `Today` on the first entry of a new day and at the visible 03:00 boundary, and add the explicit `Today` control.
5. Add the common header timestamp, refresh icon, status indicator, and GET error text to all four pages. Render current-day cached data while a refresh runs; distinguish initial loading, a successful empty result, and a failed request. Remove redundant page refresh controls.
6. Run the existing app build, manually verify the planned scenarios on Android, inspect the diff, and record actual validation and deviations in this document.


## Verification plan

During implementation, run `npm run build` in `NotionManagementApp`. Manually verify all four pages with network request inspection: first load; tab switch and app restart before/after one minute; manual refresh; failed GET with/without current-day data; successful mutations and affected-page refreshes; XP period switching, selection restoration, and `Today`; the 03:00 Warsaw boundary (including a suspended app); and that an unsuccessful request does not advance `Dane z`. No new automated test project is required by the working agreement.


## Implementation notes

- Added a shared, versioned IndexedDB cache with an in-memory mirror, per-key request attempts, deduplication, invalidation, and response generation checks. IndexedDB failures fall back to fresh API requests.
- All four Android screens use the shared cache header and Warsaw business-day lifecycle. The visible screen resets after 03:00 or app resume; XP selection is restored only within its saved business day.
- Confirmed Inbox and Today changes remain visible during refreshes through per-entry corrections; routine occurrences retain the higher server version. A successful mutation invalidates affected data and refreshes the visible screen.
- Added the approved `@capacitor/app` v8 plugin and synced the Android project. The existing backend endpoints and response contracts were unchanged.
- No material deviations from the approved design. Device-level manual verification remains to be performed.
- Follow-up implementation pass resolved the three Important review findings: obsolete-day cache writes are rejected, an active affected screen reloads on invalidation, and removal corrections retire when a GET confirms absence.

## Validation performed

- `npm run build` passed, including the existing routine validation script, TypeScript, and Vite production build.
- `npx cap sync android` passed and registered `@capacitor/app@8.1.1`.
- `git diff --check` passed; the implementation diff was inspected for request ordering, day boundaries, persistence, mutation reconciliation, and UI state.
- Android network and 03:00 acceptance scenarios were not run in this environment because `adb` is unavailable.
- After the review fixes, `npm run build` passed with access to the existing `dist` directory; the routine validation script, TypeScript check, and Vite build all passed. The unstaged and staged diffs passed `git diff --check`.

## Review findings

Review date: 2026-09-27. Comparison base: `main...HEAD` on branch `android-app-cache-v1`. Local `main` exists; its remote freshness was not verified and no fetch was performed. The three-dot diff contains only the feature document, so this review also inspected the staged implementation (`git diff --cached`). Findings refer to the current working-tree source. Production code was not changed during review.

1. **Important — Late previous-day mutations can replace current-day cache entries.** `src/cache/screenCache.ts:248-254` creates an entry for the caller's day when no matching entry exists, overwrites the map value indexed only by resource key, and increments the shared generation. Inbox/Today callbacks retain the day captured before their awaited write; Routines passes `request.expectedBusinessDate`. If a write starts before 03:00 and its confirmation arrives after the new day's GET starts or completes, it replaces the new-day entry and invalidates that GET's generation. The callback's old-day refresh cannot commit because `requestData` rejects results for an obsolete day. The visible screen can consequently lose its data without recovering automatically. Recommendation: reject obsolete-day cache writes and requests before changing entries or generations; where the completed mutation affects the current day, explicitly invalidate and refresh the current-day resource without applying an old-day snapshot.

   **Addressed in follow-up implementation:** Cache mutations and requests now reject an obsolete business date before touching entries or generations. Inbox and Today completions that cross 03:00 invalidate the current-day resource without applying the old-day correction. A subscribed current-day screen then reloads.

2. **Important — Invalidation does not refresh an affected screen that became visible during a write.** `src/cache/screenCache.ts:236-244` subscribes only to view updates; requests are triggered on mount/key/day/revisit changes or explicit refresh. `invalidate`/`invalidateXp` notify subscribers but do not start a new GET. For example, open cached XP, switch to Today, start a completion, then return to XP before the PATCH completes. The completion invalidates XP, but its callback refreshes Today through the old page instance. XP remains stale until another visit or manual refresh; if its GET was in progress, invalidation also discards that result without starting a replacement. Recommendation: make invalidation trigger a request for the currently subscribed affected resource, while leaving inactive resources deferred until entry.

   **Addressed in follow-up implementation:** Each mounted resource registers its loader. Invalidation starts a replacement GET for that resource if it is currently subscribed; inactive resources remain invalidated until entry. Mutation callbacks no longer force a GET through a page instance that may have unmounted.

3. **Important — Removal corrections never retire after server confirmation.** In `src/cache/screenCache.ts:176-179` and `191-194`, Inbox/Today removal corrections are always added to `outstanding`, including when the incoming list already omits the removed item. They persist until the business day changes. If a user restores the archived item in Notion later that day, every successful refresh still filters it out, contrary to the design's rule to retain corrections only until a GET reflects them. Recommendation: retire a removal correction when an authoritative GET first confirms the item's absence, just as matching upsert/status corrections are retired.

   **Addressed in follow-up implementation:** Inbox and Today removal corrections are retired when a successful GET omits the removed item, allowing a later restored item to appear on refresh.

Review validation: the routine validation script and TypeScript check passed. The first `npm run build` reached Vite but failed with `EPERM` when preparing the existing `dist/assets` directory. Re-running the same build with `--outDir` pointing to a temporary directory passed, including the Vite production bundle. `git diff --cached --check` passed. Findings are based on source and control-flow inspection; Android runtime, network inspection, process-restart, and 03:00 scenarios were not executed in this review. No automated tests were added. Native Android build and user acceptance remain unverified.

## Manual acceptance checklist

- [ ] On each of Inbox, Today, Routines, and XP, clear app data and open the screen: show initial loading (never a premature empty state), yellow request status, and `Dane z —`; after success show data or a genuine empty state, green status, and a Warsaw timestamp including seconds.
- [ ] Inspect network requests while switching tabs, resuming the app, and restarting its process: within 60 seconds of the last completed GET, reuse current-day data without another GET; after 60 seconds, show cached data while refreshing. Leaving a screen open beyond 60 seconds must not itself fetch.
- [ ] Force a GET failure with and without cached data: show red status and an error, preserve usable data and its timestamp, suppress automatic retry within 60 seconds even after restart, and retry on a later visit. Manual refresh must retry immediately and show yellow, then green after recovery. Repeated refresh taps must share the same in-flight GET.
- [ ] Exercise Inbox create/edit/delete/move, Today status/archive, and routine complete/reopen/skip: preserve confirmed changes through stale or failed GETs, refresh the visible affected screen, and invalidate other affected screens for their next entry. Confirm routine version conflicts and retry behavior still work.
- [ ] With a delayed write response, switch to another affected screen before completion (Today to XP, Routines to XP, Inbox move to Today): the newly visible screen must refresh immediately on confirmation, including when it already has a GET in flight. Unaffected historical XP periods must remain reusable.
- [ ] After archiving an Inbox/Today item and observing a GET that confirms its absence, restore it in Notion on the same business day: manual refresh must display it again.
- [ ] Visit multiple XP periods and starts: verify independent freshness intervals, same-day selection restoration after navigation/restart, and `Today` selecting the current business date under normal cache rules.
- [ ] Cross 03:00 Warsaw with each screen visible and with the app suspended, including XP on a historical period: hide old-day data, show loading, fetch the new day, and reset XP to `Today`. Repeat with a device time zone other than Warsaw.
- [ ] Delay a successful mutation response across 03:00 until after the new-day GET starts or finishes: current-day data must not disappear or be replaced by an old-day correction, and the new-day request must still complete normally.
- [ ] With unavailable or corrupt IndexedDB data, verify the API remains usable and cache/selection recovery does not crash or indefinitely block the screen.

## Open questions

None currently identified.

## Decisions log

| Date | Decision | Reason |
|---|---|---|
| 2026-09-27 | Start business discovery for Android App Cache V1. | The user requested the feature and described the initial behavior. |
| 2026-09-27 | Use the 03:00 `Europe/Warsaw` business-day boundary for cache behavior on every screen. | The user specified that "day" always means the business day. |
| 2026-09-27 | Hide prior-business-day data after 03:00 and show loading while the first new-day request runs. | The user accepted the recommendation to avoid briefly displaying yesterday's state. |
| 2026-09-27 | Show a data-loading error message whenever a request fails. | The user explicitly requested failure feedback. |
| 2026-09-27 | Keep current-business-day data visible after a failed refresh and use green/yellow/red request indicators. | The user confirmed retaining data and specified status colors and adjacent error text. |
| 2026-09-27 | Reset the one-minute freshness interval for data affected by a successful user change. | The user confirmed that a change should reset the interval. |
| 2026-09-27 | Refresh the visible affected screen immediately and other affected screens when next opened. | The user accepted the recommendation to defer refreshes for screens that are not visible. |
| 2026-09-27 | Use the time of the last successful full-screen fetch for `Dane z`; preserve it after a failed refresh. | The user accepted the recommended timestamp meaning. |
| 2026-09-27 | Cache each XP period selection separately, with its own one-minute interval. | The user requested separate counters for the XP selections. |
| 2026-09-27 | At 03:00, hide previous-day data on the visible screen and fetch fresh data; refresh other screens when opened. | The user confirmed that the open screen should hide the old data itself. |
| 2026-09-27 | Do not refresh a continuously open screen merely because one minute has elapsed. | The user accepted the recommendation to check freshness on return instead. |
| 2026-09-27 | Show `Dane z` as `dd.MM.yyyy HH:mm:ss` in `Europe/Warsaw`. | The user accepted the recommended display format and time zone. |
| 2026-09-27 | Start the normal one-minute request interval after a failed request and allow immediate manual retry. | The user said the counter starts normally after failure and that manual refresh is available. |
| 2026-09-27 | Retry automatically on a later screen/app visit more than one minute after a failed request. | The user clarified that failed and successful requests follow the same revisit interval; manual refresh bypasses it. |
| 2026-09-27 | Show `Dane z —` until the first successful fetch. | The user accepted the recommended empty timestamp state. |
| 2026-09-27 | Complete business discovery. | The user confirmed the documented business behavior and approved closing the stage. |
| 2026-09-27 | Restore the last XP period and start after tab switches and app restarts; add `Today` to select the current business day. | The user specified both behaviors during technical design; the following decision limits restoration to the same business day. |
| 2026-09-27 | On the first XP opening of each business day, select and fetch `Today`; restore the last selection only on later openings within that same day. | The user clarified that the first opening of the day must fetch today's data. |
| 2026-09-27 | Switch an already-visible XP screen to `Today` at the 03:00 boundary and fetch the new day's data. | The user confirmed that the boundary should reset the visible XP selection. |
| 2026-09-27 | Add the official `@capacitor/app` v8 plugin for Android app activation detection. | The user explicitly approved the dependency; native activation will trigger business-day and freshness checks. |
| 2026-09-27 | Complete technical design with a shared IndexedDB cache, per-screen request state, a shared Warsaw business-day lifecycle, and the documented implementation plan. | The user explicitly asked to finish the technical analysis after reviewing its scope and approving the new dependency. |
