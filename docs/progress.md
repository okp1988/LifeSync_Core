# LifeSync progress

Recorded: 2026-09-08 12:37 +08:00. Updated/processed: 2026-10-05 13:17 +08:00. Status: History checkpoint and final-documentation workflow correction processed; runtime verification unknown. Times describe recording/processing, not invented implementation dates. Section timestamps cover their entries. [Requirements](requirements.md) owns intent and uncertainty; [AGENTS](../AGENTS.md) owns standing rules.

## Current task and agreed next step

Added: 2026-09-08 12:37 +08:00. Updated/processed: 2026-10-05 13:17 +08:00. Status: final implementation-documentation workflow recorded; History/startup correction implemented; final live script upload unconfirmed.

- Current request: make discussion cleanup and documentation updating the final process after each implementation. Standing authorization is recorded in AGENTS; relevant confirmed requirements and actual results are saved/verified before completed discussion content is removed. This final step needs no separate request. No application changes or tests are part of this policy update.
- The five-file policy (D-DOC-POLICY) was confirmed/implemented on 2026-09-18: the original three were request-only and Discussion/External folders automatic. On 2026-10-05 the user authorized the final implementation cleanup/checkpoint as an exception; outside that step the original three remain request-only. AGENTS owns the current rule; no new file or shared-contribution update was made. The earlier three-file consolidation remains historical provenance, with removed tracked originals recoverable from Git.
- The user reports bugs only and otherwise stays silent. D-HISTORY-RETEST was removed from the active queue as an acknowledgement follow-up, not as evidence that manual tests ran. D-HISTORY/D-STARTUP requirements, implementation and actual checks remain recorded. Completed work does not await success acknowledgement.
- History/startup correction is implemented: RowHeight="Auto" was corrected to "NaN" and rebuilt. Full .NET 8 startup and History search/details/Back/Undo/refresh/scrolling remain independently unverified; no user success report or verification follow-up is required. No additional implementation is agreed; reopen on a reported bug.
- On 2026-09-09 the user supplied appsscript.json/GoogleTasks.gs as the live versions. Review and the subsequent confirmed correction changed local GoogleTasks.gs and Models/SheetTask.cs. That earlier live-version statement does not establish upload of the later correction.
- Remaining handoff: upload the final corrected GoogleTasks.gs as previously instructed, then verify live behavior when requested. appsscript.json needed no change for this correction. Upload/redeployment, migration, trigger inspection and live tests were not performed by the agent; no additional implementation has been agreed.
- Optional `../_General/behavior[project].md`: not created/updated; exact name/existence unknown.

## Implementation status

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-10-05 13:15 +08:00. Status: evidence checkpoint processed; implemented means source present/prior reported work, not proof of unrun tests.

| Area | Implementation evidence | Verification remaining |
| --- | --- | --- |
| Tasks/Priority | WPF peer views, grid date/cycle/state, Priority tables | Real-data visuals and scrolling after all changes unknown |
| History overview (D-HISTORY) | MainWindow.xaml top Unsynced list and monthly three-column summary/details; MainViewModel RebuildCompletionHistory, search, OpenHistoryTaskDetailsAsync and BackToHistorySummaryAsync; HistoryTaskSummary.Build/GetTaskKey; code-behind double-click/Enter handlers | Implemented; startup, current/past months, search, details/Back, Undo/refresh and sizing/scrolling independently unverified; no acknowledgement required |
| History startup correction (D-STARTUP) | MainWindow.xaml History detail grid uses RowHeight="NaN" instead of invalid "Auto"; initial failure reproduced by WPF construction | Implemented; isolated fragment construction/layout passed; full .NET 8 app launch not verified; no acknowledgement required |
| Links/minors/pause/filters | Predecessor chains, locking, inline minor selection, pause manager, ALL hierarchy, draft local filters present | Persistence and relationship edge cases through live sync unproven |
| Compound completion/Undo | Cache/outbox/history, one-hour delay, affected snapshots/server responses implemented | Restart/failure/conflict/Undo integration unknown |
| Independent updates | Save Remark Only / Update Selected Minors Only and updateRemark/updateMinors mutations present | Deployed support and live behavior unknown |
| Manual Sync | Includes delayed operations; optimistic Pending and revision conflicts retained | Live failure/retry/recovery testing unknown |
| Pending Changes | Pending button, action/status/attempt/error details, Retry Selected/All; LastError/LastAttemptAt persistence in local changes | Current two pending records and live recovery not verified |
| Daily Summary | Models/SheetTask.cs BuildGoogleTaskSchedule now uses later expiry/snooze anchor for next event and G Nd; existing per-section ordering/Last Created remain | Live creation metadata and visual fit unknown |
| Google reminders | GoogleTasks.gs reminderStage_ requires expiry reached and snooze ended; removed warning-date prerequisite and all pre-expiry snooze creation; weekly keys/cadence retained | 11 local cases passed; upload of final correction and live execution unknown |
| Check-in popup | BuildCheckinSummary and parent/minor row builders present | Arithmetic and edge-case inclusion unverified |
| Archive | No UI binding; compatibility handler/mutation/data remain | No archived-record deletion authorized |
| Frequency classification | Deferred | No implementation authorized |

