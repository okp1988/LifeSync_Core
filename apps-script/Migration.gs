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
  if (audit) audit.copyTo(spreadsheet).setName(`Audit Backup ${stamp}`);

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
  }

  sheet.getRange(2, context.indexes[HEADERS.UPDATED_AT], Math.max(sheet.getMaxRows() - 1, 1), 1).setNumberFormat('dd MMM yyyy HH:mm:ss');
  sheet.getRange(2, context.indexes[HEADERS.SNOOZE_UNTIL], Math.max(sheet.getMaxRows() - 1, 1), 1).setNumberFormat('dd MMM yyyy');
  sheet.getRange(2, context.indexes[HEADERS.LAST_GOOGLE_TASK_CREATED_DATE], Math.max(sheet.getMaxRows() - 1, 1), 1).setNumberFormat('dd MMM yyyy');
  const systemRange = sheet.getRange(1, 21, sheet.getMaxRows(), SYSTEM_HEADERS.length);
  const protection = systemRange.protect().setDescription('LifeSync system columns');
  protection.setWarningOnly(true);
  sheet.hideColumns(21, SYSTEM_HEADERS.length);

  if (audit) {
    audit.getRange(1, 8).setValue(HEADERS.TASK_ID);
  }

  return {
    changed: needsSchemaChange || duplicateRows.length > 0,
    taskBackup: `Tasks Backup ${stamp}`,
    auditBackup: audit ? `Audit Backup ${stamp}` : null,
    duplicateRowsAssignedNewIds: duplicateRows
  };
}

function setLifeSyncApiToken() {
  PropertiesService.getScriptProperties().setProperty('LIFESYNC_API_TOKEN', 'LIFESYNC');
}
