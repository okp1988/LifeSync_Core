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
- Keep cached tasks visible until Sync returns canonical sheet state.
- Support `ALL`, `Normal`, `Warning`, `Expired`, `Pending`, and `Warning + Expired` status filters.
- Use single-click for selection and double-click for task detail.
- Keep Daily Summary readable and mouse-wheel scrollable.

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

## Logging Requirements

- Write info and warning/error logs under `<build output>\log`.
- Prune old log files using `LogRetentionDays`.
- Never let logging failures block workflows.
- Never log raw token-bearing requests.
