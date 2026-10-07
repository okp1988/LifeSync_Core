# LifeSync requirements

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-10-05 13:08 +08:00. Status: History checkpoint processed. Original dates of earlier conversation decisions are unknown. Section timestamps apply to their items. Implementation belongs in [progress](progress.md); documentation policy in [AGENTS](../AGENTS.md).

## Purpose and scope

Recorded: 2026-09-08 12:37 +08:00. Status: confirmed user direction.

- Personal recurring-task tracking with Google Sheet synchronization and Google Task alerts. Preserve Google synchronization when simplifying the app.
- Tasks, Priority, Daily Summary, and History are the main views. Preserve the ordinary Tasks grid, filtering, sorting and responsive scrolling while adding relationships.
- Linked tasks are full normal tasks; minors are subordinate items with optional execution dates/intervals, not full reminder tasks.
- Google Sheets remains the synchronized source of truth for full tasks. Links, pause state and minors are app-managed. A protected Apps Script sheet may store minors. Direct Sheet editing of minors, filters and membership is not needed; see remote filter uncertainty below.

## Task viewing and Priority

Recorded: 2026-09-08 12:37 +08:00. Status: confirmed; later corrections supersede earlier designs.

- State goes first and includes Level and History icons alongside Alert/Sync/status information. Provide visible paused-task identification. Enabled/disabled History and Alert appearances need clear color contrast.
- Level ranges from 1 to 5; 5 is highest. The State icon is numbered, red at 5 and white at 1, with a black border. Add Level to task details/editor for Priority sorting.
- Priority replaces former Watch List functionality as a peer tab. Remove the mode-switch tab inside Tasks.
- Priority has two tables: Expired left, Warning right. Columns: Category, Type, Task, Due/Warning Date, Days Left/Overdue. Remove the Level column. Highlight only the numeric Days cell dark-to-light for high-to-low level, on both tables; positive remaining, negative overdue.
- Priority sorts Level descending, Days Left ascending, Category, Type, Task.
- Details must not cover either Priority table, especially the right table. Provide enough height and allow multiline remarks.
- Single-click selects/changes task; double-click opens details in Tasks and Priority. Remove the Priority Open Details button.
- Add left/right cell spacing, especially Cycle; always show all 10 blocks. The later request to start at minimum width supersedes repeatedly squeezing/clipping the table when shrinking. No numeric minimum was explicitly confirmed.
- New-task labels must display completely, including Audit. Save/Cancel remain accessible when minors are added; leave spacing between Remove and scrollbar. Scrolling should remain responsive as task count grows.
- Show a compact expander only for tasks with minors or linked followers, with separate Minor Tasks/Linked Tasks sections. One real icon toggles expand-all/collapse-all for eligible rows and must reveal information, not merely change arrows. Preserve row virtualization.

## Combined date and Cycle

Recorded: 2026-09-08 12:37 +08:00. Status: confirmed.

- Merge Warning, Expired and Alert dates into Next Date. Put an identifying icon first, then date; day metrics at the right. Use icons to identify the date kind rather than relying on tooltips.
- Warning: warning date with `(days left to warning | days left to expiry)`. Expiry: expiry date with `(days left to expiry)`. Post-expiry alert: next alert date with `(negative days overdue | days left to next alert)`.
- Grid date metrics use numbers instead of repeated “days left/overdue” wording. Calculate next alert according to Apps Script expiry/snooze/weekly rules.
- Cycle has exactly 10 rectangles. Default light green, Today black before warning; colors other than light green should be darker/distinct.
- Before Warning, show a yellow warning marker. At Warning all blocks become yellow. After Warning, elapsed blocks including Today become blue; future blocks stay yellow.
- At expiry all 10 blocks become red. Every 10 overdue days changes one block to grey; all grey at 100 days.

## Linked full tasks

Recorded: 2026-09-08 12:37 +08:00. Status: confirmed; supersedes original one-level restriction and follower date resetting.

- Configure from the follower by selecting its Main Task/Unlocked By source using a searchable textbox/combobox. One source can have multiple followers; a follower has one source. Chains may continue to arbitrary depth. Reject self-links/cycles.
- Unlocking is directional: Change Bedsheet unlocks Laundry Bedsheet; Laundry does not unlock Change. Laundry can unlock Dry, then Keep.
- A new follower with no active cycle starts locked. Linking an active existing task preserves its cycle until its next completion, when it locks.
- Completing a task restarts its own recurrence. A completed follower locks, retaining its own calculated dates until its source unlocks it.
- Source completion unlocks only locked direct followers, retaining their dates. Already unlocked followers retain their deadlines. Never reverse-unlock the predecessor.
- Locked followers remain outside DEFAULT/custom Tasks, Priority, Daily Summary and Google reminders, even after retained expiry. Unlocking may expose an already overdue task.
- ALL shows the full hierarchy together, root to descendants, with visible relationship identification. Locked followers remain available for management.

