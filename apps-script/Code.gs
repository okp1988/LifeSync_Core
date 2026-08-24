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
        completeCompoundTask_(context, row, executeDate, null, `sheet-${Utilities.getUuid()}`, []);
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
    HEADERS.ALERT, HEADERS.HISTORY, HEADERS.SNOOZE_UNTIL, HEADERS.SNOOZE_NOTE,
    HEADERS.LEVEL
  ].map(header => context.indexes[header]);
  return editable.some(column => column >= range.getColumn() && column <= range.getLastColumn());
}

function completeCompoundTask_(context, rowNumber, executeDate, remark, operationId, minorCompletions) {
  const taskBefore = taskFromRow_(context, rowNumber);
  if (Number(taskBefore.expiredValue) <= 0 || Number(taskBefore.warningValue) < 0) throw new Error('Recurring cycle values are invalid.');
  const mainExpired = addByUnit_(executeDate, taskBefore.expiredValue, taskBefore.expiredUnit);
  const mainWarning = addByUnit_(executeDate, taskBefore.warningValue, taskBefore.warningUnit);
  if (mainWarning > mainExpired) throw new Error('Warning date cannot be after expired date.');
  const followerUpdates = [];
  for (let followerRow = 2; followerRow <= context.sheet.getLastRow(); followerRow++) {
    if (followerRow === rowNumber) continue;
    const follower = taskFromRow_(context, followerRow);
    if (follower.archived || follower.predecessorTaskId !== taskBefore.taskId || follower.isLinkedUnlocked) continue;
    followerUpdates.push({ row: followerRow });
  }
  const minorSummary = applyMinorCompletions_(taskBefore.taskId, minorCompletions, operationId);
  completeTaskRow_(context, rowNumber, executeDate, remark, operationId, minorSummary);
  const affected = [];

  if (taskBefore.predecessorTaskId) {
    setAt_(context, rowNumber, HEADERS.LINKED_UNLOCKED, false);
    setAt_(context, rowNumber, HEADERS.LINKED_ACTIVATION_DATE, '');
  }

  followerUpdates.forEach(update => {
    const follower = taskFromRow_(context, update.row);
    const followerAnchor = parseDate_(follower.lastExecutedDate) || parseDate_(follower.previousDate1);
    if (!follower.expiredDate && followerAnchor && Number(follower.expiredValue) > 0) {
      context.sheet.getRange(update.row, context.indexes[HEADERS.EXPIRED_DATE])
        .setValue(addByUnit_(followerAnchor, follower.expiredValue, follower.expiredUnit)).setNumberFormat('dd MMM yyyy');
    }
    if (!follower.warningDate && followerAnchor && Number(follower.warningValue) >= 0) {
      context.sheet.getRange(update.row, context.indexes[HEADERS.WARNING_DATE])
        .setValue(addByUnit_(followerAnchor, follower.warningValue, follower.warningUnit)).setNumberFormat('dd MMM yyyy');
    }
    setAt_(context, update.row, HEADERS.LINKED_UNLOCKED, true);
    setAt_(context, update.row, HEADERS.LINKED_ACTIVATION_DATE, executeDate);
    bumpRevision_(context, update.row, operationId);
    affected.push(taskFromRow_(context, update.row));
  });
  return affected;
}

function applyMinorCompletions_(taskId, minorCompletions, operationId) {
  if (!minorCompletions || minorCompletions.length === 0) return '';
  const context = minorTaskContext_();
  const summaries = [];
  const seen = new Set();
  const updates = minorCompletions.map(completion => {
    const id = String(completion.minorTaskId || '').trim();
    const completionDate = parseDate_(completion.completionDate);
    if (!id || !completionDate || seen.has(id)) throw new Error('Each selected minor task requires one valid ID and completion date.');
    seen.add(id);
    const row = findMinorTaskRow_(context, id);
    if (!row) throw new Error(`Minor task not found: ${id}`);
    const values = context.sheet.getRange(row, 1, 1, context.sheet.getLastColumn()).getValues()[0];
    const minor = minorTaskFromValues_(context, values);
    if (minor.parentTaskId !== taskId || minor.archived) throw new Error(`Minor task is not active for this parent: ${id}`);
    const dueDate = minor.intervalValue > 0 ? addByUnit_(completionDate, minor.intervalValue, minor.intervalUnit) : null;
    return { row, minor, completionDate, dueDate };
  });
  updates.forEach(update => {
    const { row, minor, completionDate, dueDate } = update;
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.LAST_COMPLETED]).setValue(completionDate).setNumberFormat('dd MMM yyyy');
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.DUE_DATE]).setValue(dueDate || '').setNumberFormat('dd MMM yyyy');
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.LAST_OPERATION_ID]).setValue(operationId || '');
    summaries.push(`${minor.name} (${Utilities.formatDate(completionDate, TIME_ZONE, 'dd MMM yyyy')})`);
  });
  return summaries.join(', ');
}

function completeTaskRow_(context, rowNumber, executeDate, remark, operationId, minorSummary) {
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
    appendAudit_(context, rowNumber, executeDate, minorSummary || '');
  }
}

function appendAudit_(context, rowNumber, executeDate, minorSummary) {
  const audit = SpreadsheetApp.getActiveSpreadsheet().getSheetByName(AUDIT_SHEET);
  if (!audit) throw new Error(`Missing sheet: ${AUDIT_SHEET}`);
  const task = taskFromRow_(context, rowNumber);
  audit.appendRow([
    executeDate, task.category, task.type, task.task,
    parseDate_(task.expiredDate), '', task.remark, task.taskId, task.lastLifeSyncOperationId, minorSummary || ''
  ]);
  const row = audit.getLastRow();
  audit.getRange(row, 1).setNumberFormat('dd MMM yyyy');
  audit.getRange(row, 5).setNumberFormat('dd MMM yyyy');
}
