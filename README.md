# Life Sync Personal Tracker

Windows WPF app for stable-ID, Google Sheet-backed recurring life tasks.

## Documentation

- [Project Architecture](docs/project-architecture.md)
- [Project Requirements](docs/project-requirements.md)
- [Project Flows](docs/project-flows.md)
- [Project Persistence](docs/project-persistence.md)
- [Project Settings](docs/project-settings.md)
- [Project API Contract](docs/project-api-contract.md)
- [Project Development Rules](docs/project-development-rules.md)
- [Current Code Review](docs/code-review-current.md)

## Quick Setup

1. Build and run on Windows with the Windows SDK available.
2. Configure the deployed Google Apps Script URL, API key, log retention, and check-in schedule from Settings.
3. Deploy and migrate the production Apps Script package in `apps-script`.
4. Press Sync to push queued changes and retrieve Google Sheet tasks.

Runtime data is stored under the build output folder:

- `data\config.json`
- `data\tasks.json`
- `data\task-sync-queue.json`
- `data\watch-list.json`
- `data\checkin-settings.json`
- `log\lifesync-info-yyyy-MM-dd.log`
- `log\lifesync-warning-error-yyyy-MM-dd.log`

Keep `apps-script`, `Services/GoogleSheetClient.cs`, and `docs/project-api-contract.md` aligned before deploying Apps Script changes.

## Current Product

TASK is the only product surface.

- Sync is manual: queued local mutations upload first, then Google Sheet tasks are pulled and merged by stable Task ID.
- Create, edit, complete, snooze, clear-snooze, and archive save locally first and upload in the background.
- The task grid uses single-click for selection and double-click to open detail.
- Tasks filters are ordered Category, Type, Search Task, Status, Mode, and Clear; search filters the Task column immediately.
- Normal mode orders Category, Type, Task. Priority mode promotes alert-enabled expired and warning tasks before other expired, warning, and remaining tasks.
- Task dates use compact bracketed day numbers, and Alert/Sync/Status share one three-icon State column.
- Watch List keeps local task bookmarks and resolves their display from the latest cached Sheet data.
- Tasks, Watch List, and Daily Summary share one segmented main-view switch; Daily Summary groups expired, warning, and snoozed tasks with quick snooze actions.
- Startup reads local files concurrently and publishes cached task rows in one batch; it does not contact Google Sheet.
- Google Apps Script creates Google Task reminders for eligible warning, expired, and overdue stages.
- Check-in reminders use a local per-day schedule.
