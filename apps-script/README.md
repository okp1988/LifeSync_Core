# LifeSync Google Apps Script

This folder is the production source for the Google Sheet-backed TASK API and Google Tasks reminders. It does not access the live sheet until you place it in the bound Apps Script project and manually run a function.

## Deployment Order

1. Back up the Apps Script project files.
2. Replace the project files with this folder's `.gs` files and `appsscript.json`.
3. Run `setLifeSyncApiToken` once, changing its value first if the LifeSync API key is not `LIFESYNC`.
4. Run `migrateLifeSyncTaskSchema` once. It creates timestamped backups for Tasks, Audit, and existing app-managed sheets, then creates/protects the hidden Minor Tasks, Filters, and Filter Memberships sheets when needed.
5. Review the returned migration result. Confirm task rows have hidden link/pause system columns, Task ID and Level 1-5, confirm all protected app-managed sheets exist, and review `auditRowsBackfilled`; only legacy Audit rows with one unique Category/Type/Task match receive a Task ID automatically.
6. Run `setupLifeSyncTriggers` once. It removes the obsolete Google Task return trigger and installs one daily reminder trigger.
7. Deploy a new web-app version using the existing execute/access settings.
8. Update the URL/key in LifeSync if needed, then press Sync.

Do not sort or edit the hidden system columns. Normal task columns may still be edited directly; `onEdit` assigns IDs and increments revisions.

Level is stored in the protected system columns. LifeSync validates it as an integer from 1-5, where 5 is the highest priority.

Predecessor/link activation, Pause/Resume Date, minor definitions, filters, and memberships are app-managed. Do not edit hidden system columns or protected system sheets directly.

LifeSync mutation payload dates are sent as `yyyy-MM-dd`. Keep Apps Script date parsing noon-safe and date-only friendly for `executeDate` and `snoozeUntil`.

## Reminder Behavior

- Warning: one Google Task after the warning date is reached and before expiry.
- A snooze before the warning date creates one Snooze End task on that date; a snooze ending during the warning period creates the delayed Warning task on that date.
- A snooze ending on expiry creates one Expired task. A snooze ending after expiry delays the Expired/Overdue task until the snooze date.
- Expired: one Google Task from the effective expiry-reminder date through six days after it.
- Overdue: one Google Task for each seven-day slot. A post-expiry snooze resets this cadence from Snooze Until.
- While today is before Snooze Until, Google Task creation remains blocked.
- Effectively paused tasks and locked linked followers never create Google Tasks. A dated pause becomes eligible again on its Resume Date with its original dates unchanged.
- Google Task completion is not read back into the sheet.

## Manual Checks

- Create a blank recurring task through LifeSync and confirm it has no dates until first completion.
- Sort the sheet and complete the task again; the Task ID, not row number, must be used.
- Edit the same task in the sheet before uploading an offline LifeSync edit and confirm a revision conflict appears.
- Run `checkExpiredAndCreateGoogleTask` twice and confirm the second run creates no duplicate for the same stage key.
- Check snooze end before Warning, during Warning, on Expiry, and after Expiry. Confirm exactly one task is created on Snooze Until and post-expiry reminders resume seven days later.
- Link one active and one inactive follower to a source. Confirm source completion activates only the locked follower, follower completion locks it again, and replaying the same operation ID is idempotent.
- Complete selected minors with different dates and verify only those rows change in the protected Minor Tasks sheet and the parent Audit row carries one summary.
- Verify dated/indefinite pause and manual resume suppress reminders without changing warning or expiry dates.
