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
5. Mode
6. Clear Filters

Status choices are hard-coded in `MainViewModel.Statuses`:

- `ALL`
- `Normal`
- `Warning`
- `Expired`
- `Pending`
- `Warning + Expired`

`Pending` is derived from mutation sync state. Category, Type, Status, and Search Task combine and apply immediately. Search Task performs a case-insensitive partial match against the Task column. There is no Day Left filter.

Mode choices:

- `Normal`: Category, Type, Task.
- `Priority`: Expired + Alert, Warning + Alert, Expired, Warning, then all remaining statuses; ties use Category, Type, Task.

Mode remains selected during Sync, Clear Filters resets it to Normal, and startup begins in Normal. Grid columns are not directly sortable.
