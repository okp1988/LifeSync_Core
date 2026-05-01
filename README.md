# LifeSync Task Client

Windows WPF app for reading task rows from a Google Sheet, caching them locally as JSON, editing the selected task remark, and marking a task complete.

## Workflow

1. Open the app.
2. The app reads only the local cache at `<build folder>\data\tasks.json`.
3. Press **Request** to fetch current tasks from Google Sheet and replace the local JSON cache.
4. Select one task, edit **Selected Remark**, then press **Save Remark** or **Mark Complete**.
5. **Mark Complete** saves the remark to Google Sheet first, calls the complete endpoint, then removes the task from local JSON. It does not re-request tasks.

## Google Sheet Columns

The Apps Script sample expects these headers:

- Category
- Type
- Task
- Expired Date
- Warning Date
- Day Left
- Prev Date 1
- Prev Date 2
- Remark
- Completed

`Completed` should be a checkbox column. The app maps returned data rows to sheet row numbers, where the first returned record is row 2, the second is row 3, and so on.

## Setup

1. Copy [docs/google-apps-script.js](docs/google-apps-script.js) into your Google Sheet Apps Script project.
2. Update `SHEET_NAME`, `API_KEY`, and `runCompletionLogic_`.
3. Deploy the script as a web app that the Windows app can access.
4. Put the deployed web app URL and matching API key into the app fields.

The app stores the endpoint and key in `<build folder>\data\config.json`.
Info logs are written to `<build folder>\log\lifesync-info-yyyy-MM-dd.log`.
Warning and error logs are written to `<build folder>\log\lifesync-warning-error-yyyy-MM-dd.log`.
Log retention is controlled by `logRetentionDays` in `<build folder>\data\config.json`.