## Known issues and partial work

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-10-05 13:15 +08:00. Status: checkpoint processed; open unless explicitly resolved.

- Reports of minor dates/intervals disappearing, completed children remaining visible, expansion failures, cropping and heavy scrolling led to changes. No comprehensive live regression evidence proves all resolved.
- On 2026-10-05 the user reported the app would not open after the History change. WPF fragment construction reproduced XamlParseException: RowHeight="Auto" cannot convert to Double. Changing it to "NaN" corrected that conversion failure and the fragment laid out successfully; build/XML/static review alone had missed it. Full startup remains independently unverified; user success acknowledgement is not required, and the earlier review is not proof the app launches.
- User previously reported two pending items. Recovery UI improves visibility; present queue contents/failures were not inspected in this task. Do not discard/recreate records without a scoped recovery decision.
- “Last Created: Never/Yes/date” and calculated next Google date rely on cached metadata; they do not prove remote execution. Client/server scheduling must align; deployment is separate.
- Removed docs had Warning reminders, obsolete Sheet headers, user-facing Archive, and a “current” review dated 2026-08-13. Corrected/qualified during consolidation; its “no blocking findings” is not current assurance.
- Check-in arithmetic, no-date follower activation, History limits and stored expiry-field semantics remain uncertainties. Reminder timing itself was confirmed and implemented on 2026-09-09; see requirements.
- Dehumidifier diagnosis (2026-09-09, processed): local lifesync-info-2026-09-08.log lines 1-2 show snooze POST at 11:40:46 and success at 11:40:53, setting Snooze Until to 15 September. Response already held 8 September creation metadata and the normal expired key. Available logs contained no earlier Dehumidifier snooze upload. A 06:00-hour creation before that snooze is consistent with evidence, but exact Google creation time was not logged locally. The snooze handler updates dates without removing an existing Google Task.
- The initially reviewed live-copy script's pre-expiry snooze gap is no longer a defect under the later confirmed rule: no creation before expiry is now intended. Warning Date no longer blocks valid expired reminders. Manifest JSON/service alias were consistent; Asia/Singapore and Malaysia share UTC+8 for these dates.
- Build output lock during the correction was resolved after the user closed the app; successful primary rebuild followed. No active build blocker remains.
- Queues created before date-only serialization may need case-specific recovery if still failing; no present evidence supports deletion.

## Verification ledger

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-10-05 13:15 +08:00. Status: conversation evidence imported with limits; checkpoint correction processed.