## Minor tasks and independent updates

Recorded: 2026-09-08 12:37 +08:00. Status: confirmed; independent updates supersede original “cannot complete independently.”

- Parent editor manages stable minor ID, parent ID, name, optional expiry interval, optional latest completion, calculated due date, display order and archived state.
- Interval and last execution must persist after editing/sync. Interval minors become overdue; no-interval minors only record latest completion and never become overdue.
- Completion controls belong below Remark in the right task sidebar. No separate completion panel and no Select All. Every minor has its own checkbox.
- Checked minors default to main Complete Date, but each date stays editable. Selected interval minors calculate due dates from their own completion date; unchecked minors retain existing completion/due dates.
- Compact labels: `E` expiry, `L` last execution, `I` interval; units `d`, `mth`, `yr` (e.g. 1d, 1mth, 1yr). Display in expanded/completion details with minimal wording.
- Mark Complete completes the parent plus selected minors. Also provide independent selected-minor updates without parent completion/recurrence advancement, and parent Remark updates without completion.
- Amber overdue-minor count appears by parent, less prominent than main Warning/Expired. Minors never enter Priority or Google reminders. Check-in popup explicitly includes related minors as a later exception to the original Daily Summary exclusion.
- Minors completed with the parent appear as a summary in that parent History record, not separate rows.

## Pause and filters

Recorded: 2026-09-08 12:37 +08:00. Status: confirmed.

- Pause can be indefinite or have Resume Date. Automatic return keeps original Warning/Expired dates even if immediately overdue. Paused tasks stay outside active views and reminders.
- Paused (N) near filters opens a manager with paused task, resume date, Resume and Edit. Identify paused rows in ALL.
- Pause bottom-up: reject pause while any follower is unpaused; allow when followers are paused. Resume top-down: predecessor first. Explain which relationship blocks the action. Do not automatically pause separate followers.
- Pause replaces user-facing Archive. Remove Archive from UI; do not delete existing archived data as part of this change.
- ALL, DEFAULT and custom filters are available. DEFAULT normally opens first, hides disabled/inactive tasks and locked followers; favouriting another filter makes it open by default. DEFAULT can be favourited but not edited/deleted.
- Custom filters may select independent tasks as well as linked groups. Paused tasks are permanently unchecked/disabled in filter management.
- New tasks start unchecked in custom filters; DEFAULT includes them only when eligible. Linked followers inherit group/source selection and cannot be independently adjusted. Name mismatched tasks when linking/editing/filtering; no separate “match source filter” operation is needed.
- Checking/unchecking a root follows through descendants. Pausing removes only that task's membership, one by one, not the whole group.
- Manage Filters offers add/edit/delete/favourite, membership checkboxes, search and useful task details. All changes stay in memory until Save writes configuration. Close/Cancel discards drafts.

## Completion, sync and History

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-10-05 13:08 +08:00. Status: confirmed from accepted plan and corrections; monthly overview accepted 2026-10-05, checkpoint processed.

- Completion updates locally and remains usable while Pending. Persist cache, mutation queue and before-state. Failures retain optimistic Pending work; revision conflicts preserve complete compound before-state for explicit resolution.
- Parent completion, selected minors and follower unlocks form one compound local action and server mutation. Responses include all affected full tasks/revisions and merge without overwriting pending editable fields.
- Completion waits one hour before automatic sync. Manual Sync uploads every pending operation, including those inside the delay.
- Every eligible History row has its own Undo. Synced actions have none. Compound Undo restores parent, selected minors and followers atomically. Do not expose unsafe Undo after later affected-task changes or for conflicted/synced records.
- Track Audit actions monthly, with a short count in the main table and detail in History. Do not silently use the early latest-10 request to delete monthly records; retention ambiguity is below.
- History opens a compact monthly overview for both current and past months: one row per task, with Task (Category / Type), completion count and last completed date. Keep month navigation and add search.
- Unsynced individual records stay directly above the overview, newest first, with an Undo button on every eligible row. Keep these records accessible across month boundaries, including backdated completions; the user must not search task by task or open a separate Undo shortcut to find them. Once synced, a record leaves the top section and remains accessible through the monthly details.
- Double-click a summary task to open its individual records for that month, including completion dates, remarks, minor completions and status. Back returns to the same month/search. Do not require a different current-month layout just to preserve Undo access.
- Exclude undone actions from completion counts but retain them in task details. Preserve existing Undo eligibility/safety, saved history and retention; the overview is a viewing change, not permission to delete records.
- Pending actions and failures must be identifiable and explainable so the user can recover instead of seeing only an unexplained count.
- Task fetch includes paused/locked full tasks for management but excludes archived tasks. Preserve restart persistence, retry, idempotent replay and compound conflict/Undo safety.
- Migration makes timestamped backups, appends hidden link/pause columns, creates a protected Minor Tasks sheet. Existing rows default to active, unlinked, unpaused, no minors.

