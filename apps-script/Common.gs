function taskContext_() {
  const sheet = SpreadsheetApp.getActiveSpreadsheet().getSheetByName(TASK_SHEET);
  if (!sheet) throw new Error(`Missing sheet: ${TASK_SHEET}`);
  const lastColumn = Math.max(sheet.getLastColumn(), 1);
  const headers = sheet.getRange(1, 1, 1, lastColumn).getDisplayValues()[0];
  const indexes = {};
  headers.forEach((header, index) => {
    if (header) indexes[String(header).trim()] = index + 1;
  });
  return { sheet, indexes };
}

function requireHeaders_(context, headers) {
  headers.forEach(header => {
    if (!context.indexes[header]) throw new Error(`Missing header: ${header}`);
  });
}

function valueAt_(row, context, header) {
  const column = context.indexes[header];
  return column ? row[column - 1] : '';
}

function setAt_(context, rowNumber, header, value) {
  context.sheet.getRange(rowNumber, context.indexes[header]).setValue(value);
}

function dateText_(value) {
  return value instanceof Date && !isNaN(value.getTime())
    ? Utilities.formatDate(value, TIME_ZONE, 'yyyy-MM-dd')
    : null;
}

function parseDate_(value) {
  if (!value) return null;
  if (value instanceof Date) return new Date(value.getFullYear(), value.getMonth(), value.getDate());
  const parsed = new Date(`${value}T12:00:00`);
  return isNaN(parsed.getTime()) ? null : new Date(parsed.getFullYear(), parsed.getMonth(), parsed.getDate());
}

function bool_(value) {
  return value === true || String(value).toLowerCase() === 'true' || String(value) === '1';
}

function addByUnit_(baseDate, amount, unit) {
  const date = new Date(baseDate);
  const value = Number(amount);
  const normalizedUnit = String(unit || '').toLowerCase();
  if (!Number.isFinite(value) || value < 0) throw new Error('Cycle value is invalid.');
  if (normalizedUnit.startsWith('day')) date.setDate(date.getDate() + value);
  else if (normalizedUnit.startsWith('month')) date.setMonth(date.getMonth() + value);
  else if (normalizedUnit.startsWith('year')) date.setFullYear(date.getFullYear() + value);
  else throw new Error(`Unsupported cycle unit: ${unit}`);
  return date;
}

function findTaskRow_(context, taskId) {
  if (!taskId) return null;
  const lastRow = context.sheet.getLastRow();
  if (lastRow < 2) return null;
  const values = context.sheet.getRange(2, context.indexes[HEADERS.TASK_ID], lastRow - 1, 1).getDisplayValues();
  const matches = [];
  values.forEach((row, index) => {
    if (String(row[0]).trim() === String(taskId).trim()) matches.push(index + 2);
  });
  if (matches.length > 1) throw new Error(`Duplicate Task ID: ${taskId}`);
  return matches.length === 1 ? matches[0] : null;
}

function taskFromRow_(context, rowNumber) {
  const row = context.sheet.getRange(rowNumber, 1, 1, context.sheet.getLastColumn()).getValues()[0];
  return {
    taskId: String(valueAt_(row, context, HEADERS.TASK_ID) || ''),
    revision: Number(valueAt_(row, context, HEADERS.REVISION) || 0),
    updatedAt: valueAt_(row, context, HEADERS.UPDATED_AT) || null,
    archived: bool_(valueAt_(row, context, HEADERS.ARCHIVED)),
    category: String(valueAt_(row, context, HEADERS.CATEGORY) || ''),
    type: String(valueAt_(row, context, HEADERS.TYPE) || ''),
    task: String(valueAt_(row, context, HEADERS.TASK) || ''),
    expiredDate: dateText_(valueAt_(row, context, HEADERS.EXPIRED_DATE)),
    warningDate: dateText_(valueAt_(row, context, HEADERS.WARNING_DATE)),
    previousDate1: dateText_(valueAt_(row, context, HEADERS.PREV_DATE_1)),
    previousDate2: dateText_(valueAt_(row, context, HEADERS.PREV_DATE_2)),
    remark: String(valueAt_(row, context, HEADERS.REMARK) || ''),
    lastExecutedDate: dateText_(valueAt_(row, context, HEADERS.LAST_EXECUTED_DATE)),
    expiredValue: Number(valueAt_(row, context, HEADERS.EXPIRED_VALUE) || 0),
    expiredUnit: String(valueAt_(row, context, HEADERS.EXPIRED_UNIT) || 'Month'),
    warningValue: Number(valueAt_(row, context, HEADERS.WARNING_VALUE) || 0),
    warningUnit: String(valueAt_(row, context, HEADERS.WARNING_UNIT) || 'Month'),
    alert: bool_(valueAt_(row, context, HEADERS.ALERT)),
    history: bool_(valueAt_(row, context, HEADERS.HISTORY)),
    rowNumber,
    lastGoogleTaskId: String(valueAt_(row, context, HEADERS.LAST_GOOGLE_TASK_ID) || ''),
    snoozeUntil: dateText_(valueAt_(row, context, HEADERS.SNOOZE_UNTIL)),
    snoozeNote: String(valueAt_(row, context, HEADERS.SNOOZE_NOTE) || ''),
    lastGoogleTaskKey: String(valueAt_(row, context, HEADERS.LAST_GOOGLE_TASK_KEY) || ''),
    lastGoogleTaskCreatedDate: dateText_(valueAt_(row, context, HEADERS.LAST_GOOGLE_TASK_CREATED_DATE)),
    lastLifeSyncOperationId: String(valueAt_(row, context, HEADERS.LAST_OPERATION_ID) || ''),
    completed: false
  };
}

function bumpRevision_(context, rowNumber, operationId) {
  const revisionCell = context.sheet.getRange(rowNumber, context.indexes[HEADERS.REVISION]);
  revisionCell.setValue(Number(revisionCell.getValue() || 0) + 1);
  setAt_(context, rowNumber, HEADERS.UPDATED_AT, new Date());
  if (operationId !== undefined) setAt_(context, rowNumber, HEADERS.LAST_OPERATION_ID, operationId || '');
}

function json_(value) {
  return ContentService.createTextOutput(JSON.stringify(value)).setMimeType(ContentService.MimeType.JSON);
}