| Evidence date | Check | Result/scope |
| --- | --- | --- |
| 2026-08-19, old review | dotnet build LifeSyncTaskClient.sln --no-restore | Historical report 0 warnings/errors; not rerun now, no live evidence |
| Prior Daily Summary turn, exact execution timestamp unavailable | Primary build with --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1 and MSBUILDDISABLENODEREUSE=1 | Conversation tool output succeeded, 0 warnings/errors, 6.54 seconds |
| Same prior turn | Executable existence, MainWindow.xaml XML parse, git diff --check | Executable existed; XAML parsed; no diff errors, LF/CRLF advisories only |
| 2026-09-08 consolidation | Inventory and source comparisons | All 11 old Markdown files reviewed across audit/consolidation; key discrepancies compared to source |
| 2026-09-08 12:47 +08:00 | Final inventory, Markdown links, git diff --check | Exactly 3 personal Markdown files; local Markdown links resolve; no diff errors, LF/CRLF advisories only. Both READMEs and 8 old docs removed; tracked originals recoverable from Git |
| 2026-09-08 consolidation | Build/behavioral tests/live migration | Not run; documentation-only task. No current automated test-suite results available |
| 2026-09-09 correction | Node VM execution of actual GoogleTasks.gs with assertions and date-format helper stub | 11 cases passed: early snooze end, after early end but before expiry, expiry after early snooze, expiry before late snooze, late end, unsnoozed before/at expiry, equal dates, 6/7/14 days after late snooze. Manifest JSON/script parsed; Google API not called |
| 2026-09-09 initial build | Primary build command below | Failed copying locked executable: MSB3027/MSB3021. User then closed app; no alternative output path used |
| 2026-09-09 retry after user closed app | `dotnet build LifeSyncTaskClient.sln --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1`, MSBUILDDISABLENODEREUSE=1 | Succeeded, 0 warnings/errors, 0.75 seconds; executable existence verified; source diff check passed with LF/CRLF advisories |
| 2026-09-11 10:24 +08:00 checkpoint | Documentation inventory, local links, exact-rule and duplicate checks, diff/whitespace validation | Documentation only; no application tests/build/live deployment rerun |
| 2026-09-18 policy alignment | Documentation inventory and whitespace checks | Exactly five project Markdown files; AGENTS aligned, Discussion/External folders created; requirements/progress and application unchanged; no build |
| 2026-10-05 History implementation | Primary build using the command in AGENTS | Succeeded, 0 warnings/errors, 6.80 seconds; after search-width correction, final pre-retest build succeeded in 6.43 seconds; executable checked |
| 2026-10-05 History implementation | Independent read-only regression review, XAML XML parse and source diff check | Explicit search width corrected; no remaining static finding reported. XML parsed and whitespace passed with LF/CRLF advisories. Subsequent user startup failure shows these checks did not verify runtime construction |
| 2026-10-05 startup diagnosis/correction | In-memory Windows PowerShell WPF XamlReader construction of actual History fragment/resources; event handlers omitted | Before: RowHeight="Auto" conversion exception. After "NaN": construction and 900x440 layout succeeded. Isolated Windows PowerShell/.NET Framework WPF check, not a full .NET 8 app launch or interactive test |
| 2026-10-05 startup correction | Primary build, executable and source whitespace check | Succeeded, 0 warnings/errors, 6.99 seconds; executable existed; no diff errors, LF/CRLF advisory only |
| 2026-10-05 13:12 +08:00 cleanup/checkpoint | Inventory, local file links, exact-rule/duplicate/whitespace checks, before/after section comparison and independent documentation review | Exactly five Markdown files; links resolve; unrelated sections preserved; AGENTS/External folders unchanged; no actionable consistency finding. Completed Discussion material removed only after destinations saved/re-read. No application code changes, builds/tests, launches, runtime-data writes or deployment in this documentation task |
| Unknown; no acknowledgement follow-up | Full startup and History interactions after corrected build | Independently unverified: current/past-month summary, search, double-click/Enter, Back retaining month/search, top unsynced Undo, sync/undo refresh and minimum-window sizing/scrolling. D-HISTORY-RETEST follow-up withdrawn by user's 2026-10-05 bug-report-only correction; silence is not test evidence |
| Pending | Final corrected script upload and Google execution, migration/trigger, compound retries/replays/conflicts/Undo | Unknown/not run; earlier live-copy statement predates final correction |

## Architecture and current behavior reference

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-10-05 13:08 +08:00. Status: implementation evidence, not independent user confirmation.

