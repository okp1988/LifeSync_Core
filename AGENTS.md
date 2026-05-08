# AGENTS.md

## Project Shape

- `LifeSyncTaskClient.csproj` is a Windows-only WPF app targeting `net8.0-windows10.0.19041.0`; builds need the Windows SDK available.
- `MainWindow.xaml` owns the entire UI: toolbar, filters, task grid, and right-side task sidebar. `MainWindow.xaml.cs` only wires startup and Escape-to-close-sidebar behavior.
- `ViewModels/MainViewModel.cs` is the workflow hub. Keep request, filtering, local cache updates, task selection, and completion behavior there unless a change clearly belongs in a service.
- `Models/SheetTask.cs` contains computed UI state: `DayLeft` and `Status` are derived from dates plus `Completed`; call `NotifyCalculatedFieldsChanged()` after changing fields that affect them.
- `Services/GoogleSheetClient.cs` is the only Google Apps Script HTTP client. It accepts both `{ tasks: [...] }` JSON and legacy `{ data: [...] }` row arrays.
- `Services/JsonFileStore.cs`, `Services/AppPaths.cs`, and `Services/AppLogger.cs` define local persistence under the build output folder, not the repo root.
- `docs/google-apps-script.js` is the matching Apps Script sample and documents the sheet header contract used by the client.

## Runtime Data Contract

- Local runtime files live under `<build output>\data` and `<build output>\log`; current debug data is therefore under `bin\Debug\net8.0-windows10.0.19041.0\`.
- `config.json` stores the deployed Apps Script URL, API key, and log retention. Do not move this to appsettings without changing `AppPaths`/`JsonFileStore`.
- `tasks.json` is a cache, not the source of truth. Pressing **Request** replaces the entire local task cache with the Google Sheet response.
- The grid status filter values are hard-coded in `MainViewModel.Statuses`; add new `SheetTask.Status` values there or they cannot be selected from the UI.
- The legacy row-array parser maps columns by index: Category `0`, Type `1`, Task `2`, Expired Date `3`, Warning Date `4`, Prev Date 1 `6`, Prev Date 2 `7`, Remark `8`, Completed `9`.
- The Apps Script sample expects headers in this order: `Category`, `Type`, `Task`, `Expired Date`, `Warning Date`, `Day Left`, `Prev Date 1`, `Prev Date 2`, `Remark`, `Completed`.

## Completion Flow Rules

- Selecting a task resets `CompletionDate` to `DateTime.Today`.
- `Mark Complete` must not remove the row from the local cache. It sets `Completed = true`, updates the local remark and last executed date, refreshes the view, saves `tasks.json`, and closes the sidebar.
- Completion posts to the API in the background. Successful responses stay quiet; failures are logged and surfaced through `Message` on the WPF dispatcher.
- Do not make the UI wait for the completion API response before updating local state.
- Completed cached tasks remain visible until the user manually presses **Request**; the server-side Apps Script filters completed rows out of future task fetches.

## API Details

- Task fetch is `GET <GoogleAppsScriptUrl>?action=tasks&token=<ApiKey>`.
- Completion is `POST <GoogleAppsScriptUrl>` with form fields: `action=complete`, `token`, `rowid`, `executedate` as `yyyy-MM-dd`, and `remark`.
- `GoogleSheetClient.EnsureSuccess` treats HTTP failures, JSON `{ success: false }`, `{ ok: false }`, non-empty `error`, and some HTML/script error bodies as failures.
- Log token-bearing POST payloads only through `RedactToken`; the current client redacts the `token=` field before writing logs.