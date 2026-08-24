# Project Flows

## Startup Flow

1. `MainWindow` constructs `MainViewModel`.
2. `InitializeAsync` loads config and prunes old logs.
3. Cached tasks, pending mutations, completion history, and check-in settings load concurrently.
4. Cached tasks are calculated and published to the Tasks grid with one collection reset; sync state, filters, and sorting use the complete cache.
5. Tasks view opens while Priority, Daily Summary, and History show loading labels and remain disabled.
6. After a 150 ms first-render delay, Priority rows and Daily Summary groups build from task snapshots on a worker task while History is prepared from its local event records.
7. The snapshot results apply on the UI thread; stale snapshots fall back to rebuilding from the current task set, then all secondary views become available.
8. The check-in timer starts, and no Google Sheet request runs automatically.

## Manual Task Sync Flow

1. The user presses Sync.
2. All Pending mutations upload in task/order sequence, including completions whose one-hour `UploadAfter` time has not arrived; a completion loses Undo eligibility after it synchronizes.
3. A failed mutation stays Pending but does not prevent queued mutations for other task IDs from being attempted.
4. Successful responses replace optimistic fields with canonical server data.
5. Stale revisions become conflicts.
6. Current non-archived tasks are retrieved and merged by stable Task ID only when mutation processing has no remaining upload failure.
7. Cache and mutation queue state are saved.
8. Task filters reset while Category/Type/Task ordering remains fixed.

## Settings Flow

1. The gear opens drafts of the saved Google connection, log retention, and check-in schedule.
2. Cancel restores saved values without writing files.
3. Save validates check-in times, normalizes log retention, writes `config.json` and `checkin-settings.json`, and refreshes reminder state.

## Task Create And Edit Flow

1. New Task opens a recurring-rule draft; Edit Task copies the selected task into the draft.
2. Category and Type use existing searchable options but remain editable for new values; `ALL` is not an editor option.
3. Save validates the draft, title-cases Category, Type, and Task, and creates a stable local UUID for a new task.
4. For an active task, changing either cycle recalculates Warning and Expired dates from Last Executed Date, with Prev Date 01 as the legacy fallback; a warning date after expiry is rejected.
5. Save validates the optional searchable Main Task / Unlocked By relationship, rejects cycles, inherits custom-filter membership from the linked root, and reconciles stable minor definitions.
6. Save updates `tasks.json`, appends a mutation, and attempts background upload.
7. Network failure leaves the row Pending.
8. First completion calculates warning and expiry dates. Pause preserves current dates for later return; Archive is not offered as a user action.

## Mark Complete Flow

1. Selecting a task resets completion date to today.
2. Below Remark, the sidebar shows independent minor checkboxes, editable completion dates, and compact `E` expiry, `L` last-completion, and `I` interval values. Each minor date defaults to the main Complete Date. The user checks only minors completed this time and may change each checked date. Mark Complete shows the normal confirmation only, with no separate minor panel or Select All.
3. The client rejects legacy task cycles whose expired value is not positive or whose warning value is negative before changing local state or queueing a mutation.
4. The task advances locally to its next recurring cycle.
5. Previous dates shift, warning/expired dates recalculate, and snooze clears.
6. Selected minors update their latest completion and optional due date. Unchecked minors remain unchanged.
7. A follower then locks while keeping its new dates. Every completed task unlocks only its currently locked direct followers and leaves each follower's existing dates unchanged.
8. Cache, outbox, and a new parent History record save the parent/minor/follower before-state before the action UI closes.
9. The compound completion mutation receives an upload time one hour in the future; the minute timer and background uploader respect it, while an explicit manual Sync bypasses it.
10. During that pending window, Undo restores the complete parent/minor/follower state when none of the affected tasks changed later.
11. At or after the deadline, the timer uploads one compound mutation. Success merges every affected task; failures remain Pending and stale parent revisions require conflict review.

## Pause Flow

1. Pause from task detail accepts a future Resume Date or a blank indefinite pause.
2. A task can pause only after its direct followers are paused. It disappears from DEFAULT, custom views, Priority, Daily Summary, and Google reminders without changing dates; only its own custom memberships are removed.
3. `Paused (N)` opens the manager. Resume is allowed only after its predecessor resumes; Edit changes its normal definition while retaining pause state.
4. A dated pause becomes ineffective on its Resume Date, so the task automatically returns with its original dates and may already be overdue.

