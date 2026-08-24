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

function minorTaskContext_() {
  const sheet = SpreadsheetApp.getActiveSpreadsheet().getSheetByName(MINOR_TASK_SHEET);
  if (!sheet) throw new Error(`Missing sheet: ${MINOR_TASK_SHEET}. Run migrateLifeSyncTaskSchema.`);
  const lastColumn = Math.max(sheet.getLastColumn(), 1);
  const headers = sheet.getRange(1, 1, 1, lastColumn).getDisplayValues()[0];
  const indexes = {};
  headers.forEach((header, index) => {
    if (header) indexes[String(header).trim()] = index + 1;
  });
  Object.values(MINOR_HEADERS).forEach(header => {
    if (!indexes[header]) throw new Error(`Missing Minor Tasks header: ${header}`);
  });
  return { sheet, indexes };
}

function systemSheetContext_(sheetName, headers) {
  const sheet = SpreadsheetApp.getActiveSpreadsheet().getSheetByName(sheetName);
  if (!sheet) throw new Error(`Missing sheet: ${sheetName}. Run migrateLifeSyncTaskSchema.`);
  const values = sheet.getRange(1, 1, 1, Math.max(sheet.getLastColumn(), 1)).getDisplayValues()[0];
  const indexes = {};
  values.forEach((header, index) => { if (header) indexes[String(header).trim()] = index + 1; });
  Object.values(headers).forEach(header => { if (!indexes[header]) throw new Error(`Missing ${sheetName} header: ${header}`); });
  return { sheet, indexes };
}

function taskFilters_() {
  const filterContext = systemSheetContext_(FILTER_SHEET, FILTER_HEADERS);
  const membershipContext = systemSheetContext_(FILTER_MEMBERSHIP_SHEET, FILTER_MEMBERSHIP_HEADERS);
  const memberships = new Map();
  if (membershipContext.sheet.getLastRow() >= 2) {
    membershipContext.sheet.getRange(2, 1, membershipContext.sheet.getLastRow() - 1, membershipContext.sheet.getLastColumn()).getValues().forEach(row => {
      const filterId = String(row[membershipContext.indexes[FILTER_MEMBERSHIP_HEADERS.FILTER_ID] - 1] || '').trim();
      const taskId = String(row[membershipContext.indexes[FILTER_MEMBERSHIP_HEADERS.TASK_ID] - 1] || '').trim();
      if (!filterId || !taskId) return;
      if (!memberships.has(filterId)) memberships.set(filterId, []);
      memberships.get(filterId).push(taskId);
    });
  }
  if (filterContext.sheet.getLastRow() < 2) return [];
  return filterContext.sheet.getRange(2, 1, filterContext.sheet.getLastRow() - 1, filterContext.sheet.getLastColumn()).getValues()
    .map(row => {
      const filterId = String(row[filterContext.indexes[FILTER_HEADERS.ID] - 1] || '').trim();
      if (!filterId) return null;
      return {
        filterId,
        name: String(row[filterContext.indexes[FILTER_HEADERS.NAME] - 1] || ''),
        isSystem: bool_(row[filterContext.indexes[FILTER_HEADERS.SYSTEM] - 1]),
        isFavourite: bool_(row[filterContext.indexes[FILTER_HEADERS.FAVOURITE] - 1]),
        sortOrder: Number(row[filterContext.indexes[FILTER_HEADERS.SORT_ORDER] - 1] || 0),
        taskIds: memberships.get(filterId) || []
      };
    }).filter(item => item !== null);
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
  const text = String(value).trim();
  const parsed = /^\d{4}-\d{2}-\d{2}$/.test(text)
    ? new Date(`${text}T12:00:00`)
    : new Date(text);
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
    level: level_(valueAt_(row, context, HEADERS.LEVEL)),
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
    predecessorTaskId: String(valueAt_(row, context, HEADERS.PREDECESSOR_TASK_ID) || ''),
    isLinkedUnlocked: valueAt_(row, context, HEADERS.LINKED_UNLOCKED) === ''
      ? true
      : bool_(valueAt_(row, context, HEADERS.LINKED_UNLOCKED)),
    linkedActivationDate: dateText_(valueAt_(row, context, HEADERS.LINKED_ACTIVATION_DATE)),
    paused: bool_(valueAt_(row, context, HEADERS.PAUSED)),
    resumeDate: dateText_(valueAt_(row, context, HEADERS.RESUME_DATE)),
    minorTasks: [],
    completed: false
  };
}

function minorTasksByParent_() {
  const context = minorTaskContext_();
  const result = new Map();
  if (context.sheet.getLastRow() < 2) return result;
  const values = context.sheet.getRange(2, 1, context.sheet.getLastRow() - 1, context.sheet.getLastColumn()).getValues();
  values.forEach(row => {
    const parentTaskId = String(row[context.indexes[MINOR_HEADERS.PARENT_ID] - 1] || '').trim();
    const minorTaskId = String(row[context.indexes[MINOR_HEADERS.ID] - 1] || '').trim();
    if (!parentTaskId || !minorTaskId) return;
    const minor = minorTaskFromValues_(context, row);
    if (!result.has(parentTaskId)) result.set(parentTaskId, []);
    result.get(parentTaskId).push(minor);
  });
  result.forEach(items => items.sort((left, right) => left.order - right.order || left.name.localeCompare(right.name)));
  return result;
}

function minorTaskFromValues_(context, row) {
  return {
    minorTaskId: String(row[context.indexes[MINOR_HEADERS.ID] - 1] || ''),
    parentTaskId: String(row[context.indexes[MINOR_HEADERS.PARENT_ID] - 1] || ''),
    name: String(row[context.indexes[MINOR_HEADERS.NAME] - 1] || ''),
    intervalValue: row[context.indexes[MINOR_HEADERS.INTERVAL_VALUE] - 1] === ''
      ? null
      : Number(row[context.indexes[MINOR_HEADERS.INTERVAL_VALUE] - 1]),
    intervalUnit: String(row[context.indexes[MINOR_HEADERS.INTERVAL_UNIT] - 1] || 'Month'),
    latestCompletionDate: dateText_(row[context.indexes[MINOR_HEADERS.LAST_COMPLETED] - 1]),
    dueDate: dateText_(row[context.indexes[MINOR_HEADERS.DUE_DATE] - 1]),
    order: Number(row[context.indexes[MINOR_HEADERS.ORDER] - 1] || 0),
    archived: bool_(row[context.indexes[MINOR_HEADERS.ARCHIVED] - 1])
  };
}

function findMinorTaskRow_(context, minorTaskId) {
  if (!minorTaskId || context.sheet.getLastRow() < 2) return null;
  const values = context.sheet.getRange(2, context.indexes[MINOR_HEADERS.ID], context.sheet.getLastRow() - 1, 1).getDisplayValues();
  const matches = [];
  values.forEach((row, index) => {
    if (String(row[0]).trim() === String(minorTaskId).trim()) matches.push(index + 2);
  });
  if (matches.length > 1) throw new Error(`Duplicate Minor Task ID: ${minorTaskId}`);
  return matches.length === 1 ? matches[0] : null;
}

function level_(value) {
  const level = Number(value);
  return Number.isInteger(level) && level >= 1 && level <= 5 ? level : 1;
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
