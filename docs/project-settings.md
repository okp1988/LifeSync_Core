# Project Settings

## Config Settings

`config.json` is edited through app-bound fields and saved before network calls.

- `GoogleAppsScriptUrl`: deployed Google Apps Script web app URL.
- `ApiKey`: shared token sent to Apps Script.
- `LogRetentionDays`: minimum 1; invalid values normalize to 30 at startup.

## Check-In Settings

- Each day has an enabled checkbox.
- Each day uses HHmm time text, such as `1200`.
- Save rejects invalid time text.
- Cancel restores the saved settings.
- The all-days checkbox is checked, unchecked, or indeterminate based on enabled days.
- Reminders run at most once per day after the configured time.

## Task Filters

- Category
- Type
- Day Left
- Status

Status choices are hard-coded in `MainViewModel.Statuses`:

- `ALL`
- `Normal`
- `Warning`
- `Expired`
- `Pending`
- `Warning + Expired`

`Pending` is derived from mutation sync state. Filters apply immediately.
