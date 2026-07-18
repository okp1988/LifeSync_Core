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
2. Configure the deployed Google Apps Script URL and API key in the app.
3. Deploy and migrate the production Apps Script package in `apps-script`.
4. Press Sync to push queued changes and retrieve Google Sheet tasks.

Runtime data is stored under the build output folder:

- `data\config.json`
- `data\tasks.json`
- `data\task-sync-queue.json`
- `data\checkin-settings.json`
- `log\lifesync-info-yyyy-MM-dd.log`
- `log\lifesync-warning-error-yyyy-MM-dd.log`

Keep `apps-script`, `Services/GoogleSheetClient.cs`, and `docs/project-api-contract.md` aligned before deploying Apps Script changes.

## Current Product

TASK is the only product surface.

- Sync is manual: queued local mutations upload first, then Google Sheet tasks are pulled and merged by stable Task ID.
- Create, edit, complete, snooze, clear-snooze, and archive save locally first and upload in the background.
- The task grid uses single-click for selection and double-click to open detail.
- Daily Summary shows expired and warning tasks with quick snooze actions.
- Google Apps Script creates Google Task reminders for eligible warning, expired, and overdue stages.
- Check-in reminders use a local per-day schedule.
