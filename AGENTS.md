# AGENTS.md

## Project Shape

- `LifeSyncTaskClient.csproj` is a Windows-only WPF app targeting `net8.0-windows10.0.19041.0`; builds need the Windows SDK available.
- If a default build fails because `LifeSyncTaskClient.exe` is locked by the running app, notify the user to close the app before trying the default build again.
- `MainWindow.xaml` owns the entire UI: unified Tasks/Priority/Daily Summary/History views, toolbar, filters, grids, overlays, and sidebars. `MainWindow.xaml.cs` only wires UI event glue such as startup, Escape, double-click detail opening, mouse-wheel forwarding, date picker input blocking, and grid selection behavior.
- `ViewModels/MainViewModel.cs` is the workflow hub. Keep synchronization, filtering, local cache/outbox updates, task selection, and completion behavior there unless a change clearly belongs in a service.
- Startup publishes Tasks first. Priority, Daily Summary, and History stay disabled with loading labels until `BuildSecondaryViewsAfterStartupAsync()` finishes its delayed snapshot build; ordinary task filtering must refresh only `TasksView`.
- Daily Summary includes only active tasks with Alert enabled, including its Expired, Warning, and Snoozed sections.
- `Models/SheetTask.cs` contains computed UI state: `DayLeft` and `Status` are derived from dates plus `Completed`; call `NotifyCalculatedFieldsChanged()` after changing fields that affect them.
- `Services/GoogleSheetClient.cs` is the only Google Apps Script HTTP client. It accepts both `{ tasks: [...] }` JSON and legacy `{ data: [...] }` row arrays.
- `Services/JsonFileStore.cs`, `Services/AppPaths.cs`, and `Services/AppLogger.cs` define local persistence under the build output folder, not the repo root.
- `apps-script` is the production Apps Script source and migration package. `docs/google-apps-script.js` is only a deprecated pointer kept for older references.

## Runtime Data Contract

- Local runtime files live under `<build output>\data` and `<build output>\log`; current debug data is therefore under `bin\Debug\net8.0-windows10.0.19041.0\`.
- `config.json` stores the deployed Apps Script URL, API key, and log retention. Do not move this to appsettings without changing `AppPaths`/`JsonFileStore`.
- `tasks.json` is a cache, not the source of truth. Pressing **Sync** uploads every queued mutation, including completion mutations whose `UploadAfter` time has not arrived, then pulls Google Sheet tasks and merges by stable `Task ID` without overwriting pending local tasks.
- `task-sync-queue.json` stores pending/conflict create, edit, complete, snooze, clear-snooze, and archive operations.
- `completion-history.json` stores local completion actions, imported stable Audit rows, and pre-completion snapshots used for pending Undo. History is browsed by month.
- `watch-list.json` is retired compatibility data. Current code does not read, modify, or delete it.
- The grid status filter values are hard-coded in `MainViewModel.Statuses`; current choices are `ALL`, `Normal`, `Warning`, `Expired`, `Pending`, and `Warning + Expired`.
- Task editor Level is a required 1-5 selection (5 highest, 1 lowest). Category and Type are editable, searchable ComboBoxes backed by options that exclude `ALL`; new values remain valid, and Save normalizes Category, Type, and Task to title case.
- Editing an active task's expired or warning cycle recalculates both dates from Last Executed Date, falling back to Prev Date 01 for legacy rows; reject warning dates after expiry.
- Priority tables sort by Level descending, then Day Left ascending, Category, Type, and Task. Missing legacy Level values normalize to 1.
- Tasks places State first. State shows Level, History, Alert, Sync, a conditional Pause badge, and Status icons; Level uses a black-bordered white-to-red scale from 1 to 5. Paused rows in `ALL` use a muted background and expose their resume state through the Pause tooltip.
- The History state badge shows the current-month count as `N×` only for Audit-enabled tasks. Sync merges Audit rows by operation ID, with a legacy semantic fallback.
- Tasks merges Warning, Expired, and Alert dates into Next Date and shows a 10-block Cycle timeline. Overdue values are negative; post-expiry reminder dates follow the Apps Script snooze-adjusted seven-day cadence.
- Tasks defaults to `DEFAULT`; effectively paused tasks and locked linked followers stay cached but are excluded from DEFAULT, custom views, Priority, Daily Summary, and reminders. `ALL` intentionally shows the complete non-archived hierarchy.
- Linked branching may continue to arbitrary depth while self-links and cycles are rejected. Stable minor definitions are app-managed; conditional row expansion shows Minor Tasks and linked followers, one toolbar command expands/collapses every eligible visible row, and minors never enter Priority, Daily Summary, or Google reminders.
- Manage Filters uses an explicit in-memory draft. Only Save atomically writes `task-filters.json`; Close or Escape discards all draft changes.
- The legacy row-array parser maps columns by index: Category `0`, Type `1`, Task `2`, Expired Date `3`, Warning Date `4`, Prev Date 1 `6`, Prev Date 2 `7`, Remark `8`, Completed `9`.
- Production Apps Script preserves user columns A-S, renames old `Track ID` to `Last Google Task ID`, and appends stable system columns such as `Task ID`, `Revision`, `Updated At`, `Archived`, snooze fields, reminder metadata, `Last LifeSync Operation ID`, and `Level`.

## Completion Flow Rules

- Selecting a task resets `CompletionDate` to `DateTime.Today`, but single-click selection must not open the task sidebar. Main-grid double-click or Summary Open opens detail.
- `Mark Complete` must update local state, save `tasks.json` and `task-sync-queue.json`, close the action UI, and start upload in the background. Do not block other task actions while upload is pending.
- Completion advances the recurring cycle locally: update remark, last executed date, previous dates, next expired date, next warning date, and clear snooze fields.
- Failed uploads stay `Pending`; stale revisions become `Conflict` for explicit Keep PC or Use Sheet resolution. Do not automatically revert optimistic local task changes.
- New completion mutations wait one hour before automatic upload. Pressing **Sync** explicitly bypasses that delay and removes Undo eligibility once the completion synchronizes. While the exact completion remains Pending and no later mutation exists for that task, its History row may Undo by restoring the persisted pre-completion snapshot and removing that mutation. Synced, conflicted, legacy, and undone records have no Undo action.
- Completion is compound when minors or locked followers are affected: the main task always completes, one mutation updates the parent, only the minors checked below the sidebar Remark using their independently editable completion dates, and follower activation. Minor dates default to the main Complete Date and no separate minor-completion panel is used. Pending Undo restores all affected snapshots atomically, while conflicts retain the complete before-state.

## API Details

- Task fetch is `GET <GoogleAppsScriptUrl>?action=tasks&token=<ApiKey>`.
- Mutations are JSON `POST <GoogleAppsScriptUrl>` with `action`, `token`, `operationId`, `taskId`, `expectedRevision`, and `payload`.
- Date-only mutation fields such as `executeDate` and `snoozeUntil` must serialize as `yyyy-MM-dd` for Apps Script parsing.
- `GoogleSheetClient` treats HTTP failures, JSON `{ success: false }`, and HTML/script error bodies as failures.
- Log token-bearing requests only in redacted or token-free form.
- Google Task reminders honor Snooze Until as the delayed reminder date. A snooze on expiry creates one task; a snooze after expiry resets the seven-day overdue cadence from its end date.
