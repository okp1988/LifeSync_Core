function onEdit(e) {
  if (!e || !e.range) return;
  const sheet = e.range.getSheet();
  if (sheet.getName() !== TASK_SHEET || e.range.getRow() === 1) return;

  const lock = LockService.getDocumentLock();
  lock.waitLock(30000);
  try {
    const context = taskContext_();
    requireHeaders_(context, Object.values(HEADERS));
    const firstRow = e.range.getRow();
    const lastRow = e.range.getLastRow();
    const checkboxColumn = context.indexes[HEADERS.CHECKBOX];

    for (let row = firstRow; row <= lastRow; row++) {
      ensureRowIdentity_(context, row);
      if (e.range.getColumn() <= checkboxColumn
          && e.range.getLastColumn() >= checkboxColumn
          && context.sheet.getRange(row, checkboxColumn).getValue() === true) {
        const executeDate = parseDate_(context.sheet.getRange(row, context.indexes[HEADERS.EXECUTED_DATE]).getValue()) || new Date();
        completeTaskRow_(context, row, executeDate, null, '');
        context.sheet.getRange(row, checkboxColumn).setValue(false);
      } else if (touchesEditableColumn_(context, e.range)) {
        bumpRevision_(context, row, '');
      }
    }
  } catch (error) {
    e.range.setNote(`ERROR: ${error.message || error}`);
    e.range.setBackground('#ffcccc');
    throw error;
  } finally {
    lock.releaseLock();
  }
}

function ensureRowIdentity_(context, rowNumber) {
  const category = context.sheet.getRange(rowNumber, context.indexes[HEADERS.CATEGORY]).getDisplayValue().trim();
  const task = context.sheet.getRange(rowNumber, context.indexes[HEADERS.TASK]).getDisplayValue().trim();
  if (!category && !task) return;
  const idCell = context.sheet.getRange(rowNumber, context.indexes[HEADERS.TASK_ID]);
  if (!idCell.getDisplayValue().trim()) idCell.setValue(Utilities.getUuid());
  const revisionCell = context.sheet.getRange(rowNumber, context.indexes[HEADERS.REVISION]);
  if (!Number(revisionCell.getValue())) revisionCell.setValue(1);
  const updatedCell = context.sheet.getRange(rowNumber, context.indexes[HEADERS.UPDATED_AT]);
  if (!updatedCell.getValue()) updatedCell.setValue(new Date());
}

function touchesEditableColumn_(context, range) {
  const editable = [
    HEADERS.CATEGORY, HEADERS.TYPE, HEADERS.TASK, HEADERS.EXPIRED_DATE,
    HEADERS.WARNING_DATE, HEADERS.REMARK, HEADERS.EXPIRED_VALUE,
    HEADERS.EXPIRED_UNIT, HEADERS.WARNING_VALUE, HEADERS.WARNING_UNIT,
    HEADERS.ALERT, HEADERS.HISTORY, HEADERS.SNOOZE_UNTIL, HEADERS.SNOOZE_NOTE
  ].map(header => context.indexes[header]);
  return editable.some(column => column >= range.getColumn() && column <= range.getLastColumn());
}

function completeTaskRow_(context, rowNumber, executeDate, remark, operationId) {
  const expiredValue = context.sheet.getRange(rowNumber, context.indexes[HEADERS.EXPIRED_VALUE]).getValue();
  const expiredUnit = context.sheet.getRange(rowNumber, context.indexes[HEADERS.EXPIRED_UNIT]).getValue();
  const warningValue = context.sheet.getRange(rowNumber, context.indexes[HEADERS.WARNING_VALUE]).getValue();
  const warningUnit = context.sheet.getRange(rowNumber, context.indexes[HEADERS.WARNING_UNIT]).getValue();
  if (Number(expiredValue) <= 0 || Number(warningValue) < 0) throw new Error('Recurring cycle values are invalid.');

  const expiredDate = addByUnit_(executeDate, expiredValue, expiredUnit);
  const warningDate = addByUnit_(executeDate, warningValue, warningUnit);
  if (warningDate > expiredDate) throw new Error('Warning date cannot be after expired date.');

  const previousDate1 = context.sheet.getRange(rowNumber, context.indexes[HEADERS.PREV_DATE_1]).getValue();
  context.sheet.getRange(rowNumber, context.indexes[HEADERS.PREV_DATE_2]).setValue(previousDate1).setNumberFormat('dd MMM yyyy');
  context.sheet.getRange(rowNumber, context.indexes[HEADERS.PREV_DATE_1]).setValue(executeDate).setNumberFormat('dd MMM yyyy');
  context.sheet.getRange(rowNumber, context.indexes[HEADERS.EXPIRED_DATE]).setValue(expiredDate).setNumberFormat('dd MMM yyyy');
  context.sheet.getRange(rowNumber, context.indexes[HEADERS.WARNING_DATE]).setValue(warningDate).setNumberFormat('dd MMM yyyy');
  context.sheet.getRange(rowNumber, context.indexes[HEADERS.LAST_EXECUTED_DATE]).setValue(executeDate).setNumberFormat('dd MMM yyyy');
  context.sheet.getRange(rowNumber, context.indexes[HEADERS.EXECUTED_DATE]).setFormula('=NOW()');
  if (remark !== null && remark !== undefined) setAt_(context, rowNumber, HEADERS.REMARK, remark);
  setAt_(context, rowNumber, HEADERS.SNOOZE_UNTIL, '');
  setAt_(context, rowNumber, HEADERS.SNOOZE_NOTE, '');
  setAt_(context, rowNumber, HEADERS.ARCHIVED, false);
  bumpRevision_(context, rowNumber, operationId);

  if (bool_(context.sheet.getRange(rowNumber, context.indexes[HEADERS.HISTORY]).getValue())) {
    appendAudit_(context, rowNumber, executeDate);
  }
}

function appendAudit_(context, rowNumber, executeDate) {
  const audit = SpreadsheetApp.getActiveSpreadsheet().getSheetByName(AUDIT_SHEET);
  if (!audit) throw new Error(`Missing sheet: ${AUDIT_SHEET}`);
  const task = taskFromRow_(context, rowNumber);
  audit.appendRow([
    executeDate, task.category, task.type, task.task,
    parseDate_(task.expiredDate), '', task.remark, task.taskId
  ]);
  const row = audit.getLastRow();
  audit.getRange(row, 1).setNumberFormat('dd MMM yyyy');
  audit.getRange(row, 5).setNumberFormat('dd MMM yyyy');
}
