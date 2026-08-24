# Project API Contract

## Current C# Client Contract

Task fetch and manual synchronization pull:

```text
GET <GoogleAppsScriptUrl>?action=tasks&token=<ApiKey>
```

All new mutations use JSON and stable identity:

```json
POST <GoogleAppsScriptUrl>
{
  "action": "create|update|complete|pause|resume|snooze|clearSnooze|archive",
  "token": "<ApiKey>",
  "operationId": "<idempotency key>",
  "taskId": "<stable UUID>",
  "expectedRevision": 4,
  "payload": {}
}
```

Successful mutations return the canonical task and `affectedTasks` for compound follower activation. Stale revisions return `success: false`, `errorCode: REVISION_CONFLICT`, and `serverTask`. Row number is metadata and is never used by the new client for writes.

An `update` mutation that changes recurring cycle values recalculates the current Warning and Expired dates from `Last Executed Date`, falling back to `Prev Date 01` for legacy rows. The mutation fails if the resulting warning date is after expiry.

Date-only payload fields must be serialized as `yyyy-MM-dd`, especially:

- `payload.executeDate`
- `payload.snoozeUntil`
- `payload.resumeDate`
- each `payload.minorCompletions[].completionDate`

Apps Script intentionally parses date-only values using noon-safe local conversion. Do not send full .NET JSON datetimes for these fields.

## Task Response Shapes

`GoogleSheetClient` accepts three response shapes.

Raw task array:

```json
[
  {
    "level": 5,
    "category": "House",
    "type": "Renewal",
    "task": "Example",
    "expiredDate": "2026-06-01T00:00:00.000Z",
    "warningDate": "2026-05-20T00:00:00.000Z",
    "previousDate1": "2025-06-01T00:00:00.000Z",
    "previousDate2": null,
    "remark": "",
    "completed": false,
    "alert": true,
    "history": true,
    "rowNumber": 2,
    "snoozeUntil": "2026-05-30",
    "snoozeNote": "Wait until weekend",
    "lastGoogleTaskKey": "lifesync-task-expired-7F3A91C2D4B8",
    "lastGoogleTaskId": "google-task-id",
    "lastGoogleTaskCreatedDate": "2026-05-27"
  }
]
```

Object with task array:

```json
{
  "tasks": [],
  "historyRecords": [
    {
      "recordId": "audit-operation-id",
      "operationId": "operation-id",
      "taskId": "stable-task-id",
      "completedDate": "2026-08-19",
      "recordedAt": "2026-08-19T00:00:00.000Z",
      "category": "House",
      "type": "Renewal",
      "task": "Example",
      "remark": "Completed",
      "state": "Synced"
    }
  ]
}
```

`historyRecords` contains Audit rows with stable Task IDs. The client merges matching operation IDs into its local completion records so synchronized app completions are not duplicated.

Legacy row-array object:

```json
{
  "data": []
}
```

Legacy row indexes:

- Category: `0`
- Type: `1`
- Task: `2`
- Expired Date: `3`
- Warning Date: `4`
- Prev Date 1: `6`
- Prev Date 2: `7`
- Remark: `8`
- Completed: `9`

## Sheet Headers

Production Apps Script preserves user columns A-S and expects these visible task headers:

- `Category`
- `Type`
- `Task`
- `Expired Date`
- `Warning Date`
- `Day Left`
- `Prev Date 1`
- `Prev Date 2`
- `Remark`
- `Completed`
- `Last Google Task ID`

The migration renames old `Track ID` to `Last Google Task ID`, then appends protected system columns:

- `Task ID`
- `Revision`
- `Updated At`
- `Archived`
- `Snooze Until`
- `Snooze Note`
- `Last Google Task Key`
- `Last Google Task Created Date`
- `Last LifeSync Operation ID`
- `Level` (integer 1-5; legacy or missing values default to 1)
- `Predecessor Task ID`
- `Linked Unlocked`
- `Linked Activation Date`
- `Paused`
- `Resume Date`

The protected, hidden `Minor Tasks` sheet stores stable Minor Task ID, Parent Task ID, name, optional interval, latest completion, due date, order, archived state, and last operation ID. Minor completion and due dates use date-only `yyyy-MM-dd` JSON; Apps Script also accepts legacy ISO timestamps. Filter configuration is locally authoritative in `task-filters.json`; existing protected filter sheets and API fields are retained only for deployment compatibility and are not applied by the current client.

The Audit tab uses column H for `Task ID`, column I for `LifeSync Operation ID`, and column J for the parent-level minor completion summary. Migration backfills Task ID only when the Audit row's Category, Type, and Task match exactly one current task; ambiguous rows remain unchanged rather than being attached incorrectly.

The complete source and migration are in `apps-script`.

The Apps Script trigger creates warning, snooze-end, expired, and weekly overdue Google Tasks for alert-enabled rows. Reminder keys use Task ID, cycle expired date, and stage.

Snooze delays reminder creation until `Snooze Until`. A snooze ending before expiry creates the applicable Snooze End or Warning reminder; one ending on or after expiry creates one Expired/Overdue reminder. When Snooze Until is after expiry, weekly overdue slots restart from that snooze date. Existing unsnoozed reminder-key formats remain unchanged.

## Error Handling

`GoogleSheetClient.EnsureSuccess` treats these as failures:

- non-success HTTP status codes,
- JSON `{ "success": false }`,
- JSON `{ "ok": false }`,
- non-empty JSON `error`,
- HTML/script bodies containing error-like text.

## Important Alignment Note

`apps-script` must match this contract. `docs/google-apps-script.js` is deprecated and should only point to the production source. If the Apps Script expects row numbers or form completion while the C# client sends stable Task ID JSON mutations, Sync and task actions will fail.
