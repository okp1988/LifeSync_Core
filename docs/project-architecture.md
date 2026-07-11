# Project Architecture

## Application Shape

Life Sync Personal Tracker is a Windows-only WPF desktop app. It targets `net8.0-windows10.0.19041.0`, uses WPF binding, and stores runtime files under the build output folder.

The app currently has one active workspace and one dormant workspace:

- **TASK**: Google Sheet-backed recurring tasks with stable Task IDs, local cache, offline mutation queue, manual Sync, daily summary, snooze, and Google Task reminder metadata.
- **TRACK**: hidden/dormant local Home Inventory implementation. Existing JSON and code are preserved for possible future recovery, but normal startup/timer/UI work does not load it.

## Main Components

- `MainWindow.xaml`: owns the entire UI, including tabs, toolbar controls, filters, grids, overlays, and sidebars.
- `MainWindow.xaml.cs`: contains UI event wiring that is awkward in XAML-only binding: startup, Escape behavior, tab close/reset, date picker input blocking, managed grid sorting, row/background selection behavior, and grid column width reset.
- `ViewModels/MainViewModel.cs`: central workflow hub for task sync, local cache/outbox, filtering, selection, completion, daily summary, check-in reminders, dormant track workflows, and settings saves.
- `Models/SheetTask.cs`: Google Sheet task model plus computed task UI state.
- `Models/TrackItem.cs`: local tracker item model plus computed stock, change, expiry, batch, and highlight state.
- `Models/CheckinSettings.cs`: daily check-in schedule and check-in state.
- `Models/TrackSettings.cs`: stock thresholds, colors, expiry alert rules, and already-shown expiry alert keys.
- `Models/TrackOptions.cs`: persisted category and remark suggestions for tracker inputs.
- `Services/GoogleSheetClient.cs`: the only HTTP client for Google Apps Script task operations.
- `Services/JsonFileStore.cs`: all local JSON load/save behavior.
- `Services/AppPaths.cs`: canonical runtime paths.
- `Services/AppLogger.cs`: info and warning/error file logging with retention cleanup.
- `apps-script`: production server-side Apps Script, migration, and reminder trigger source. `docs/google-apps-script.js` is a deprecated pointer.

## Ownership Rules

- Keep workflow behavior in `MainViewModel` unless it is clearly storage, logging, HTTP, or pure model calculation.
- Keep Google Apps Script HTTP details only in `GoogleSheetClient`.
- Keep runtime path changes only in `AppPaths` plus matching `JsonFileStore` updates.
- Keep computed task fields in `SheetTask`.
- Keep computed tracker fields in `TrackItem`.
- Keep UI layout and visual states in `MainWindow.xaml`; keep only event glue in `MainWindow.xaml.cs`.

## Data Direction

TASK data source of truth:

1. Google Sheet is the source of truth.
2. `tasks.json` is a local cache with optimistic pending changes.
3. `task-sync-queue.json` is the durable outbox for unsynced task mutations.
4. Pressing Sync pushes queued mutations, pulls sheet tasks, and merges by stable Task ID.
5. Create, edit, complete, snooze, clear-snooze, and archive save locally first, then upload in the background.

TRACK data source of truth:

1. TRACK is hidden and dormant in the current product.
2. Existing local JSON files remain the preserved source of truth if TRACK is re-enabled.
3. Tracker items do not sync to Google Sheet.

## UI Structure

- TASK tab:
  - Header with app title, check-in control, settings, Summary, Conflicts, New Task, and Sync.
  - Filter panel for category, type, day-left, and status.
  - Task grid where single-click selects and double-click opens detail.
  - Daily Summary drawer with readable expired/warning cards and quick snooze actions.
  - Right task sidebar for completion date and remark.
  - Task editor and conflict review overlays.
  - Check-in settings overlay.

- TRACK tab:
  - Hidden and disabled in the current app.
  - Preserved implementation includes:
    - Header with New, Edit, Remove, Maintenance, and settings.
    - Home view selector for Attention, All Items, and Categories.
    - Filter panel for category and type.
    - Compact inventory grid for item browsing.
    - Category summary grid when Categories is selected.
    - Right edit sidebar for item metadata.
    - Right item detail sidebar with summary, focused actions, and transaction history below.
    - Track settings overlay and manual repair/maintenance overlay.

## Sorting

- Default task sort is Category, Type, Task.
- Only task `Day Left` is user-sortable.
- Default track sort is Category, Name if TRACK is re-enabled.
- Only track `Left` quantity is user-sortable if TRACK is re-enabled.
- Filter changes apply immediately. Refresh buttons are intentionally not part of the normal TASK or TRACK flow.

## Open Design Notes

- TRACK should stay inventory-first rather than ledger-first.
- Batches are internal stock references created by Add Stock; daily Use Stock should not ask the user to pick a batch.
- Put Back needs clear return-location handling because the original use location and returned-to location are different decisions.
- Item detail transaction display is still an active design area. It currently shows event, signed quantity, stock reference, optional expiry, place, and note.
