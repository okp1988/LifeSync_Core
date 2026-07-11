# AGENTS.md

## Project Shape

- `LifeSyncTaskClient.csproj` is a Windows-only WPF app targeting `net8.0-windows10.0.19041.0`; builds need the Windows SDK available.
- If a default build fails because `LifeSyncTaskClient.exe` is locked by the running app, notify the user to close the app before trying the default build again.
- `MainWindow.xaml` owns the entire UI: toolbar, filters, task grid, daily summary, overlays, and sidebars. `MainWindow.xaml.cs` only wires UI event glue such as startup, Escape, double-click detail opening, mouse-wheel forwarding, date picker input blocking, and grid sorting/selection behavior.
- `ViewModels/MainViewModel.cs` is the workflow hub. Keep synchronization, filtering, local cache/outbox updates, task selection, and completion behavior there unless a change clearly belongs in a service.
- `Models/SheetTask.cs` contains computed UI state: `DayLeft` and `Status` are derived from dates plus `Completed`; call `NotifyCalculatedFieldsChanged()` after changing fields that affect them.
- `Services/GoogleSheetClient.cs` is the only Google Apps Script HTTP client. It accepts both `{ tasks: [...] }` JSON and legacy `{ data: [...] }` row arrays.
- `Services/JsonFileStore.cs`, `Services/AppPaths.cs`, and `Services/AppLogger.cs` define local persistence under the build output folder, not the repo root.
- `apps-script` is the production Apps Script source and migration package. `docs/google-apps-script.js` is only a deprecated pointer kept for older references.

## Runtime Data Contract

- Local runtime files live under `<build output>\data` and `<build output>\log`; current debug data is therefore under `bin\Debug\net8.0-windows10.0.19041.0\`.
- `config.json` stores the deployed Apps Script URL, API key, and log retention. Do not move this to appsettings without changing `AppPaths`/`JsonFileStore`.
- `tasks.json` is a cache, not the source of truth. Pressing **Sync** uploads queued mutations, pulls Google Sheet tasks, and merges by stable `Task ID` without overwriting pending local tasks.
- `task-sync-queue.json` stores pending/conflict create, edit, complete, snooze, clear-snooze, and archive operations.
- The grid status filter values are hard-coded in `MainViewModel.Statuses`; current choices are `ALL`, `Normal`, `Warning`, `Expired`, `Pending`, and `Warning + Expired`.
- The legacy row-array parser maps columns by index: Category `0`, Type `1`, Task `2`, Expired Date `3`, Warning Date `4`, Prev Date 1 `6`, Prev Date 2 `7`, Remark `8`, Completed `9`.
- Production Apps Script preserves user columns A-S, renames old `Track ID` to `Last Google Task ID`, and appends stable system columns such as `Task ID`, `Revision`, `Updated At`, `Archived`, snooze fields, reminder metadata, and `Last LifeSync Operation ID`.

## Completion Flow Rules

- Selecting a task resets `CompletionDate` to `DateTime.Today`, but single-click selection must not open the task sidebar. Main-grid double-click or Summary Open opens detail.
- `Mark Complete` must update local state, save `tasks.json` and `task-sync-queue.json`, close the action UI, and start upload in the background. Do not block other task actions while upload is pending.
- Completion advances the recurring cycle locally: update remark, last executed date, previous dates, next expired date, next warning date, and clear snooze fields.
- Failed uploads stay `Pending`; stale revisions become `Conflict` for explicit Keep PC or Use Sheet resolution. Do not automatically revert optimistic local task changes.

## API Details

- Task fetch is `GET <GoogleAppsScriptUrl>?action=tasks&token=<ApiKey>`.
- Mutations are JSON `POST <GoogleAppsScriptUrl>` with `action`, `token`, `operationId`, `taskId`, `expectedRevision`, and `payload`.
- Date-only mutation fields such as `executeDate` and `snoozeUntil` must serialize as `yyyy-MM-dd` for Apps Script parsing.
- `GoogleSheetClient` treats HTTP failures, JSON `{ success: false }`, and HTML/script error bodies as failures.
- Log token-bearing requests only in redacted or token-free form.
