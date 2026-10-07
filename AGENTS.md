# LifeSync agent instructions

Added: 2026-09-08 12:37 +08:00. Updated: 2026-10-05 13:17 +08:00. Status: active; final implementation cleanup/checkpoint and bug-report-only workflow processed. Original dates of older rules unknown.

## Documentation and scope

- Before implementation, after compaction, or when context is unclear, read relevant [requirements](docs/requirements.md) and [progress](docs/progress.md). Requirements express user intent; progress records implementation/evidence. Code and agent suggestions do not establish requirements.
- Maintain exactly five project-local Markdown records, each with a separate purpose: this file owns project working rules; requirements owns confirmed requirements, restrictions and unresolved requirements; progress owns implementation status, verification evidence and remaining work; [Discussion](docs/DISCUSSION.md) owns active discussion, decisions, approval scope and next steps; [External folders](docs/EXTERNAL_FOLDERS.md) registers project-used external roots, known subpaths, purposes and evidence.
- Discussion and External folders are maintained automatically throughout work without further approval. Keep them concise and current when material state or known folder usage changes. Before resuming non-trivial work, read Discussion; follow the global pending-item and discussion-cleanup workflows. Discussion is not a replacement for requirements/progress. Folder registration does not authorize external writes.
- Updated: 2026-10-05 13:17 +08:00. Status: confirmed standing authorization, processed. After each implementation, the final process is discussion cleanup and a documentation checkpoint, after the applicable build/check/review work. This standing instruction authorizes that final documentation step without another request. Merge confirmed requirements into requirements, actual implementation/checks/unknowns into progress, and any confirmed working-rule changes into AGENTS; skip unchanged files/sections. Save and verify destinations before removing completed Discussion items. Retain actual unfinished work, unresolved decisions and reported bugs; do not retain success-acknowledgement items. Cleanup here means discussion records, not deletion of code/data/folders.
- Outside that final implementation step, AGENTS, requirements and progress remain request-only. Save authorized important changes promptly rather than waiting for compaction. No automatic new Markdown files, shared-contribution edits or global-rule edits are authorized by the final checkpoint.
- Follow the global working rules alongside these project-specific rules; current user instructions override conflicting older documentation. Do not automatically load `_General/INDEX.md` or other shared standards.
- Update affected sections, avoid duplication, and replace superseded requirements with later confirmed decisions. Separate unclear requirements. Do not rewrite whole files routinely.
- Each added/changed entry or coherent section needs added/updated date-time, timezone, and pending/processed status. Track confirmation separately from implementation/verification. Mark imported dates unknown rather than inventing them; record processed timestamps when work completes.
- Ask before adding Markdown beyond the five designated records, giving the concrete information, audience, why existing files cannot hold it, and overlap checks. Do not recreate READMEs or automatic review/summary files.
- Optional `../_General/behavior[project].md` (project placeholder; exact filename/existence unconfirmed) shares rules/behavior with other agents and is not personal Markdown. Create/update only on request; verify exact path and existing content, deduplicate/update entries, and timestamp each entry with processing status. Do not assume it exists or inspect unrelated shared files.
- Do not start additional implementation, refactoring, unrelated reviews, deployment, or a recorded next step unless requested. Useful in-scope read-only impact/regression reviews and subagent delegation follow the global rules without separate requests; they do not expand editing approval.

## Workspace and safety

- Repository is the only general-purpose writable workspace. Preserve unrelated dirty changes and user runtime data. No unauthorized secrets, global configuration, dependencies, or Git history/remote changes.
- Shared standards/icons are read-only unless maintenance is requested. For icon buttons, search `../_General/icon_index.csv`, propose an indexed icon and obtain icon-selection confirmation before inspecting/copying/using its file. Do not recursively scan shared icons. Copy approved assets locally; never depend on the shared folder at runtime/build/deployment. Icon-only buttons need concise tooltips and accessible names.
- Normal builds may read installed SDKs, targeting packs, restored NuGet packages/caches and use OS build temporary files. No unrelated external/AppData inspection or cleanup, external filesystem links, package installation/restore, or network without approval.
- Before deleting/moving/renaming/replacing, list exact targets, reasons, dependencies and recovery. No broad recursive deletion, destructive Git cleanup, alternate output/workspace paths, or background processes without authorization.
- Keep UI in MainWindow.xaml, event glue in code-behind, workflows in MainViewModel, computed fields in SheetTask, HTTP in GoogleSheetClient, persistence in AppPaths/JsonFileStore. Notify calculated fields after mutations; preserve local save before upload, pending/conflict data and virtualization.

## Verification

- Updated: 2026-10-05 13:15 +08:00. Status: confirmed user correction, processed. Do not retain completed work in Discussion solely to await verification, retest or success acknowledgement. The user reports bugs only; silence requires no acknowledgement or verification follow-up. Keep actual checks and unknown runtime verification in progress without claiming tests passed from silence. A reported bug reopens active work; actual unfinished implementation/deployment remains distinct from acknowledgement.
- After source/project/build changes run the primary Windows build using restored packages:

```powershell
$env:MSBUILDDISABLENODEREUSE = "1"
dotnet build LifeSyncTaskClient.sln --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
```

- If output is locked, stop; never kill the app, clean locked files or change output paths. Say: “Build stopped because the primary build path appears to be locked or in use. Please close the app, then rerun the build. I will not create a secondary build path.”
- Report build/executable/relevant validation and unverified live behavior separately. Documentation-only work uses inventory/reference/diff checks; no build required. Summarize intentionally changed files before finishing.
