# Project Flows

## Startup Flow

1. `MainWindow` constructs `MainViewModel` and sets it as `DataContext`.
2. `Window_Loaded` calls `InitializeAsync`.
3. `InitializeAsync` loads config and prunes old logs.
4. Cached tasks are loaded from `tasks.json`.
5. Pending task mutations are loaded from `task-sync-queue.json`.
6. Check-in settings are loaded or created.
7. Task filters are reset.
8. The check-in timer starts.
9. Check-in reminders are checked once.
10. Date-derived task display state refreshes after the day changes.
11. TRACK data, TRACK expiry reminders, and TRACK timer work are not loaded while TRACK is hidden.

## Manual Task Sync Flow

1. User presses Sync.
2. Pending mutations are uploaded in task/order sequence using Task ID and expected revision.
3. Successful responses replace optimistic fields with the canonical server task.
4. Stale writes become conflicts with a server snapshot.
5. LifeSync retrieves current non-archived tasks and merges them by Task ID without overwriting pending local tasks.
6. Tasks and the mutation queue are saved locally.

## Task Create And Edit Flow

1. New Task creates a local UUID and recurring-rule draft with no dates.
2. Saving updates `tasks.json`, appends a create/update mutation, and immediately attempts upload.
3. Network failure leaves the row Pending.
4. First completion calculates warning and expiry dates from the execution date and recurring intervals.
5. Archive hides the row while preserving it online.

## Mark Complete Flow

1. User selects a task. Single-click only selects; double-click or Summary Open opens the task sidebar.
2. `CompletionDate` resets to today.
3. User edits remark and presses Mark Complete.
4. The app asks for confirmation.
5. The selected task is advanced locally to its next recurring cycle.
6. Previous dates are shifted, next warning/expired dates are calculated, snooze is cleared, and recalculated fields are raised.
7. The task sidebar closes.
8. `tasks.json` and `task-sync-queue.json` are saved.
9. The UI message says the local update is done and the user can continue working.
10. The completion mutation is uploaded in the background with a stable operation ID.
11. Apps Script looks up Task ID, checks revision, advances the recurring cycle, and returns the canonical task.
12. Failed network requests remain pending; stale revisions require conflict review.

## Task Daily Summary Flow

1. User presses Summary in the TASK header.
2. The right-side summary drawer opens without changing the main grid filters.
3. Expired or overdue unsnoozed rows appear under `Expired / Overdue`.
4. Warning-reached, unexpired, unsnoozed rows appear under `Warning`.
5. Rows with `Snooze Until` today or later are hidden from the summary.
6. Summary rows are readable item cards with severity strip, task title, badges, day-state pill, date line, remark preview, and reminder/snooze/sync chips.
7. Mouse wheel scrolling works over the task display area, not only over the scrollbar.
8. `Open` selects the task and opens the normal task detail sidebar.
9. Quick snooze actions calculate today plus the selected day count.
10. Snooze and Clear Snooze save local state and queue first, then upload in the background.
11. Snooze mutation dates are serialized as `yyyy-MM-dd` so Apps Script can parse them reliably.

## Google Task Reminder Flow

1. Apps Script trigger checks TASK rows independently of LifeSync.
2. Warning, expired, and weekly overdue reminder stages are eligible when `Alert` is checked and snooze is inactive.
3. If warning and expiry are the same date, only the expired stage is created.
4. Apps Script builds reminder keys from Task ID, cycle expired date, and stage.
5. If the key matches `Last Google Task Key`, no duplicate Google Task is created.
6. Otherwise Apps Script creates a reminder task and stores key, task id, and created date in the sheet.
7. Google Task completion is not read back into LifeSync or the sheet.

## Check-In Flow

1. The check-in timer ticks every minute.
2. The app finds today's `CheckinDaySetting`.
3. If the day is enabled and the current time is past the HHmm setting, the app checks whether today's check-in or reminder already happened.
4. If not, `LastAlertDate` is set to today and saved.
5. A reminder message box is shown.
6. When the user checks in manually, `LastCheckinAt` is set to now, `LastAlertDate` is set to today, and settings are saved.

## Track Item Edit Flow

TRACK is hidden and dormant in the current app. The flows below are preserved for future recovery only.

1. New creates a draft `TrackItem` with default type `Quantity`.
2. Edit clones the selected item into a draft.
3. Save validates that name is present.
4. New items are cloned into `_trackItems`; edits copy editable metadata onto the selected item.
5. New categories are saved into `track-options.json`.
6. Track category filters are rebuilt.
7. `track-items.json` is saved.

## Track Home Inventory Flow

This flow is disabled while TRACK is hidden.

1. If TRACK is re-enabled, it opens on the Attention view.
2. The user can switch between Attention, All Items, and Categories.
3. Attention shows inventory needing action: low/out stock, expiring/expired stock, replacement due items, and maintenance/repair count.
4. Selecting an item opens a read-first item detail sidebar.
5. Item detail shows summary and actions first, then transaction history below.

## Track Quantity Flow

1. Add Stock asks for quantity, date, optional location, optional note, and expiry date when the item tracks expiry.
2. Add Stock creates an `Owned` history record and a unique batch id. The batch is the add-stock record itself.
3. Use Stock asks for quantity, date, optional location, and optional note.
4. Use Stock allocates automatically from nearest-expiry stock first, then oldest stock, and creates one `Used` history record per consumed batch.
5. Put Back asks for quantity, date, returned-to location, optional note, and only shows a return target choice when multiple valid targets exist.
6. Put Back creates a `Put Back` history record linked to the source batch by `SourceBatchId`.
7. Each history change sorts history newest-first, recalculates quantities, refreshes batch choices, and saves `track-items.json`.

## Track Change Cycle Flow

1. Change Cycle items use `ChangeEvery` and `ChangeUnit`.
2. A `Changed` history record resets `StartUseDate` to the selected action date.
3. `NextChangeDate` is calculated from the most recent changed record, any last record date, or `StartUseDate`.
4. `AlertStatus` shows expired, due today, or days left.
5. Change Cycle actions do not use stock batches, expiry batch fields, Add Stock, Use Stock, or Put Back.

## Track Maintenance And Repair Flow

This flow is disabled while TRACK is hidden.

1. If TRACK is re-enabled, the TRACK header shows Maintenance.
2. If old unbound stock records are detected, Maintenance displays a count.
3. Detection scans Quantity history for `Owned` records missing `BatchId` and `Used` or `Put Back` records missing `SourceBatchId`; detection does not save changes.
4. The repair overlay shows broken rows with suggested batch links.
5. Saving approved repair rows creates a before-repair backup of `track-items.json` before writing fixes.

## Track Expiry Reminder Flow

This flow is disabled while TRACK is hidden.

1. Startup and saving check-in settings call `CheckAndNotifyTrackExpiryAsync`.
2. Each expiry-enabled track item contributes its nearest non-empty expiring batch.
3. The app checks the configured expiry alert rules against days until expiry.
4. If the alert key has not been shown before, it is appended to `ShownExpiryAlertKeys`.
5. `track-settings.json` is saved.
6. A message box lists up to 12 new expiry alerts.
