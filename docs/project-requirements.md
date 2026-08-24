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
- Keep a failed mutation Pending while continuing the same upload pass with mutations for other task IDs.
- Surface missing or duplicate Task IDs as sync errors.
- Accept modern `{ tasks: [...] }` and legacy `{ data: [...] }` task responses.
- Derive task status locally from completion, expired date, and warning date.
- Call `NotifyCalculatedFieldsChanged()` after changing fields that affect calculated display.
- Identify alert-enabled tasks in both the Tasks grid and Daily Summary.
- Order task-grid columns as State, Category, Type, Task, Next Date, Cycle, Last Executed Date, and Remark.
- Merge Warning, Expired, Snooze, and next Google Task reminder dates into one icon-led Next Date column. Use positive upcoming values, zero today, and negative overdue values.
- Before Warning show `(days to warning | days to expiry)`; before Expiry show `(days to expiry)`; for a post-expiry alert show `(negative days overdue | days to next alert)`.
- Show Cycle as 10 compact blocks with left/right cell spacing: light green by default, with a black Today block and a yellow Warning block before Warning begins. On the Warning date the full 10-block strip turns yellow; after that date, elapsed blocks through Today turn blue while future blocks remain yellow. At expiry all blocks turn red; each 10 overdue days changes one block to grey until all are grey at 100 days.
- At window widths below 1120, keep the primary task identity, Next Date, and complete Cycle visible by hiding Last Executed Date and Remark and expanding Task into the available space; restore the fixed Task width and detail columns automatically when the window widens.
- Show only bracketed numeric day values beside task-grid dates: days left and days passed are positive, overdue days are negative, and today is zero.
- Place State first in Tasks and show compact indicators in this order: Level, History, Alert, Sync, conditional Pause, Status. Level must be a numbered black-bordered icon graded from white at 1 to red at 5. In `ALL`, distinguish paused tasks with a muted row and a Pause tooltip that shows indefinite pause or the resume date.
- Keep cached tasks visible until Sync returns canonical sheet state.
- Support `ALL`, `Normal`, `Warning`, `Expired`, `Pending`, and `Warning + Expired` status filters.
- Use single-click for selection and double-click for task detail.
- Keep Daily Summary readable and mouse-wheel scrollable.
- Include only Alert-enabled tasks in every Daily Summary section.
- Include active snoozed tasks in a separate Daily Summary section and allow their snooze date to be extended.
- Present Tasks, Priority, Daily Summary, and History as peer views without an outer single-item tab control.
- Start each app session in Tasks and do not persist the selected main view.
- Keep task filters compact, left-aligned, and visible only in Tasks.
- Filter the task grid immediately as the user types in Search Task, matching the Task column case-insensitively and combining with other filters.
- Keep ordinary task-filter changes limited to `TasksView.Refresh()`; do not rebuild Daily Summary, recalculate date-derived fields, or reapply sorting for filter-only changes.
- Order Tasks by Category, Type, Task.
- Show Priority as two side-by-side tables: Expired and Warning. Both tables contain Category, Type, Task, the relevant due/warning date, and a numeric Days column.
- Require a task Level from 1-5, where 5 is highest and missing legacy values default to 1. Sort both Priority tables by Level descending, Day Left ascending, Category, Type, and Task.
- Keep Level hidden from the Priority table and represent it only through the Days-cell background, grading from light for Level 1 to dark for Level 5. Days are positive when remaining, zero today, and negative when overdue.
- Dock Priority task details below both tables rather than covering the Warning table with the right sidebar.
- Open Priority details by row double-click without a separate Open button; while open, single-click selection must change the detailed task.
- Give Priority details enough height for a wrapped multiline Remark editor with vertical scrolling.
- Do not allow direct DataGrid column sorting.
- Do not provide a Day Left filter; keep Day Left as a calculated display beside Expired Date and in Priority.
- Load independent local startup files concurrently and publish cached task rows with one collection reset.
- Publish Tasks before building Priority, Daily Summary, and History. Keep all secondary view commands disabled with loading labels until their delayed build is applied.
- Batch sort-description changes with deferred refresh and keep row/column recycling virtualization enabled for the main task grid.
- Keep task-grid text and template content vertically centered with consistent horizontal inset.
- Keep selected-row text, custom date metrics, and state icons readable with a light selection palette.
- Preserve retired `watch-list.json` without reading or modifying it.
- Persist every new completion action locally, import stable Google Sheet Audit rows during Sync, and browse detailed History by month.
- Show an Audit-enabled task's current-month completion count as a compact `N×` value in its History state badge.
- Delay each new completion upload for one hour, including across restarts, during scheduled/background upload. Manual Sync must bypass the delay, upload all Pending mutations, and end Undo eligibility for successfully synchronized completions.
- Show Undo on each History row only while its exact completion mutation remains Pending, has a saved before-state, and no later mutation exists for that task.
- Undo restores the saved pre-completion task state, removes only that completion mutation, and marks the History record Undone. Synced, conflicted, legacy, and undone records have no Undo button.
- Reject completion before local mutation when a legacy task has a non-positive expired value or a negative warning value.
- Use editable, case-insensitive searchable Category and Type ComboBoxes in New/Edit Task, exclude `ALL` from their options, and continue accepting new typed values.
- Recalculate an active task's Warning and Expired dates immediately when either cycle is edited, using Last Executed Date or legacy Prev Date 01 as the anchor; reject a resulting warning date after expiry.
- Keep each Category/Type label on the same row as its ComboBox.
- Normalize Category, Type, and Task to title case at the save boundary for both new and edited tasks.
- Preserve the ordinary Tasks table and use `DEFAULT` as the normal active view. Show an expander only for tasks with active minor tasks or linked followers, with separate Minor Tasks and Linked Tasks sections; row-detail expansion must remain reliable with recycled rows. Provide one accessible icon button whose state and tooltip switch between expanding and collapsing all eligible rows in the current view.
- Show an amber overdue-minor count beside the parent task. Minor tasks never enter Priority, Daily Summary, or Google reminders.
- Configure the relationship from the follower by selecting its Main Task / Unlocked By task through a searchable selector. A task may have many direct followers, a follower has at most one predecessor, chains may continue to any depth, and self-links/cycles are rejected.
- Every linked task keeps its own warning and expiry dates. Completing a task advances its own cycle, locks it when it has a predecessor, and unlocks only its locked direct followers without recalculating their dates.
- Completing any task unlocks only locked direct followers without moving their existing dates; already active followers retain their deadlines.
- Manage stable minor definitions in the task editor. Keep Save and Cancel visible while the editor body scrolls, and leave spacing between each minor Remove action and the vertical scrollbar. In the task sidebar below Remark, show one checkbox and editable completion date per minor plus compact `E` expiry, `L` last-completion, and `I` interval values, using `d`, `mth`, and `yr` units. Default each minor date to the main Complete Date, retain independent edits, always complete the main task, and update only checked minors; do not open a separate minor panel or provide Select All.
- A minor with an interval calculates its next due date; a minor without one records only its latest completion. Minor completions remain a summary on the parent History row.
- Support indefinite or dated Pause. Effectively paused tasks are absent from active/custom views and reminders; on the resume date they return with unchanged warning and expiry dates.
- Require bottom-up pause (all direct followers paused first) and top-down resume (predecessor resumed first). Pausing removes only that task from custom memberships. Do not expose Archive in the UI; retain existing archived data as hidden compatibility data.
- Provide `ALL`, `DEFAULT`, and custom saved views. `ALL` includes paused and locked non-archived tasks in root-to-descendant order; `DEFAULT` and custom views hide them. Manage Filters must search tasks, show useful task/date/status/link details, and keep every add/edit/delete/favourite/membership change in memory until Save atomically writes the local configuration; Close or Escape discards the draft.
- Do not add Frequent/Regular/Long classification, filtering, badges, thresholds, or sorting; frequency work is deferred.

