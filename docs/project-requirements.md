# Project Requirements

## Platform

- Must run as a Windows WPF desktop app.
- Must target `net8.0-windows10.0.19041.0`.
- Must build on a machine with the Windows SDK available.

## Task Requirements

- Load cached tasks from `<build output>\data\tasks.json` on startup.
- Do not call Google Sheet automatically on startup.
- Push pending mutations before pulling Google Sheet tasks during Sync.
- Merge fetched tasks by stable Task ID without overwriting pending optimistic tasks.
- Surface missing or duplicate Task IDs as sync errors.
- Accept modern `{ tasks: [...] }` and legacy `{ data: [...] }` task responses.
- Derive task status locally from completion, expired date, and warning date.
- Call `NotifyCalculatedFieldsChanged()` after changing fields that affect calculated display.
- Identify alert-enabled tasks in both the Tasks grid and Daily Summary.
- Order task-grid columns as Category, Type, Task, Expired Date, Warning Date, Alert Date, Last Executed Date, Remark, and State.
- Display missing task-grid dates as `-`, and display Warning Date as `-` when it is the same date as Expired Date.
- Use Snooze Until as Alert Date and show only a bracketed numeric day value; active days left and elapsed days passed are positive, while no snooze displays `-`.
- Show only bracketed numeric day values beside task-grid dates: days left and days passed are positive, overdue days are negative, and today is zero.
- Combine Alert, Sync, and Status into one compact, non-clickable three-icon State column with descriptive tooltips.
- Keep cached tasks visible until Sync returns canonical sheet state.
- Support `ALL`, `Normal`, `Warning`, `Expired`, `Pending`, and `Warning + Expired` status filters.
- Use single-click for selection and double-click for task detail.
- Keep Daily Summary readable and mouse-wheel scrollable.
- Include active snoozed tasks in a separate Daily Summary section and allow their snooze date to be extended.
- Present Tasks, Watch List, and Daily Summary as peer views without an outer single-item tab control.
- Start each app session in Tasks and do not persist the selected main view.
- Keep task filters compact, left-aligned, and visible only in Tasks.
- Filter the task grid immediately as the user types in Search Task, matching the Task column case-insensitively and combining with other filters.
- Support Normal ordering by Category, Type, Task.
- Support Priority ordering by alert-enabled expired, alert-enabled warning, expired, warning, then all remaining tasks, with Category, Type, Task ties.
- Preserve ordering mode during Sync, reset it with Clear Filters, and begin each app session in Normal.
- Do not allow direct DataGrid column sorting.
- Do not provide a Day Left filter; keep Day Left as a calculated display beside Expired Date and in Watch List.
- Load independent local startup files concurrently and publish cached task rows with one collection reset.
- Keep task-grid text and template content vertically centered with consistent horizontal inset.
- Persist unique Watch List entries locally by stable Task ID and date added.
- Resolve watched task fields from the current cache and order by Category, Type, Task, then Date Added.
- Remove watched tasks on completion or archive, and prune missing IDs only after successful Sync.

## Completion Requirements

- Reset `CompletionDate` to today when a task is selected.
- Ask for confirmation before Mark Complete.
- Update local cache and queue before waiting for the API.
- Save remark and last execution date.
- Shift previous execution dates.
- Calculate the next warning and expired dates from recurring intervals.
- Clear snooze fields and refresh calculated fields.
- Close the task sidebar and upload in the background.
- Keep failed uploads pending.
- Require explicit Keep PC or Use Sheet resolution for revision conflicts.

## Check-In Requirements

- Load `checkin-settings.json` and create defaults when missing.
- Support per-day enablement and HHmm reminder times.
- Check reminders on startup and every minute.
- Show no more than one check-in reminder per day.
- Record both check-in and alert state when the user checks in.
- Edit Google connection, log retention, and check-in schedule through one draft-based Settings dialog.

## Logging Requirements

- Write info and warning/error logs under `<build output>\log`.
- Prune old log files using `LogRetentionDays`.
- Never let logging failures block workflows.
- Never log raw token-bearing requests.
