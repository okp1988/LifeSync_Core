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
- Keep plain and templated task-grid cells on the shared alignment styles.

## Must Not Do

- Do not move runtime JSON to appsettings without redesigning persistence.
- Do not treat `tasks.json` as the source of truth.
- Do not block the UI while waiting for mutation confirmation.
- Do not fetch Google Sheet tasks automatically on startup.
- Do not store secrets in repository files.
- Do not log raw token-bearing requests.
- Do not add task status values without updating `MainViewModel.Statuses`.
- Do not let logging failures break workflows.

## Change Checklist

Task behavior:

- Update `SheetTask` computed fields when needed.
- Update selectable status/day-left choices when needed.
- Update `GoogleSheetClient` and Apps Script together for contract changes.
- Verify cache, pending, and conflict behavior.

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