## Completion Requirements

- Reset `CompletionDate` to today when a task is selected.
- Ask for confirmation before Mark Complete.
- Update local cache and queue before waiting for the API.
- Save remark and last execution date.
- Shift previous execution dates.
- Calculate the next warning and expired dates from recurring intervals.
- Clear snooze fields and refresh calculated fields.
- Close the task action UI and schedule upload after the one-hour Undo window.
- Keep failed uploads pending.
- Require explicit Keep PC or Use Sheet resolution for revision conflicts.
- Treat parent completion, selected minor completions, and locked-follower activation as one optimistic compound action and one server mutation.
- Undo a pending compound completion atomically, restoring the parent, selected minor state, and every affected follower. Preserve the compound before-state during conflicts.

## Check-In Requirements

- Load `checkin-settings.json` and create defaults when missing.
- Support per-day enablement and HHmm reminder times.
- Check reminders on startup and every minute.
- Show no more than one check-in reminder per day.
- Record both check-in and alert state when the user checks in.
- Edit Google connection, log retention, and check-in schedule through one draft-based Settings dialog.

## Logging Requirements

- Write info and warning/error logs under `<build output>\log`.
- Prune old log files using `LogRetentionDays`.
- Never let logging failures block workflows.
- Never log raw token-bearing requests.
