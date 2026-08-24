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
- `data\completion-history.json`
- `data\watch-list.json`
- `data\checkin-settings.json`
- `log\lifesync-info-yyyy-MM-dd.log`
- `log\lifesync-warning-error-yyyy-MM-dd.log`

Keep `apps-script`, `Services/GoogleSheetClient.cs`, and `docs/project-api-contract.md` aligned before deploying Apps Script changes.

## Current Product

TASK is the only product surface.

- Sync is manual: every queued local mutation uploads first, including completions still inside their one-hour Undo delay, then Google Sheet tasks are pulled and merged by stable Task ID.
- Create, edit, pause, resume, snooze, clear-snooze, and archive save locally first and upload in the background. New completions wait one hour for automatic upload unless the user presses Sync.
- The task grid uses single-click for selection and double-click to open detail.
- Tasks has saved `DEFAULT`, `ALL`, and custom views above Category, Type, Search Task, Status, and Clear. `DEFAULT` hides paused and locked followers; `ALL` keeps linked groups together in hierarchy order.
- Task filtering refreshes only the Tasks collection view; date-derived fields and Daily Summary rebuild only when the date or task data changes.
- Tasks always order by Category, Type, Task. Priority is a separate peer view with Expired and Warning tables ordered by Level (5 highest), Day Left, Category, Type, and Task.
- Priority hides the Level column and uses a light-to-dark background on the numeric Days cell for Level 1-5; positive values are days left and negative values are overdue.
- The leading State column shows Level, current-month Audit count, Alert, Sync, a conditional Pause badge, and Status. Paused rows in `ALL` use a muted background, and the Pause tooltip identifies an indefinite pause or its resume date. The Audit badge uses a compact count such as `3×`.
- Warning, Expired, and Alert dates share one icon-led Next Date column with signed day values. A 10-block Cycle column marks Warning, Today, progress after Warning, expiry, and each 10 overdue days through 100 days.
- Selected task rows use a light-blue, dark-text palette so custom date and state colours remain readable.
- New/Edit Task includes a required Level from 1-5, where 5 is highest. It uses searchable editable Category and Type lists, accepts new values, aligns each label with its ComboBox, and saves Category, Type, and Task in title case.
- Saving cycle changes on an active task immediately recalculates Warning and Expired dates from its last execution date.
- Tasks, Priority, Daily Summary, and History share one segmented main-view switch. Priority places Expired and Warning tables side by side; double-click opens the taller bottom detail area, then single-click changes its task without covering either table.
- Daily Summary shows only tasks with Create Google Task alerts enabled, across Expired, Warning, and Snoozed sections.
- History persists completion actions, imports stable Audit rows during Sync, and provides previous/next month navigation. Each eligible unsynced row has Undo; synchronized rows do not.
- Tasks may expand to show app-managed Minor Tasks and linked followers without adding permanent grid columns. One icon button expands or collapses every eligible row in the current view. Locked followers, paused tasks, and minors stay out of active views and reminders.
- The task editor keeps Save and Cancel fixed while its content scrolls; minor rows leave clearance beside their Remove action and scrollbar. The task sidebar lists minor checkboxes below Remark with compact `E` expiry, `L` last-completion, and `I` interval values. Each checked minor defaults to the main Complete Date but keeps an independently editable date. Mark Complete always completes the main task without another panel.
- A follower selects its Main Task / Unlocked By task in the searchable editor. Links may branch and continue to any depth while cycles are rejected; each task keeps its own countdown dates.
- Manage Filters provides task search and detailed membership rows. Add, rename, delete, favourite, and membership changes remain in memory until Save writes the local filter configuration; Close or Escape discards the draft. Custom views hide locked followers, while `ALL` retains the complete hierarchy. `Paused (N)` supports dated or indefinite pause; pause works bottom-up and resume works top-down.
- Startup reads local files concurrently and publishes cached Tasks in one batch; it does not contact Google Sheet. Priority, Daily Summary, and History build after the first Tasks render and remain disabled with loading labels until ready.
- The retired `watch-list.json` file is preserved but no longer read or modified.
- Google Apps Script creates Google Task reminders for eligible warning, expired, and overdue stages.
- Snooze delays Google Task creation until Snooze Until. If it extends beyond expiry, the overdue seven-day reminder cadence restarts from the snooze end date.
- Check-in reminders use a local per-day schedule.
