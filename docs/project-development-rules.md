# Project Development Rules

## Must Do

- Keep runtime data under the build output path unless `AppPaths` and documentation change together.
- Keep Google Sheet communication in `GoogleSheetClient`.
- Keep JSON persistence in `JsonFileStore`.
- Keep task and check-in workflows in `MainViewModel`.
- Call `NotifyCalculatedFieldsChanged()` after task changes that affect computed UI state.
- Save local JSON before background mutation upload.
- Use the WPF dispatcher for UI-bound background updates.
- Redact API tokens from logs.
- Keep `apps-script`, `GoogleSheetClient`, and `docs/project-api-contract.md` aligned.
- Serialize date-only mutation fields as `yyyy-MM-dd`.
- Replace a complete cached task set in one collection notification; do not repopulate the bound task collection row by row.
- Publish Tasks before hidden secondary views at startup; keep Priority, Daily Summary, and History disabled until the delayed snapshot build has applied.
- Keep filter-only work to `TasksView.Refresh()` and reserve calculated-field/summary rebuilds for date or task-data changes.
- Batch sort changes with `TasksView.DeferRefresh()` and preserve task-grid recycling virtualization.
- Keep plain and templated task-grid cells on the shared alignment styles.
- Keep selected task-grid cells on the shared high-contrast selection style.
- Keep New/Edit Category and Type as editable searchable ComboBoxes that exclude `ALL`, accept new values, and save Category, Type, and Task in title case.
- Keep Level constrained to 1-5 and preserve the Priority order: Level descending, Day Left ascending, Category, Type, Task.
- Keep the client Next Date reminder calculation aligned with `apps-script/GoogleTasks.gs`, including snooze-delayed expiry anchors and reminder-key formats.
- Merge synchronized Audit records by operation ID before using the legacy Task ID/date/remark fallback; never attach an ambiguous legacy Audit row during migration.

## Must Not Do

- Do not move runtime JSON to appsettings without redesigning persistence.
- Do not treat `tasks.json` as the source of truth.
- Do not block the UI while waiting for mutation confirmation.
- Do not fetch Google Sheet tasks automatically on startup.
- Do not store secrets in repository files.
- Do not log raw token-bearing requests.
- Do not add task status values without updating `MainViewModel.Statuses`.
- Do not rebuild Priority or Daily Summary for ordinary filter-only changes.
- Respect `TaskMutation.UploadAfter` during scheduled/background upload. An explicit manual Sync must bypass it and attempt every queued mutation.
- Do not expose Undo for a completion after it syncs, conflicts, is undone, or has a later mutation for the same task.
- Do not let logging failures break workflows.

## Change Checklist

Task behavior:

- Update `SheetTask` computed fields when needed.
- Update selectable status/day-left choices when needed.
- Update `GoogleSheetClient` and Apps Script together for contract changes.
- Verify cache, pending, and conflict behavior.
- Preserve task-editor option filtering, free-text entry, title-case normalization, and horizontal label/input alignment.

Persistence:

- Update `AppPaths`, `JsonFileStore`, and `docs/project-persistence.md` together.
- Consider normalization for existing runtime JSON.

Settings:

- Preserve cancel/save draft behavior.
- Normalize loaded values.
- Update `docs/project-settings.md`.

## Build

Use:

```powershell
dotnet build LifeSyncTaskClient.sln --no-restore
```

The build requires Windows and the Windows SDK. If the primary output is locked, close the running app before rebuilding.
