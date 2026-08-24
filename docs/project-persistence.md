# Project Persistence

## Runtime Folder

Runtime files live under `AppContext.BaseDirectory`, normally the build output folder:

- Data: `<build output>\data`
- Logs: `<build output>\log`

## Persisted Files

### `data\config.json`

Stores the Google Apps Script URL, API key, and log retention days. It is created automatically when missing.

### `data\tasks.json`

Local Google Sheet task cache.

- Google Sheet remains the source of truth.
- Local task actions update this cache before upload.
- Pending tasks stay visible until acknowledged.
- Reminder and snooze metadata are cached with each task.
- Level is stored as an integer from 1-5; legacy cache rows without Level use 1.
- Link source, linked activation state, pause state/resume date, and nested minor definitions are cached with each full task. Paused and locked tasks remain in the cache for management even though active views exclude them.

### `data\task-filters.json`

Caches ALL/DEFAULT/custom definitions, the single favourite, and memberships. Manage Filters edits an in-memory copy; only Save atomically replaces this local configuration, while Close or Escape leaves the file unchanged. Legacy pending-upload metadata is ignored and normalized on the next Save.

### `data\task-sync-queue.json`

Durable ordered outbox for create, edit, complete, pause, resume, snooze, clear-snooze, and archive operations. Entries include an operation ID, expected revision, payload, state, optional conflict snapshot, and optional `UploadAfter`. Completion payloads include selected minor IDs/dates, and new completion entries set `UploadAfter` one hour after local completion.

### `data\completion-history.json`

Stores local completion actions and synchronized Google Sheet Audit rows. New local records include the associated operation ID, completed date, task display fields, minor completion summary, state, and parent/follower pre-completion snapshots. The snapshots support atomic Undo only while that exact compound completion is Pending and no later mutation exists for any affected task.

### `data\checkin-settings.json`

Stores last check-in, last alert date, and the enabled/HHmm schedule for each day. Defaults to every day at `1200`.

### `data\watch-list.json`

Retired compatibility data from the former Watch List. Current code preserves the file without reading, modifying, pruning, or deleting it.

## Logs

- `log\lifesync-info-yyyy-MM-dd.log`
- `log\lifesync-warning-error-yyyy-MM-dd.log`

Startup pruning removes matching log files older than `logRetentionDays`.

## Must Persist

- Apps Script URL and API key.
- Log retention.
- Task cache and mutation queue.
- Completion history and pending Undo snapshots.
- App-managed links, pause state, and minor definitions/completion state.
- Existing retired Watch List data must remain untouched.
- Check-in schedule and last check-in/alert dates.

## Must Not Persist

- Open overlays or sidebars.
- Current selection.
- Canceled drafts.
- Current filters.
- Current Tasks/Priority/Daily Summary/History view.
- Grid widths or sort direction.
- Transient busy flags.
