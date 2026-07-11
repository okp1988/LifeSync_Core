# LifeSync Google Apps Script

This folder is the production source for the Google Sheet-backed TASK API and Google Tasks reminders. It does not access the live sheet until you place it in the bound Apps Script project and manually run a function.

## Deployment Order

1. Back up the Apps Script project files.
2. Replace the project files with this folder's `.gs` files and `appsscript.json`.
3. Run `setLifeSyncApiToken` once, changing its value first if the LifeSync API key is not `LIFESYNC`.
4. Run `migrateLifeSyncTaskSchema` once. It creates timestamped `Tasks` and `Audit` backup tabs before changing headers or data.
5. Review the returned migration result and confirm all existing rows have a hidden Task ID.
6. Run `setupLifeSyncTriggers` once. It removes the obsolete Google Task return trigger and installs one daily reminder trigger.
7. Deploy a new web-app version using the existing execute/access settings.
8. Update the URL/key in LifeSync if needed, then press Sync.

Do not sort or edit the hidden system columns. Normal task columns may still be edited directly; `onEdit` assigns IDs and increments revisions.

LifeSync mutation payload dates are sent as `yyyy-MM-dd`. Keep Apps Script date parsing noon-safe and date-only friendly for `executeDate` and `snoozeUntil`.

## Reminder Behavior

- Warning: one Google Task after the warning date is reached and before expiry.
- Expired: one Google Task from expiry through six overdue days.
- Overdue: one Google Task for each seven-day overdue slot.
- Snooze is inclusive and blocks both summary visibility and Google Task creation.
- Google Task completion is not read back into the sheet.

## Manual Checks

- Create a blank recurring task through LifeSync and confirm it has no dates until first completion.
- Sort the sheet and complete the task again; the Task ID, not row number, must be used.
- Edit the same task in the sheet before uploading an offline LifeSync edit and confirm a revision conflict appears.
- Run `checkExpiredAndCreateGoogleTask` twice and confirm the second run creates no duplicate for the same stage key.
