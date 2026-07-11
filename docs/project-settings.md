# Project Settings

## Config Settings

`config.json` is edited through app-bound fields and saved before network calls.

- `GoogleAppsScriptUrl`: deployed Google Apps Script web app URL.
- `ApiKey`: shared API key/token sent to Apps Script.
- `LogRetentionDays`: minimum value is 1; invalid values are normalized to 30 at startup or clamped in the property setter.

## Check-In Settings

Check-in settings are edited in the TASK tab overlay.

- Each day has an enabled checkbox.
- Each day uses `HHmm` time text, for example `1200`.
- Save rejects invalid time text.
- Cancel reloads the draft from saved settings.
- The all-days checkbox is tri-state:
  - checked when all days are enabled,
  - unchecked when no days are enabled,
  - indeterminate when mixed.

Reminder behavior:

- The app checks every minute.
- A reminder appears only after today's configured time.
- A reminder appears only once per day.
- Checking in manually prevents another reminder that day.

## Track Settings

TRACK is hidden and dormant in the current app. These settings are preserved for future recovery only.

If TRACK is re-enabled, track settings are edited in the TRACK tab overlay.

Stock settings:

- Low stock threshold.
- Critical stock threshold.
- Out stock threshold.
- One highlight color for each threshold.

Expiry settings:

- Expiry alert rows have amount, unit, and color.
- Units are Day, Week, Month, and Year.
- Rows are normalized and sorted by alert window.
- Empty legacy expiry text is converted into expiry alert rows.
- Expired or expiring-today rows use the expired color.

Supported colors:

- Yellow
- Orange
- Red
- DarkGray
- Green
- Blue

The model maps these names to WPF highlight brush hex values.

## Task Filters

Task filters:

- Category
- Type
- Day Left
- Status

Status choices are hard-coded in `MainViewModel.Statuses`; new `SheetTask.Status` values must be added there or users cannot select them.

Current status choices:

- `ALL`
- `Normal`
- `Warning`
- `Expired`
- `Pending`
- `Warning + Expired`

`Pending` is based on `SyncState`, not date status. It means the task has a local queued, failed, or conflict sync operation.

Day-left choices are hard-coded in `MainViewModel.DayLeftFilters`.

Task filters apply immediately when the selected filter value changes.

## Track Filters

TRACK is hidden and dormant in the current app, so these filters are preserved for future recovery only.

Track filters:

- Home view: Attention, All Items, or Categories
- Category
- Type

Track type choices are hard-coded:

- `Quantity`
- `Change Cycle`

Track filters apply immediately when the selected filter value changes.

## Track Maintenance

TRACK Maintenance is disabled while TRACK is hidden.

If TRACK is re-enabled, Maintenance is available from the TRACK header. It is used for manual repair of old stock records and should not dominate the normal inventory workflow. If old records need repair, the button shows a count.

## Task Tracker Export

The previous Task Tracker alert export path is disabled in the current TASK-first app. Its settings remain in `config.json` for compatibility but should stay `Off`.

If re-enabled later, LifeSync can export TASK and TRACK alerts to a JSON request file for a future Task Tracker importer. TASK and TRACK exports are configured independently with `Off`, `Prompt`, or `Auto`.

- `Off`: LifeSync does not export alert requests.
- `Prompt`: LifeSync shows a direct create-request action for eligible alerts, with no extra confirmation.
- `Auto`: LifeSync writes each eligible alert request once per stable source key.

The export path and optional Task Tracker executable path are stored in `config.json`. The executable path is used only to open Task Tracker after a manual export; LifeSync never edits Task Tracker data.
