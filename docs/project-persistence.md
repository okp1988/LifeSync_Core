# Project Persistence

## Runtime Folder

All runtime files live under `AppContext.BaseDirectory`, which is usually the build output folder:

- Debug example: `bin\Debug\net8.0-windows10.0.19041.0\`
- Data folder: `<build output>\data`
- Log folder: `<build output>\log`

Do not assume runtime JSON files live at the repository root.

## Persisted Files

### `data\config.json`

Stores:

- `googleAppsScriptUrl`
- `apiKey`
- `logRetentionDays`
- Task Tracker alert export modes and paths
- exported Task Tracker alert source keys

This file is created automatically if missing.

### `data\tasks.json`

Stores cached Google Sheet tasks.

Important behavior:

- This is a cache, not the source of truth.
- Manual Sync merges by stable Task ID.
- Mark Complete, snooze, clear-snooze, create, edit, and archive update the cached task locally before upload.
- Cached tasks include sheet-owned summary fields such as `Snooze Until`, `Snooze Note`, and last Google Task reminder metadata.
- Pending cached tasks remain visible and show `SyncState = Pending` until upload succeeds.

### `data\task-sync-queue.json`

Stores ordered create, edit, complete, snooze, clear-snooze, and archive mutations that have not been acknowledged by Google Sheet. Each entry includes an idempotent operation ID, expected server revision, payload, and optional conflict snapshot.

Network failures remain pending across restart. Revision conflicts remain for explicit Keep PC or Use Sheet resolution.

### `data\checkin-settings.json`

Stores:

- `lastCheckinAt`
- `lastAlertDate`
- one setting per day of week
- per-day enabled state
- per-day HHmm reminder time

This file is created with all days enabled at `1200` if missing.

### `data\track-items.json`

Stores local tracker items and their history records.

Important behavior:

- TRACK is hidden and dormant in the current app; this file is preserved but not loaded for normal UI/timer/alert work.
- This is the source of truth for tracker data.
- Quantities are recalculated from history on load.
- History records include action, quantity, location, remark, batch ids, and optional expiry date.
- Add Stock creates batches by writing `BatchId` on `Owned` history rows.
- Use Stock and Put Back link back to those batches through `SourceBatchId`.
- Repair should preserve the existing JSON shape and write only approved fixes.
- Removing a track item removes all of its local history.

Backup behavior:

- Before broad TRACK repair or migration work, copy `track-items.json`, `track-options.json`, and `track-settings.json` to a timestamped backup folder beside the runtime data.
- Before saving approved repair fixes, create a second before-repair backup of `track-items.json`.

### `data\track-options.json`

Stores tracker input suggestions:

- categories
- remarks

The app seeds this file with default categories and remarks if TRACK is re-enabled and the file is missing.

### `data\track-settings.json`

Stores tracker alert behavior:

- stock thresholds
- stock highlight colors
- expiry alert rows
- expired color
- shown expiry alert keys

TRACK expiry reminder processing is disabled while TRACK is hidden. If TRACK is re-enabled, `ShownExpiryAlertKeys` must persist so the app does not repeat the same expiry reminder.

### `data\task-tracker-alert-requests.json`

Stores pending LifeSync alert requests for a future Task Tracker importer.

Important behavior:

- The previous external Task Tracker export flow is disabled in the current TASK-first app.
- LifeSync writes this file only as an inbox handoff and does not expect a result from Task Tracker.
- LifeSync does not edit Task Tracker runtime data.
- Duplicate prevention is based on `sourceKey`, not Google Sheet row number.
- LifeSync keeps exported source keys in `config.json` so the same alert is not queued again after restart.
- The contract is documented in `docs\task-tracker-alert-export-contract.md`.

## Logs

Info logs:

- `log\lifesync-info-yyyy-MM-dd.log`

Warning/error logs:

- `log\lifesync-warning-error-yyyy-MM-dd.log`

Log pruning runs on startup and deletes matching LifeSync log files older than `logRetentionDays`.

## Must Persist

- Google Apps Script URL and API key.
- Log retention setting.
- Cached tasks.
- Check-in schedule and last check-in/alert dates.
- Track items and all history.
- Track category and remark suggestions.
- Track thresholds, colors, expiry alert rows, and shown alert keys.
- Task Tracker alert export settings and exported source keys if that disabled export path is re-enabled later.

## Must Not Persist

- Open/closed sidebar state.
- Current selected task or track item.
- Draft edits that were canceled or never saved.
- Current task and track filters as app settings.
- Grid column widths or sort state.
- `IsLoadingTasks`, `IsMarkingComplete`, and other transient busy flags.
