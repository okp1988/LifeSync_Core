# Project Architecture

## Application Shape

Life Sync Personal Tracker is a Windows-only WPF desktop app targeting `net8.0-windows10.0.19041.0`. TASK is the only workspace. It manages recurring tasks backed by Google Sheet, with a local cache, durable mutation queue, local Watch List, manual Sync, daily summary, check-in reminders, snooze, and Google Task reminder metadata.

## Main Components

- `MainWindow.xaml`: unified Tasks/Watch List/Daily Summary switch, toolbar, filters, main views, editors, conflict review, and combined settings.
- `MainWindow.xaml.cs`: startup and focused UI event wiring.
- `ViewModels/MainViewModel.cs`: task synchronization, local cache/outbox, filters, selection, completion, summary, and check-in workflows.
- `Models/SheetTask.cs`: task data and computed UI state.
- `Models/TaskSyncModels.cs`: mutation, conflict, and task editor contracts.
- `Models/CheckinSettings.cs`: daily check-in schedule and state.
- `Models/WatchListEntry.cs`: local Watch List identity and date-added record.
- `Services/GoogleSheetClient.cs`: Google Apps Script HTTP client.
- `Services/JsonFileStore.cs`: local JSON persistence.
- `Services/AppPaths.cs`: canonical runtime paths.
- `Services/AppLogger.cs`: retained info and warning/error logs.
- `ViewModels/RangeObservableCollection.cs`: single-notification replacement of cached task rows.
- `apps-script`: production API, migration, and Google Task reminder trigger source.

## Ownership Rules

- Keep task and check-in workflows in `MainViewModel` unless behavior clearly belongs to storage, logging, HTTP, or a pure model calculation.
- Keep Google Apps Script HTTP details in `GoogleSheetClient`.
- Keep runtime path changes in `AppPaths` with matching `JsonFileStore` changes.
- Keep computed task fields in `SheetTask`.
- Keep UI layout in `MainWindow.xaml` and event glue in `MainWindow.xaml.cs`.

## Data Direction

1. Google Sheet is the task source of truth.
2. `tasks.json` is a local cache with optimistic pending changes.
3. `task-sync-queue.json` is the durable outbox.
4. `watch-list.json` is local-only and references tasks by stable Task ID.
5. Sync uploads queued mutations, retrieves sheet tasks, and merges by stable Task ID.
6. Local mutations save cache and outbox state before background upload.

## UI Structure

The workspace contains:

- A segmented Tasks, Watch List, and Daily Summary main-view switch.
- Header actions for check-in, settings, Conflicts, New Task, and Sync.
- Compact Category, Type, Search Task, Status, Mode, and Clear controls in Tasks view.
- Task grid with single-click selection, double-click detail, compact date/relative-day cells, and a three-icon Alert/Sync/Status state column.
- Watch List grid with Category, Type, Task, Day Left, Date Added, and access to normal task detail.
- Full-width Daily Summary view with expired, warning, and snoozed groups plus snooze extension actions.
- Task completion sidebar.
- Task editor and conflict review overlays.
- Settings overlay for Google connection, log retention, and check-in schedule.

## Sorting

- Normal mode sorts by Category, Type, Task.
- Priority mode groups alert-enabled expired and warning tasks first, followed by expired, warning, and all remaining tasks; each group uses Category, Type, Task.
- Grid columns are not user-sortable. Day Left and Day Passed remain display values beside their dates.
- Filters apply immediately.
- The filter toolbar remains compact and left-aligned at all supported window widths.
- Cached tasks are inserted with one collection reset so startup sorting, filtering, and grid layout run once rather than once per row.
- Task-grid text and template cells share the same vertical centering and horizontal inset.

## Task Grid Display

- Column order is Category, Type, Task, Expired Date, Warning Date, Alert Date, Last Executed Date, Remark, State.
- Empty dates display `-`; Warning Date also displays `-` when it duplicates Expired Date.
- Expired Date shows days left as a positive bracketed number and overdue days as a negative number.
- Alert Date is based only on Snooze Until. Active days left and elapsed days passed are positive bracketed numbers.
- Last Executed Date shows elapsed days as a positive bracketed number.
- State contains three non-clickable, tooltip-labelled icons for Alert, Sync, and Status.
