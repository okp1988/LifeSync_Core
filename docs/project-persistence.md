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

### `data\task-sync-queue.json`

Durable ordered outbox for create, edit, complete, snooze, clear-snooze, and archive operations. Entries include an operation ID, expected revision, payload, state, and optional conflict snapshot.

### `data\checkin-settings.json`

Stores last check-in, last alert date, and the enabled/HHmm schedule for each day. Defaults to every day at `1200`.

## Logs

- `log\lifesync-info-yyyy-MM-dd.log`
- `log\lifesync-warning-error-yyyy-MM-dd.log`

Startup pruning removes matching log files older than `logRetentionDays`.

## Must Persist

- Apps Script URL and API key.
- Log retention.
- Task cache and mutation queue.
- Check-in schedule and last check-in/alert dates.

## Must Not Persist

- Open overlays or sidebars.
- Current selection.
- Canceled drafts.
- Current filters.
- Grid widths or sort direction.
- Transient busy flags.
