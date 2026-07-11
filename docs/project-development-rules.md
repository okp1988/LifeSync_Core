# Project Development Rules

## Must Do

- Keep runtime data under the build output path unless `AppPaths` and all related docs are changed together.
- Keep Google Sheet communication in `GoogleSheetClient`.
- Keep local JSON persistence in `JsonFileStore`.
- Keep task workflow state in `MainViewModel`.
- Keep tracker workflow state in `MainViewModel` unless a dedicated service becomes necessary.
- Call `NotifyCalculatedFieldsChanged()` after changing date, completion, quantity, expiry, or settings state that affects computed UI fields.
- Save local JSON immediately after user actions that mutate persisted data.
- Use the WPF dispatcher when updating UI-bound state from background work.
- Redact API tokens before logging request payloads.
- Keep `apps-script`, `GoogleSheetClient`, and `docs/project-api-contract.md` in sync.
- Keep task mutation date-only fields serialized as `yyyy-MM-dd`.

## Must Not Do

- Do not update Markdown or documentation files during normal development unless the user explicitly asks for documentation changes.
- Do not move runtime JSON to appsettings without redesigning `AppPaths` and `JsonFileStore`.
- Do not treat `tasks.json` as the source of truth.
- Do not block the UI while waiting for task mutation API confirmation.
- Do not make task fetch run automatically at startup.
- Do not store secrets in repository files.
- Do not log raw token-bearing payloads.
- Do not silently add new task status values without updating `MainViewModel.Statuses`.
- Do not add tracker actions that bypass history recalculation.
- Do not let logging failures break app startup or user workflows.

## Change Checklist

When changing task behavior:

- Update `SheetTask` computed fields if needed.
- Update `MainViewModel.Statuses` or `DayLeftFilters` if new values are user-selectable.
- Update `GoogleSheetClient` and Apps Script together for API changes.
- Verify `tasks.json` cache behavior.
- Verify `task-sync-queue.json` pending/conflict behavior.

When changing tracker behavior:

- Update `TrackItem.NotifyCalculatedFieldsChanged()` for new computed fields.
- Recalculate quantities from history after history mutations.
- Save `track-items.json` after user mutations.
- Update `track-settings.json` semantics if alert behavior changes.

When changing persistence:

- Update `AppPaths`.
- Update `JsonFileStore`.
- Update `docs/project-persistence.md`.
- Consider migration or normalization for existing user JSON files.

When changing settings:

- Preserve cancel/save draft behavior.
- Normalize loaded legacy values.
- Update `docs/project-settings.md`.

Documentation exception:

- Only update Markdown files when the user explicitly requests docs updates. If code changes make docs stale during normal development, mention the stale docs in the final response instead of editing them automatically.

## Build Notes

- Build requires Windows and the correct Windows SDK.
- A normal validation command is:

```powershell
dotnet build LifeSyncTaskClient.sln
```

If the Windows SDK is missing, build errors are environment issues rather than app logic issues.

If the default build fails because `LifeSyncTaskClient.exe` is locked by a running app instance, tell the user to close the app before trying the default build again.

If sandboxed build access to the Windows SDK is denied, rerun the same build with the required approval rather than changing project files.
