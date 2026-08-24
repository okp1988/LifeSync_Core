# Project Architecture

## Application Shape

Life Sync Personal Tracker is a Windows-only WPF desktop app targeting `net8.0-windows10.0.19041.0`. TASK is the only workspace. It manages recurring tasks backed by Google Sheet, with a local cache, durable mutation queue, Priority and Daily Summary views, local completion history/Undo, check-in reminders, snooze, and Google Task reminder metadata.

## Main Components

- `MainWindow.xaml`: unified Tasks/Priority/Daily Summary/History switch, toolbar, filters, main views, editors, conflict review, and combined settings.
- `MainWindow.xaml.cs`: startup and focused UI event wiring.
- `ViewModels/MainViewModel.cs`: task synchronization, local cache/outbox, filters, selection, completion, summary, and check-in workflows.
- `Models/SheetTask.cs`: task data, linked/pause state, and computed UI state.
- `Models/MinorTask.cs`: stable minor definitions and per-completion selection drafts.
- `Models/TaskSyncModels.cs`: mutation, conflict, and task editor contracts.
- `Models/CompletionHistoryRecord.cs`: completion event, status, and pre-completion Undo snapshot.
- `Models/CheckinSettings.cs`: daily check-in schedule and state.
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
4. `completion-history.json` stores completion events and pending Undo snapshots.
5. New completion mutations store `UploadAfter` one hour ahead; scheduled/background upload respects that deadline, while an explicit manual Sync bypasses it.
6. Manual Sync uploads every queued mutation in task/order sequence, retrieves sheet tasks, and merges by stable Task ID.
7. The retired `watch-list.json` is preserved but is not read or modified.

## UI Structure

The workspace contains:

- A segmented Tasks, Priority, Daily Summary, and History main-view switch.
- Header actions for check-in, settings, Conflicts, New Task, and Sync.
- Compact Category, Type, Search Task, Status, and Clear controls in Tasks view.
- A `Paused (N)` manager beside the filters for dated/indefinite pause, Resume, and Edit.
- Task grid with single-click selection, double-click detail, a leading Level/History-count/Alert/Sync/conditional-Pause/Status state column, one Next Date column, and a 10-block Cycle timeline.
- Conditional row expansion with separate subordinate Minor Tasks and Linked Tasks sections plus an amber overdue-minor count, and one stateful icon command that expands or collapses every eligible row in the current view.
- Side-by-side Priority tables for Expired and Warning tasks with Category, Type, Task, relevant date, and signed numeric Days columns whose light-to-dark background represents Level 1-5.
- Priority details open by row double-click and dock below both tables so they do not cover Warning rows; later single-click selections update the open detail, whose Remark editor supports multiline text.
- Full-width Daily Summary view limited to Alert-enabled tasks, with expired, warning, and snoozed groups plus snooze extension actions.
- Month-browsable History grid combining local completion actions with stable Google Sheet Audit rows; per-row Undo remains available only while the associated local mutation is safely pending.
- Task completion sidebar.
- Task editor with link/minor management and a scrollable body with fixed Save/Cancel actions; the ordinary task sidebar owns inline minor completion below Remark, while pause management and conflict review retain their overlays.
- Task editor requires a Level from 1-5 (5 highest), while Category and Type fields are editable searchable ComboBoxes that accept new values; their labels share a row with the inputs, and Save title-cases Category, Type, and Task.
- Settings overlay for Google connection, log retention, and check-in schedule.

## Sorting And Rendering

- Tasks sort by Category, Type, Task.
- Priority separates active unsnoozed Expired and Warning tasks into two tables, each ordered by Level descending, Day Left ascending, Category, Type, and Task.
- Grid columns are not user-sortable. Day Left and Day Passed remain display values beside their dates.
- Filters apply immediately but refresh only `TasksView`; they do not recalculate date fields, rebuild Daily Summary, or reapply sort descriptions.
- The filter toolbar remains compact and left-aligned at all supported window widths.
- Cached tasks are inserted with one collection reset so startup sorting, filtering, and grid layout run once rather than once per row.
- Startup publishes Tasks before hidden peer-view data. After a 150 ms first-render delay, Priority and Daily Summary build from snapshots on a worker task while History is applied from its local event store; their view commands stay disabled and show loading labels until ready.
- Sort-description changes use `TasksView.DeferRefresh()`, and the main task grid uses row/column virtualization with recycling.
- Task-grid text and template cells share the same vertical centering and horizontal inset.

## Task Grid Display

- Column order is State, Category, Type, Task, Next Date, Cycle, Last Executed Date, Remark.
- Next Date selects Warning, Expiry, active Snooze, or the next eligible Google Task reminder and identifies it with a stable icon.
- Next Date uses signed compact metrics; overdue values are negative. The post-expiry alert calculation mirrors the Apps Script stage key and snooze-adjusted seven-day cadence.
- Cycle divides Last Executed Date through Expiry into 10 blocks and switches to red/grey overdue aging after expiry.
- Last Executed Date shows elapsed days as a positive bracketed number.
- State contains tooltip-labelled indicators for Level, History, Alert, Sync, conditional Pause, and Status. Paused rows visible in `ALL` use a muted background; the Pause tooltip shows whether the pause is indefinite or its resume date.
- Selected cells use a light-blue background, dark foreground, and blue border so ordinary and custom-styled columns retain readable contrast.
