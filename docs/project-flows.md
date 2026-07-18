# Project Flows

## Startup Flow

1. `MainWindow` constructs `MainViewModel`.
2. `InitializeAsync` loads config and prunes old logs.
3. Cached tasks and pending mutations are loaded.
4. Check-in settings are loaded or created.
5. Filters and calculated task state are refreshed.
6. The check-in timer starts.
7. No Google Sheet request runs automatically.

## Manual Task Sync Flow

1. The user presses Sync.
2. Pending mutations upload in task/order sequence.
3. Successful responses replace optimistic fields with canonical server data.
4. Stale revisions become conflicts.
5. Current non-archived tasks are retrieved and merged by stable Task ID.
6. Cache and mutation queue state are saved.

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

## Daily Summary Flow

1. Summary opens without changing grid filters.
2. Unsnoozed expired tasks appear under Expired / Overdue.
3. Unsnoozed warning-reached tasks appear under Warning.
4. Open selects the task and opens normal task detail.
5. Snooze actions update local state and queue before background upload.
6. Date-only snooze values serialize as `yyyy-MM-dd`.

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
