function migrateLifeSyncTaskSchema() {
  const spreadsheet = SpreadsheetApp.getActiveSpreadsheet();
  const sheet = spreadsheet.getSheetByName(TASK_SHEET);
  if (!sheet) throw new Error(`Missing sheet: ${TASK_SHEET}`);

  const currentHeaders = sheet.getRange(1, 1, 1, Math.max(sheet.getLastColumn(), 20)).getDisplayValues()[0];
  const needsSchemaChange = currentHeaders[19] !== HEADERS.LAST_GOOGLE_TASK_ID
    || SYSTEM_HEADERS.some(header => !currentHeaders.includes(header));

  const stamp = Utilities.formatDate(new Date(), TIME_ZONE, 'yyyyMMdd-HHmmss');
  sheet.copyTo(spreadsheet).setName(`Tasks Backup ${stamp}`);
  const audit = spreadsheet.getSheetByName(AUDIT_SHEET);
  const auditSchemaChanged = audit
    ? audit.getRange(1, 8).getDisplayValue().trim() !== HEADERS.TASK_ID
      || audit.getRange(1, 9).getDisplayValue().trim() !== AUDIT_OPERATION_ID_HEADER
      || audit.getRange(1, 10).getDisplayValue().trim() !== 'Minor Tasks'
    : false;
  if (audit) audit.copyTo(spreadsheet).setName(`Audit Backup ${stamp}`);
  let minorSheet = spreadsheet.getSheetByName(MINOR_TASK_SHEET);
  const minorSheetCreated = !minorSheet;
  const minorBackupName = minorSheet ? `Minor Tasks Backup ${stamp}` : null;
  if (minorSheet) minorSheet.copyTo(spreadsheet).setName(minorBackupName);
  let filterSheet = spreadsheet.getSheetByName(FILTER_SHEET);
  let membershipSheet = spreadsheet.getSheetByName(FILTER_MEMBERSHIP_SHEET);
  const filterSheetCreated = !filterSheet;
  const membershipSheetCreated = !membershipSheet;
  const filterBackupName = filterSheet ? `Filters Backup ${stamp}` : null;
  const membershipBackupName = membershipSheet ? `Filter Memberships Backup ${stamp}` : null;
  if (filterSheet) filterSheet.copyTo(spreadsheet).setName(filterBackupName);
  if (membershipSheet) membershipSheet.copyTo(spreadsheet).setName(membershipBackupName);

  const expected = [
    'Category', 'Type', 'Task', 'Expired Date', 'Warning Date', 'Day Left',
    'Prev Date 01', 'Prev Date 02', 'Remark', 'Last Executed Date',
    'Executed Date', 'Expired Value', 'Expired Unit', 'Warning Value',
    'Warning Unit', 'Alert', 'History'
  ];
  expected.forEach((header, index) => {
    const actual = String(sheet.getRange(1, index + 3).getDisplayValue()).trim();
    if (actual !== header) throw new Error(`Unexpected header at column ${index + 3}: expected '${header}', found '${actual}'. Backups were created; no schema columns were changed.`);
  });

  if (needsSchemaChange) {
    sheet.getRange(1, 2).setValue(HEADERS.CHECKBOX);
    sheet.getRange(1, 20).setValue(HEADERS.LAST_GOOGLE_TASK_ID);
    SYSTEM_HEADERS.forEach((header, index) => sheet.getRange(1, 21 + index).setValue(header));
  }

  const context = taskContext_();
  const seen = new Set();
  const duplicateRows = [];
  const taskIdsBySignature = new Map();
  for (let row = 2; row <= sheet.getLastRow(); row++) {
    const category = sheet.getRange(row, context.indexes[HEADERS.CATEGORY]).getDisplayValue().trim();
    const taskName = sheet.getRange(row, context.indexes[HEADERS.TASK]).getDisplayValue().trim();
    if (!category && !taskName) continue;
    let taskId = sheet.getRange(row, context.indexes[HEADERS.TASK_ID]).getDisplayValue().trim();
    if (!taskId || seen.has(taskId)) {
      if (taskId) duplicateRows.push(row);
      taskId = Utilities.getUuid();
      sheet.getRange(row, context.indexes[HEADERS.TASK_ID]).setValue(taskId);
    }
    seen.add(taskId);
    const revisionCell = sheet.getRange(row, context.indexes[HEADERS.REVISION]);
    if (!Number(revisionCell.getValue())) revisionCell.setValue(1);
    const updatedCell = sheet.getRange(row, context.indexes[HEADERS.UPDATED_AT]);
    if (!updatedCell.getValue()) updatedCell.setValue(new Date());
    const archivedCell = sheet.getRange(row, context.indexes[HEADERS.ARCHIVED]);
    if (archivedCell.isBlank()) archivedCell.setValue(false);
    const linkedUnlockedCell = sheet.getRange(row, context.indexes[HEADERS.LINKED_UNLOCKED]);
    if (linkedUnlockedCell.isBlank()) linkedUnlockedCell.setValue(true);
    const pausedCell = sheet.getRange(row, context.indexes[HEADERS.PAUSED]);
    if (pausedCell.isBlank()) pausedCell.setValue(false);
    const levelCell = sheet.getRange(row, context.indexes[HEADERS.LEVEL]);
    const level = Number(levelCell.getValue());
    if (!Number.isInteger(level) || level < 1 || level > 5) levelCell.setValue(1);
    const signature = migrationTaskSignature_(
      category,
      sheet.getRange(row, context.indexes[HEADERS.TYPE]).getDisplayValue(),
      taskName);
    if (!taskIdsBySignature.has(signature)) taskIdsBySignature.set(signature, []);
    taskIdsBySignature.get(signature).push(taskId);
  }

  let auditRowsBackfilled = 0;
  if (audit) {
    audit.getRange(1, 8).setValue(HEADERS.TASK_ID);
    audit.getRange(1, 9).setValue(AUDIT_OPERATION_ID_HEADER);
    audit.getRange(1, 10).setValue('Minor Tasks');
    for (let row = 2; row <= audit.getLastRow(); row++) {
      const taskIdCell = audit.getRange(row, 8);
      if (taskIdCell.getDisplayValue().trim()) continue;
      const signature = migrationTaskSignature_(
        audit.getRange(row, 2).getDisplayValue(),
        audit.getRange(row, 3).getDisplayValue(),
        audit.getRange(row, 4).getDisplayValue());
      const matches = taskIdsBySignature.get(signature) || [];
      if (matches.length !== 1) continue;
      taskIdCell.setValue(matches[0]);
      auditRowsBackfilled++;
    }
  }

  sheet.getRange(2, context.indexes[HEADERS.UPDATED_AT], Math.max(sheet.getMaxRows() - 1, 1), 1).setNumberFormat('dd MMM yyyy HH:mm:ss');
  sheet.getRange(2, context.indexes[HEADERS.SNOOZE_UNTIL], Math.max(sheet.getMaxRows() - 1, 1), 1).setNumberFormat('dd MMM yyyy');
  sheet.getRange(2, context.indexes[HEADERS.LAST_GOOGLE_TASK_CREATED_DATE], Math.max(sheet.getMaxRows() - 1, 1), 1).setNumberFormat('dd MMM yyyy');
  sheet.getRange(2, context.indexes[HEADERS.LINKED_ACTIVATION_DATE], Math.max(sheet.getMaxRows() - 1, 1), 1).setNumberFormat('dd MMM yyyy');
  sheet.getRange(2, context.indexes[HEADERS.RESUME_DATE], Math.max(sheet.getMaxRows() - 1, 1), 1).setNumberFormat('dd MMM yyyy');
  const systemRange = sheet.getRange(1, 21, sheet.getMaxRows(), SYSTEM_HEADERS.length);
  const protection = systemRange.protect().setDescription('LifeSync system columns');
  protection.setWarningOnly(true);
  sheet.hideColumns(21, SYSTEM_HEADERS.length);

  if (!minorSheet) minorSheet = spreadsheet.insertSheet(MINOR_TASK_SHEET);
  Object.values(MINOR_HEADERS).forEach((header, index) => minorSheet.getRange(1, index + 1).setValue(header));
  minorSheet.setFrozenRows(1);
  if (minorSheet.getMaxRows() > 1) {
    minorSheet.getRange(2, 6, minorSheet.getMaxRows() - 1, 2).setNumberFormat('dd MMM yyyy');
  }
  const existingMinorProtection = minorSheet.getProtections(SpreadsheetApp.ProtectionType.SHEET)
    .find(item => item.getDescription() === 'LifeSync protected minor tasks');
  if (!existingMinorProtection) minorSheet.protect().setDescription('LifeSync protected minor tasks').setWarningOnly(true);
  minorSheet.hideSheet();

  filterSheet = prepareProtectedSystemSheet_(spreadsheet, filterSheet, FILTER_SHEET, FILTER_HEADERS, 'LifeSync protected filters');
  membershipSheet = prepareProtectedSystemSheet_(spreadsheet, membershipSheet, FILTER_MEMBERSHIP_SHEET, FILTER_MEMBERSHIP_HEADERS, 'LifeSync protected filter memberships');

  return {
    changed: needsSchemaChange || auditSchemaChanged || minorSheetCreated || filterSheetCreated || membershipSheetCreated || duplicateRows.length > 0 || auditRowsBackfilled > 0,
    taskBackup: `Tasks Backup ${stamp}`,
    auditBackup: audit ? `Audit Backup ${stamp}` : null,
    minorTaskBackup: minorBackupName,
    minorTaskSheet: MINOR_TASK_SHEET,
    filterBackup: filterBackupName,
    filterMembershipBackup: membershipBackupName,
    duplicateRowsAssignedNewIds: duplicateRows,
    auditRowsBackfilled
  };
}

function prepareProtectedSystemSheet_(spreadsheet, sheet, name, headers, description) {
  if (!sheet) sheet = spreadsheet.insertSheet(name);
  Object.values(headers).forEach((header, index) => sheet.getRange(1, index + 1).setValue(header));
  sheet.setFrozenRows(1);
  const existing = sheet.getProtections(SpreadsheetApp.ProtectionType.SHEET).find(item => item.getDescription() === description);
  if (!existing) sheet.protect().setDescription(description).setWarningOnly(true);
  sheet.hideSheet();
  return sheet;
}

function migrationTaskSignature_(category, type, task) {
  return [category, type, task].map(value => String(value || '').trim().toLowerCase()).join('|');
}

function setLifeSyncApiToken() {
  PropertiesService.getScriptProperties().setProperty('LIFESYNC_API_TOKEN', 'LIFESYNC');
}
