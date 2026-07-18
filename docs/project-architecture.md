# Project Architecture

## Application Shape

Life Sync Personal Tracker is a Windows-only WPF desktop app targeting `net8.0-windows10.0.19041.0`. TASK is the only workspace. It manages recurring tasks backed by Google Sheet, with a local cache, durable mutation queue, manual Sync, daily summary, check-in reminders, snooze, and Google Task reminder metadata.

## Main Components

- `MainWindow.xaml`: TASK toolbar, filters, grid, summary drawer, editors, conflict review, and check-in settings.
- `MainWindow.xaml.cs`: startup and focused UI event wiring.
- `ViewModels/MainViewModel.cs`: task synchronization, local cache/outbox, filters, selection, completion, summary, and check-in workflows.
- `Models/SheetTask.cs`: task data and computed UI state.
- `Models/TaskSyncModels.cs`: mutation, conflict, and task editor contracts.
- `Models/CheckinSettings.cs`: daily check-in schedule and state.
- `Services/GoogleSheetClient.cs`: Google Apps Script HTTP client.
- `Services/JsonFileStore.cs`: local JSON persistence.
- `Services/AppPaths.cs`: canonical runtime paths.
- `Services/AppLogger.cs`: retained info and warning/error logs.
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
4. Sync uploads queued mutations, retrieves sheet tasks, and merges by stable Task ID.
5. Local mutations save cache and outbox state before background upload.

## UI Structure

The TASK workspace contains:

- Header actions for check-in, settings, Summary, Conflicts, New Task, and Sync.
- Category, type, day-left, and status filters.
- Task grid with single-click selection and double-click detail.
- Daily Summary drawer with expired/warning groups and snooze actions.
- Task completion sidebar.
- Task editor and conflict review overlays.
- Check-in settings overlay.

## Sorting

- Default task sort is Category, Type, Task.
- Only Day Left is user-sortable.
- Filters apply immediately.