## Google reminders and Daily Summary tab

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-09-11 10:24 +08:00. Status: confirmed 2026-09-09; checkpoint processed; pre-expiry Snooze End creation superseded.

- Warning never creates a Google Task. Creation requires expiry to have been reached AND any snooze to have ended: use the later of Expired Date and Snooze Until.
- Snooze ends before expiry: do not create at snooze end; wait until expiry. Expiry arrives before snooze end: do not create at expiry; wait until snooze ends. Without snooze: create at expiry. Equal expiry/snooze-end dates: create one task.
- Preserve the existing seven-day overdue repeat. A snooze extending beyond expiry resets the cadence from its end date. The app's next-Google-Task date/countdown must follow the same rule.
- Daily Summary contains only Alert-enabled eligible full tasks and shows when the next Google Task will be created and whether one was previously created.
- Sort by days until Google Task creation then task name. Within/across groups interpretation is recorded below.
- Remove separate Snoozed Until date because Next Google Task identifies Snooze End/Expired; keep next event date.
- Append a short Google Task countdown after first-row “X days left”/“X overdue.” Exact abbreviation is an implementation choice, not a separately approved requirement.

## Check-in popup

Recorded: 2026-09-08 12:37 +08:00. Status: confirmed; arithmetic ambiguity below.

- Checking in opens Daily Summary with Expired and Warning groups and related minor rows.
- Columns: Task (Category / Type), Day Passed, Next Date, Last Exec Date. Example `Shirt (House / Laundry)`; minors indented/arrow-prefixed beneath the parent.
- Group Expired then Warning, minors under parents; sort by priority then task name.
- Warning/expired Day Passed is bold red. After warning calculate today minus warning date; after expiry calculate today minus expiry date, with expiry taking precedence.
- Next Date is nearest applicable warning/expiry/snooze-end date. An expired minor includes its main task; expired main task includes its minors.

## Rejected/superseded approaches

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-09-11 10:24 +08:00. Status: processed decisions; do not reintroduce without new request.

- Watch List UI and task mode switch; separate Priority Open Details; sidebar covering right Priority table.
- Permanent Priority Level column, word-heavy grid metrics, clipped cycle blocks, repeated shrink fixes ignoring minimum-width request.
- One-level-only links, source completion resetting follower dates, clearing follower dates on completion, reverse unlocking, source-side follower selection.
- Separate minor panel/Select All, uneditable minor dates, requiring parent completion for every minor/remark update.
- User-facing Archive, independent follower filter toggles, pausing one task removing whole group, filter saves before Save.
- Warning Google reminders, Google Task creation at a pre-expiry Snooze End, and duplicate Daily Summary Snoozed Until badge.
- Frequent/Regular/Long classification, frequency filters/badges/thresholds/sorting remain deferred.

## Unclear or unconfirmed

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-09-11 10:24 +08:00. Status: remaining questions open; reminder timing clarified separately. Do not resolve by assuming code or agent suggestions equal approval.

- History initially mentioned last 10/30 records, later monthly tracking. Display limit versus total retention cap is unsettled. Do not truncate history.
- Linked timing example says only main starts countdown but gives Laundry its own recurring expiry. The operational interpretation above retains each task's dates; confirm any intended departure. Activation of a never-completed follower with no dates also needs a clear rule if changed.
- Snooze reminder timing is now confirmed in Google reminders above. The older phrase “push expired date” did not separately settle whether stored/displayed expiry itself should move; current code retains it. Do not infer an expiry-field change from the confirmed reminder delay.
- Pre-warning Check-in Day Passed was literally requested as `last execute - today (negative value if not yet expired)`. This differs from usual elapsed-days naming. Confirm before changing its interpretation.
- Minor inclusion was explicitly added to check-in popup; inclusion in the ordinary Daily Summary tab was not confirmed.
- Current Daily Summary sort is within groups; global merged ordering was not explicitly chosen. `G Nd` and the 06:00 trigger hour were agent choices, not separately confirmed product requirements.
- Earlier questions about Level editing in Sheets and a one-day Home interval do not establish a new schema contract. Do not assume protected Level must be Sheet-editable. Wiper warning change from 18 months to one year remains live-data verification unknown.
- Initial plan stored filters in protected sheets; later discussion permits app-managed storage. Cross-device filter synchronization was not explicitly required or rejected; current local storage is in progress.
- Old documents include choices without available user confirmation: precise 980/1120 widths, exact status-filter options, title-case normalization, initial blank dates, HHmm defaults, batching delay, and transient UI persistence. These remain implementation evidence.
- History/Undo representation of independent minor updates beyond compound parent completion is not explicitly specified.
