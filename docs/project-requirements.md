# Project Requirements

## Platform

- Must run as a Windows WPF desktop app.
- Must target `net8.0-windows10.0.19041.0`.
- Must build on a machine with the Windows SDK available.

## Task Requirements

- Must load cached tasks from `<build output>\data\tasks.json` on startup.
- Must not call Google Sheet automatically for tasks on startup.
- Must fetch tasks only when the user presses Sync.
- Must push pending local mutations before pulling Google Sheet tasks.
- Must merge fetched tasks by stable `Task ID` and must not overwrite pending optimistic local tasks.
- Must surface missing or duplicate Task IDs as sync errors instead of falling back to row numbers.
- Must support both modern `{ tasks: [...] }` JSON and legacy `{ data: [...] }` row-array responses.
- Must show task status from computed local state:
  - `Completed` if `Completed` is true.
  - `Expired` if expired date is today or earlier.
  - `Warning` if warning date is today or earlier.
  - `Normal` otherwise.
- Must call `NotifyCalculatedFieldsChanged()` after changing task fields that affect `DayLeft`, `DayPassed`, `Status`, or completion display.
- Must keep locally cached tasks visible until Sync returns canonical sheet state.
- Must support status filters: `ALL`, `Normal`, `Warning`, `Expired`, `Pending`, and `Warning + Expired`.
- `Pending` means the task has a queued or failed sync operation and `SyncState` is not `Synced`.
- Main grid single-click selects a task only; double-click opens the task detail sidebar.
- Daily Summary rows must be visually scannable and support mouse-wheel scrolling over the task display area.

## Completion Requirements

- Selecting a task must reset `CompletionDate` to today.
- Mark Complete must ask for confirmation.
- Mark Complete must update the local cache and queue before waiting for the API response.
- Mark Complete must:
  - Save the selected remark.
  - Set `LastExecutedDate`.
  - Shift `PreviousDate1` into `PreviousDate2`.
  - Set `PreviousDate1` to the completion date.
  - Calculate next expired and warning dates from the recurring intervals.
  - Clear snooze fields.
  - Refresh calculated fields.
  - Save `tasks.json` and `task-sync-queue.json`.
  - Close the task sidebar.
  - Start upload in the background.
- The UI must not block other task actions while completion upload is running.
- Failed background upload must remain pending and be logged/surfaced through `Message`.
- Revision conflicts must show as `Conflict` and require explicit resolution.

## Tracker Requirements

- TRACK is currently hidden and dormant.
- Must not load TRACK data for normal UI use, timer work, expiry reminders, or Task Tracker exports while TRACK is hidden.
- Must not delete or migrate existing TRACK JSON because it may be recovered in the future.
- Existing TRACK implementation and local files remain documented for future reference only.
- If TRACK is re-enabled later, it should follow these preserved rules:
  - Load tracker items from `<build output>\data\track-items.json`.
  - Keep tracker data local; it is not Google Sheet-backed.
  - Present TRACK as a Home Inventory workflow, with Attention as the default view.
  - Provide Attention, All Items, and Categories views.
  - Keep Maintenance available from the TRACK header and show a repair count when old records need attention.
  - Support two track types: `Quantity` and `Change Cycle`.
  - Derive quantity values from history.
  - Add Stock creates batches by writing `BatchId` on `Owned` records.
  - Use Stock allocates internally from nearest-expiry stock first, then oldest stock.
  - Put Back links to a returnable source batch and records returned-to location.
  - Change Cycle items must not participate in batch logic or show stock actions.
  - Item detail transaction history should stay secondary to summary/actions.
## Check-In Requirements

- Must load check-in settings from `<build output>\data\checkin-settings.json`.
- Must create default check-in settings if no file exists.
- Must support per-day enablement and HHmm time text.
- Must check for reminders on startup and every minute after startup.
- Must not show more than one check-in reminder per day.
- User Check-in must set `LastCheckinAt` to now and `LastAlertDate` to today.

## Track Alert Requirements

- Track alert processing is disabled while TRACK is hidden.
- Preserved behavior if TRACK is re-enabled:
  - Load track settings from `<build output>\data\track-settings.json`.
  - Support configurable low, critical, and out-of-stock thresholds and colors.
  - Support configurable expiry alert rows with amount, unit, and color.
  - Sort expiry alerts by largest alert window first.
  - Persist `ShownExpiryAlertKeys` so the same item/batch/expiry/alert level is not shown repeatedly.

## Logging Requirements

- Must write info logs to `<build output>\log\lifesync-info-yyyy-MM-dd.log`.
- Must write warnings/errors to `<build output>\log\lifesync-warning-error-yyyy-MM-dd.log`.
- Must prune old log files on startup using `LogRetentionDays`.
- Logging failures must never block app workflows.
- Token-bearing payloads must be redacted before logging.
