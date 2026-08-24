# Project Settings

## Config Settings

`config.json` is edited in the combined Settings dialog.

- `GoogleAppsScriptUrl`: deployed Google Apps Script web app URL.
- `ApiKey`: shared token sent to Apps Script.
- `LogRetentionDays`: minimum 1; invalid values normalize to 30 at startup.
- General and check-in values use drafts; Save persists both settings files and Cancel discards changes.

## Check-In Settings

- Each day has an enabled checkbox.
- Each day uses HHmm time text, such as `1200`.
- Save rejects invalid time text.
- Cancel restores the saved settings.
- The all-days checkbox is checked, unchecked, or indeterminate based on enabled days.
- Reminders run at most once per day after the configured time.

## Task Filters

Controls appear in this order:

1. Category
2. Type
3. Search Task
4. Status
5. Clear Filters

Status choices are hard-coded in `MainViewModel.Statuses`:

- `ALL`
- `Normal`
- `Warning`
- `Expired`
- `Pending`
- `Warning + Expired`

`Pending` is derived from mutation sync state. Category, Type, Status, and Search Task combine and apply immediately. Search Task performs a case-insensitive partial match against the Task column. There is no Day Left filter.

Tasks always order by Category, Type, Task. Urgent work is presented in the separate Priority view. Grid columns are not directly sortable.

Tasks displays one Next Date selected from Warning, Expiry, active Snooze, or the next Google Task reminder. Its adjacent 10-block Cycle provides compact progress without changing task sort order.

Filter-only changes refresh the Tasks collection view. They do not rebuild Daily Summary, recalculate date-derived fields, or reapply sort descriptions.

## Task Editor

- Level is a required selection from 1-5; 5 is the highest priority and 1 is the lowest.
- Category and Type use editable ComboBoxes populated from current task values; `ALL` is excluded.
- Typing performs case-insensitive text search, while unmatched text remains valid as a new Category or Type.
- Category and Type labels share a row with their ComboBoxes.
- Save converts Category, Type, and Task to title case for both create and edit flows.
