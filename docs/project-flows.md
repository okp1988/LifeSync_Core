# Project Flows

## Startup Flow

1. `MainWindow` constructs `MainViewModel`.
2. `InitializeAsync` loads config and prunes old logs.
3. Cached tasks, pending mutations, Watch List entries, and check-in settings load concurrently.
4. Cached tasks are calculated first and then published to the grid with one collection reset.
5. Sorting, filtering, summaries, Watch List rows, and sync state rebuild against the complete cache.
6. Tasks view opens in Normal mode and the check-in timer starts.
7. No Google Sheet request runs automatically.

## Manual Task Sync Flow

1. The user presses Sync.
2. Pending mutations upload in task/order sequence.
3. Successful responses replace optimistic fields with canonical server data.
4. Stale revisions become conflicts.
5. Current non-archived tasks are retrieved and merged by stable Task ID.
6. Cache and mutation queue state are saved.
7. The selected Normal/Priority mode is preserved while task filters reset.
8. Watch entries missing from a successful Sheet response are removed; failed Sync attempts never prune them.

## Settings Flow

1. The gear opens drafts of the saved Google connection, log retention, and check-in schedule.
2. Cancel restores saved values without writing files.
3. Save validates check-in times, normalizes log retention, writes `config.json` and `checkin-settings.json`, and refreshes reminder state.

## Task Create And Edit Flow

1. New Task creates a local UUID and recurring-rule draft.
2. Save updates `tasks.json`, appends a mutation, and attempts background upload.
3. Network failure leaves the row Pending.
4. First completion calculates warning and expiry dates.
5. Archive hides the task while preserving it in Google Sheet.

## Mark Complete Flow

1. Selecting a task resets completion date to today.
2. The user edits the remark and confirms Mark Complete.
3. The task advances locally to its next recurring cycle.
4. Previous dates shift, warning/expired dates recalculate, and snooze clears.
5. Cache and outbox save before the sidebar closes.
6. Upload starts in the background.
7. Failures remain Pending and stale revisions require conflict review.
8. The task is removed from Watch List immediately.

## Watch List Flow

1. Add/Remove Watch List in task detail persists a stable Task ID and original date-added timestamp locally.
2. The Watch List grid resolves Category, Type, Task, and Day Left from the current cached task.
3. Open or double-click uses the normal selected-task sidebar without leaving Watch List view.
4. Completion and archive remove the entry immediately.

## Daily Summary Flow

1. Daily Summary is selected from the same main-view switch as Tasks and Watch List without changing task filters.
2. Unsnoozed expired tasks appear under Expired / Overdue.
3. Unsnoozed warning-reached tasks appear under Warning.
4. Active snoozed tasks appear under Snoozed and remain available for opening, clearing, or extending their snooze.
5. Alert-enabled tasks carry a visible Alert badge in every summary group.
6. Open selects the task and opens normal task detail over Daily Summary.
7. Closing task detail returns to Daily Summary.
8. Quick snooze actions add days from an active snooze date, or from today for an unsnoozed task.
9. Custom snooze sets an explicit date; all snooze actions update local state and queue before background upload.
10. Date-only snooze values serialize as `yyyy-MM-dd`.

## Google Task Reminder Flow

1. The Apps Script trigger checks sheet rows independently.
2. Rows are eligible when Alert is checked, dates are valid, and snooze is inactive.
3. Warning, expired, and weekly overdue stages produce distinct reminder keys.
4. Matching `Last Google Task Key` values prevent duplicates.
5. Apps Script stores the created task ID, key, and date in the sheet.
6. Google Task completion is not imported into LifeSync.

## Check-In Flow

1. The timer checks every minute.
2. Today must be enabled and past its configured HHmm time.
3. A reminder is skipped when today was already checked in or alerted.
4. Showing a reminder persists `LastAlertDate`.
5. Manual check-in persists `LastCheckinAt` and today's alert date.