## Saved Filter Flow

1. `DEFAULT` is selected unless another view is the one favourite. `ALL` shows the complete non-archived linked hierarchy.
2. Manage Filters searches by category/type/task and shows Level, Category, Type, Task, Next Date, Status, and link state for each membership row.
3. Checking a root or independent task controls its unpaused descendants; follower checkboxes are inherited and disabled. A stored mismatch is named as a warning.
4. Add, rename, delete, favourite, and membership changes stay in an isolated in-memory draft. Save validates and atomically writes `task-filters.json`; Close or Escape discards the whole draft.
5. Custom views hide paused and locked tasks. `ALL` remains the explicit management view that shows the complete non-archived hierarchy.

## History Flow

1. History is selected from the peer main-view switch without changing task filters.
2. Previous and next controls choose a calendar month; the current month is the default and future months are unavailable.
3. The view orders that month's persisted local actions and synchronized Audit rows by recorded time.
4. Each eligible Pending row has its own Undo button; Synced, Conflict, Undone, legacy, or superseded rows do not.
5. Sync imports Audit rows with stable Task ID and operation ID, deduplicating matching local completion actions.
6. Existing latest-completion cache values seed up to 10 legacy Synced rows only when no history file exists; every later completion is a separate event.

## Priority Flow

1. Priority is selected from the same main-view switch as Tasks, Daily Summary, and History without changing task filters.
2. Active unsnoozed expired tasks appear in the left table; warning-reached, not-yet-expired tasks appear in the right table.
3. Both tables show Category, Type, Task, the relevant date, and signed numeric Days: positive remaining, zero today, negative overdue.
4. Level is not shown as a separate column; the Days cell grades from light for Level 1 to dark for Level 5.
5. Rows sort by Level descending (5 first), Day Left ascending, Category, Type, and Task.
6. Double-clicking a row opens the taller detail area below both tables. While it remains open, single-clicking another row changes the previewed task.
7. The detail Remark accepts wrapped multiline text and scrolls vertically when needed.

## Daily Summary Flow

1. Daily Summary is selected from the same main-view switch as Tasks and Priority without changing task filters.
2. Only tasks with Create Google Task alerts enabled are eligible for Daily Summary.
3. Unsnoozed expired alert-enabled tasks appear under Expired / Overdue.
4. Unsnoozed warning-reached alert-enabled tasks appear under Warning.
5. Active snoozed alert-enabled tasks appear under Snoozed and remain available for opening, clearing, or extending their snooze.
6. Alert-enabled tasks carry a visible Alert badge in every summary group.
7. Open selects the task and opens normal task detail over Daily Summary.
8. Closing task detail returns to Daily Summary.
9. Quick snooze actions add days from an active snooze date, or from today for an unsnoozed task.
10. Custom snooze sets an explicit date; all snooze actions update local state and queue before background upload.
11. Date-only snooze values serialize as `yyyy-MM-dd`.

## Google Task Reminder Flow

1. The Apps Script trigger checks sheet rows independently.
2. Rows are eligible when Alert is checked, dates are valid, the task is not effectively paused, and a linked follower is unlocked. Before Snooze Until, reminder creation is blocked.
3. Snooze End before Warning creates a Snooze End task; during Warning it creates the delayed Warning task; on or after Expiry it creates one Expired/Overdue task.
4. A post-expiry snooze resets weekly overdue stages to seven-day slots measured from Snooze Until.
5. Warning, expired, snooze-end, and weekly overdue stages produce distinct reminder keys.
6. Matching `Last Google Task Key` values prevent duplicates.
7. Apps Script stores the created task ID, key, and date in the sheet.
8. Google Task completion is not imported into LifeSync.

## Check-In Flow

1. The timer checks every minute.
2. Today must be enabled and past its configured HHmm time.
3. A reminder is skipped when today was already checked in or alerted.
4. Showing a reminder persists `LastAlertDate`.
5. Manual check-in persists `LastCheckinAt` and today's alert date.