- Windows WPF LifeSyncTaskClient.csproj targets net8.0-windows10.0.19041.0, needs Windows SDK; primary LifeSyncTaskClient.sln.
- MainWindow.xaml owns views/overlays/settings/sidebar; code-behind startup/Escape/double-click/wheel/date-picker/selection glue. MainViewModel owns workflows, SheetTask computed fields, MinorTask/TaskSyncModels/CompletionHistoryRecord/CheckinSettings their data.
- GoogleSheetClient owns HTTP; JsonFileStore persistence, AppPaths runtime paths, AppLogger retained logs, RangeObservableCollection single-reset cache replacement. apps-script is production source; docs/google-apps-script.js is a deprecated pointer, not Markdown.
- Startup loads config/prunes logs; cache/outbox/history/check-in settings load concurrently, publishing cached Tasks without Google access. Documented 150 ms first-render delay before snapshot secondary work; tabs loading/disabled until ready. Stale snapshot fallback checks current task set.
- Filters refresh only TasksView; sorting uses DeferRefresh, grids recycling virtualization/shared inset/selection styles. Current XAML initial/minimum width 980, initial height 760, minimum height 640. Old 1120 compact breakpoint was not a confirmed numeric requirement.
- Status choices ALL, Normal, Warning, Expired, Pending, Warning + Expired. Category/Type/Status/Search combine; task search case-insensitive partial. No Day Left filter/direct column sorting. Ordinary Category/Type/Task order with ALL hierarchy handling.
- Editor Level 1-5, legacy default 1; Category/Type searchable/editable excluding ALL. Save title-cases names. Cycle changes recalculate from Last Executed Date/legacy Prev Date 01; reject warning after expiry. Completion rejects nonpositive expiry interval/negative warning interval.
- Main completion resets selected execution date to today, confirms action, shifts prior dates, updates remark/execution/recurrence, clears snooze, persists cache/outbox/history and closes action UI. Background honors UploadAfter; manual Sync bypasses. Failure for one ID permits attempts for other IDs; pull follows pass without remaining upload failure. Canonical merge protects pending fields.
- History filters monthly records by CompletedDate; current month is the initial default and future-month navigation remains disabled. Top unsynced Pending/Conflict records span all months, sorted newest RecordedAt then CompletedDate, with Undo only where existing safety permits. Summary groups case-insensitive stable TaskId, falling back to Category/Type/Task for legacy records; uses latest recorded labels, counts non-Undone records and shows their latest CompletedDate. Search matches task/category/type; current summary ordering is last completion descending then Task/Category/Type, an implementation choice. Double-click/Enter opens all that task's monthly records (including Undone), newest recorded first, with dates/remarks/minor summary/status/eligible Undo; Back retains month/search. Zero-count groups remain available for undone-only details. Tab count remains total monthly records independent of search/drill-down, and the Tasks monthly badge behavior is unchanged. Deduplicate operation ID before legacy semantic match; missing History may seed up to 10 legacy rows, not a retention cap. No saved-history/API format change.
- Settings drafts combine URL/key/log retention and per-day enabled/HHmm schedule. Save validates, Cancel discards; invalid log retention normalizes to 30, minimum 1; default times 1200. Minute checks suppress repeated same-day alerts/check-ins.
- Daily Summary Open selects normal task detail; closing returns to Summary. Quick snooze adds days from active snooze or today; custom snooze supplies explicit date; local save/queue precede upload.

## Persistence reference

Recorded: 2026-09-08 12:37 +08:00. Status: consolidated storage evidence; runtime files not opened.

All paths under AppContext.BaseDirectory (normally build output), currently bin/Debug/net8.0-windows10.0.19041.0/. These are user data, not root-level settings.

| Path | Contents/ownership |
| --- | --- |
| data/config.json | Apps Script URL, API key, log retention; do not disclose or substitute appsettings |
| data/tasks.json | Optimistic cache incl. level/link/pause/minor/snooze/reminder metadata; not remote source of truth |
| data/task-sync-queue.json | Durable ordered ID/revision/payload/state, conflict/snapshots, UploadAfter, LastAttemptAt, LastError |
| data/completion-history.json | Local/imported Audit records, minor summary, compound before-state and Undo |
| data/task-filters.json | Local authoritative definitions/favourite/membership; draft until atomic Save, legacy upload metadata normalized/ignored |
| data/checkin-settings.json | Daily schedule, LastCheckinAt, LastAlertDate |
| data/watch-list.json | Retired compatibility path; file not read/modified/deleted |
| log/lifesync-info-yyyy-MM-dd.log; log/lifesync-warning-error-yyyy-MM-dd.log | Token-redacted logs, retention pruning; logging failures cannot break workflow |

Documented transient state: overlays/sidebar, selection, canceled drafts, current filter inputs, main view, grid widths/sort direction and busy flags not persisted. Saved filter/favourite is distinct from transient filters. Durable link/minor/pause state, cache/outbox, History/Undo, connection and schedule/check-in timestamps must survive existing operations.

## API and Sheet reference

Recorded: 2026-09-08 12:37 +08:00. Updated: 2026-09-11 10:24 +08:00. Status: local reminder contract updated; final deployed alignment unknown.

