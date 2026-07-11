# Life Sync Personal Tracker

Windows WPF app for stable-ID, Google Sheet-backed recurring life tasks.

## Documentation

- [Project Architecture](docs/project-architecture.md)
- [Project Requirements](docs/project-requirements.md)
- [Project Flows](docs/project-flows.md)
- [Project Persistence](docs/project-persistence.md)
- [Project Settings](docs/project-settings.md)
- [Project API Contract](docs/project-api-contract.md)
- [Task Tracker Alert Export Contract](docs/task-tracker-alert-export-contract.md)
- [Project Development Rules](docs/project-development-rules.md)
- [Current Code Review](docs/code-review-current.md)

## Quick Setup

1. Build and run on Windows with the Windows SDK available.
2. Configure the deployed Google Apps Script URL and API key in the app.
3. Deploy and migrate the production Apps Script package in `apps-script`.
4. Press Sync to push queued changes and retrieve Google Sheet tasks.

Runtime data is stored under the build output folder, not the repo root:

- `data\config.json`
- `data\tasks.json`
- `data\task-sync-queue.json`
- `data\checkin-settings.json`
- `data\track-items.json`
- `data\track-options.json`
- `data\track-settings.json`
- `log\lifesync-info-yyyy-MM-dd.log`
- `log\lifesync-warning-error-yyyy-MM-dd.log`

Important: keep [apps-script](apps-script), [Services/GoogleSheetClient.cs](Services/GoogleSheetClient.cs), and [docs/project-api-contract.md](docs/project-api-contract.md) aligned before deploying Apps Script changes.

## Current Project State

TASK is the active product surface. TRACK is hidden and dormant; its existing local JSON remains untouched for possible future recovery.

Current TASK behavior:

- Sync is manual: queued local mutations upload first, then Google Sheet tasks are pulled and merged by stable Task ID.
- Create, edit, complete, snooze, clear-snooze, and archive save locally first and upload in the background.
- The task grid uses single-click for selection and double-click to open detail.
- Daily Summary shows expired/overdue and warning tasks as readable cards with quick snooze actions.
- Status filters are `ALL`, `Normal`, `Warning`, `Expired`, `Pending`, and `Warning + Expired`.