- Fetch: `GET <GoogleAppsScriptUrl>?action=tasks&token=<ApiKey>`. Modern `{tasks: [...], historyRecords: [...]}`, raw task arrays and legacy `{data: [...]}` parse.
- POST JSON: action, token, operationId (idempotency), taskId (UUID), expectedRevision, payload. Current actions create, update, updateRemark, updateMinors, complete, pause, resume, snooze, clearSnooze, archive. Row number is metadata, not write identity.
- Success returns canonical task and compound affectedTasks. Conflict returns success false, errorCode REVISION_CONFLICT, serverTask; explicit Keep PC/Use Sheet. HTTP errors, success/ok false, nonempty error and HTML/script error bodies fail. Redact token-bearing requests.
- Dates serialize yyyy-MM-dd: executeDate, snoozeUntil, resumeDate, minorCompletions[].completionDate. Apps Script parses local noon safely and accepts legacy ISO minor dates. Keep client reminder keys/snooze anchors aligned.
- Modern task data includes identity/revision, category/type/task/level, recurrence dates/intervals, execution dates, remark, alert/history, row metadata, links/pause/minors, lastGoogleTaskKey/Id/CreatedDate. Audit includes recordId, operationId, taskId, completedDate, recordedAt, category/type/task, remark, state, minor summary as available.
- Legacy row indices: Category 0, Type 1, Task 2, Expired 3, Warning 4, Prev Date 1 at 6, Prev Date 2 at 7, Remark 8, Completed 9. Compatibility parser only, not current visible header names.
- Current Variable.gs task headers: Complete, Category, Type, Task, Expired Date, Warning Date, Day Left, Prev Date 01, Prev Date 02, Remark, Last Executed Date, Executed Date, Expired Value, Expired Unit, Warning Value, Warning Unit, Alert, History, Last Google Task ID.
- Hidden system columns: Task ID, Revision, Updated At, Archived, Snooze Until, Snooze Note, Last Google Task Key, Last Google Task Created Date, Last LifeSync Operation ID, Level, Predecessor Task ID, Linked Unlocked, Linked Activation Date, Paused, Resume Date. Level currently protected. Migration preserves A-S and renames Track ID.
- Minor Tasks protected sheet: Minor Task ID, Parent Task ID, Minor Task, Interval Value/Unit, Last Completed Date, Due Date, Sort Order, Archived, Last LifeSync Operation ID.
- Protected Filters/Filter Memberships sheets and API remain deployment compatibility; current client uses local filter configuration.
- Audit H Task ID, I LifeSync Operation ID, J minor summary. Migration backfills only unique Category/Type/Task matches; ambiguous rows untouched.
- Reminder keys use `Task ID|expiry yyyyMMdd|stage`: expired optionally has a delayed-snooze-anchor suffix; overdue has a seven-day slot suffix. Old snooze-yyyyMMdd keys may remain as historical metadata but no new standalone snooze stage is emitted. Matching keys suppress duplicates; creation stores ID/key/date. Google Task completion is not imported.
- Server requires a valid Expired Date, not Warning Date. Both server reminderStage_ and client BuildGoogleTaskSchedule use the later expiry/snooze anchor; neither creates/predicts pre-expiry Snooze End reminders. Client retains the 06:00-hour estimate and next-day fallback after 07:00; installed trigger and final live version unverified.

## Deployment procedure retained from removed README

Recorded: 2026-09-08 12:37 +08:00. Status: reference, not executed or authorized next action.

1. Back up bound Apps Script project; replace production .gs files/appsscript.json.
2. Configure token using established setLifeSyncApiToken workflow without exposing secrets; client/server must match.
3. Run migrateLifeSyncTaskSchema; timestamped Tasks/Audit/existing-system backups and protected app-managed sheets. Review IDs, Level 1-5, hidden link/pause fields, protection and auditRowsBackfilled.
4. Run setupLifeSyncTriggers: remove obsolete return trigger, install daily reminder trigger. Deploy new web-app version with established execution/access settings.
5. Update client connection if necessary and Sync. Do not manually sort/edit protected system columns or minor sheets; editable columns use onEdit revision tracking.
6. Live verification when requested: stable IDs after Sheet sorting; blank/new task recurrence; conflicts; repeated run without duplicate; snooze before/on/after expiry/seven-day cadence; locked/active followers retaining dates; selected/unchecked minor dates; pause/resume old dates; pending restart/upload failure/retry/idempotence; compound Undo; check-in arithmetic; scrolling/layout. Record actual outcomes.

## Consolidation provenance

Added/processed: 2026-09-08 12:37 +08:00. Status: merged; original tracked versions recoverable from Git.

- README.md and apps-script/README.md: purpose/confirmed behavior to requirements, deployment/current behavior here.
- docs/project-requirements.md, project-settings.md, project-flows.md: confirmed user intent reconciled into requirements; implementation choices and flows here.
- docs/project-architecture.md, project-persistence.md, project-api-contract.md: architecture/storage/API/schema/compatibility here with stale headers/reminders corrected.
- docs/project-development-rules.md: standing rules into AGENTS, technical reference here.
- docs/code-review-current.md: historical test evidence/manual risks here; no fresh review assurance implied.
- Pre-existing MainWindow.xaml, Models/SheetTask.cs, Models/TaskSyncModels.cs, ViewModels/MainViewModel.cs, apps-script/GoogleTasks.gs changes remain outside this documentation task.
