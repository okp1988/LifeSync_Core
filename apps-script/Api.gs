function doGet(e) {
  if (!validateToken_(e && e.parameter && e.parameter.token)) return json_({ success: false, error: 'Unauthorized' });
  if (!e.parameter || e.parameter.action !== 'tasks') return json_({ success: false, error: 'Unknown action' });
  try {
    const context = taskContext_();
    requireHeaders_(context, Object.values(HEADERS));
    const tasks = [];
    for (let row = 2; row <= context.sheet.getLastRow(); row++) {
      const task = taskFromRow_(context, row);
      if ((!task.category && !task.task) || task.archived) continue;
      tasks.push(task);
    }
    return json_({ success: true, serverTime: new Date(), tasks });
  } catch (error) {
    return json_({ success: false, error: error.message || String(error) });
  }
}

function doPost(e) {
  let request;
  try {
    request = JSON.parse((e.postData && e.postData.contents) || '{}');
  } catch (_) {
    return legacyPost_(e);
  }
  if (!validateToken_(request.token)) return json_({ success: false, error: 'Unauthorized' });

  const lock = LockService.getDocumentLock();
  lock.waitLock(30000);
  try {
    const context = taskContext_();
    requireHeaders_(context, Object.values(HEADERS));
    const response = applyMutation_(context, request);
    return json_(response);
  } catch (error) {
    return json_({ success: false, errorCode: 'SERVER_ERROR', error: error.message || String(error) });
  } finally {
    lock.releaseLock();
  }
}

function applyMutation_(context, request) {
  const action = String(request.action || '');
  const taskId = String(request.taskId || '');
  const operationId = String(request.operationId || '');
  const expectedRevision = Number(request.expectedRevision || 0);
  const payload = request.payload || {};
  if (!taskId || !operationId) return { success: false, errorCode: 'INVALID_REQUEST', error: 'Task ID and operation ID are required.' };

  if (action === 'create') return createTask_(context, taskId, operationId, payload);
  const row = findTaskRow_(context, taskId);
  if (!row) return { success: false, errorCode: 'TASK_NOT_FOUND', error: `Task not found: ${taskId}` };
  const current = taskFromRow_(context, row);
  if (current.lastLifeSyncOperationId === operationId) return { success: true, task: current };
  if (current.revision !== expectedRevision) {
    return { success: false, errorCode: 'REVISION_CONFLICT', error: 'Task changed in Google Sheet.', serverTask: current };
  }

  if (action === 'update') updateTaskFields_(context, row, payload);
  else if (action === 'complete') completeTaskRow_(context, row, parseDate_(payload.executeDate) || new Date(), payload.remark || '', operationId);
  else if (action === 'snooze') {
    const snoozeUntil = parseDate_(payload.snoozeUntil);
    if (!snoozeUntil) return { success: false, errorCode: 'INVALID_REQUEST', error: 'Snooze date is required.' };
    setAt_(context, row, HEADERS.SNOOZE_UNTIL, snoozeUntil);
    setAt_(context, row, HEADERS.SNOOZE_NOTE, payload.snoozeNote || '');
  } else if (action === 'clearSnooze') {
    setAt_(context, row, HEADERS.SNOOZE_UNTIL, '');
    setAt_(context, row, HEADERS.SNOOZE_NOTE, '');
  } else if (action === 'archive') setAt_(context, row, HEADERS.ARCHIVED, true);
  else return { success: false, errorCode: 'UNKNOWN_ACTION', error: `Unknown action: ${action}` };

  if (action !== 'complete') bumpRevision_(context, row, operationId);
  return { success: true, task: taskFromRow_(context, row) };
}

function createTask_(context, taskId, operationId, payload) {
  const existingRow = findTaskRow_(context, taskId);
  if (existingRow) {
    const existing = taskFromRow_(context, existingRow);
    if (existing.lastLifeSyncOperationId === operationId) return { success: true, task: existing };
    return { success: false, errorCode: 'DUPLICATE_TASK_ID', error: `Task ID already exists: ${taskId}`, serverTask: existing };
  }
  validateTaskFields_(payload);
  const row = Math.max(context.sheet.getLastRow() + 1, 2);
  setAt_(context, row, HEADERS.TASK_ID, taskId);
  updateTaskFields_(context, row, payload);
  setAt_(context, row, HEADERS.REVISION, 1);
  setAt_(context, row, HEADERS.UPDATED_AT, new Date());
  setAt_(context, row, HEADERS.ARCHIVED, false);
  setAt_(context, row, HEADERS.LAST_OPERATION_ID, operationId);
  setAt_(context, row, HEADERS.CHECKBOX, false);
  context.sheet.getRange(row, context.indexes[HEADERS.DAY_LEFT]).setFormula(`=IF(F${row}="","",F${row}-TODAY())`);
  return { success: true, task: taskFromRow_(context, row) };
}

function updateTaskFields_(context, row, payload) {
  validateTaskFields_(payload);
  setAt_(context, row, HEADERS.CATEGORY, String(payload.category).trim());
  setAt_(context, row, HEADERS.TYPE, String(payload.type).trim());
  setAt_(context, row, HEADERS.TASK, String(payload.task).trim());
  setAt_(context, row, HEADERS.EXPIRED_VALUE, Number(payload.expiredValue));
  setAt_(context, row, HEADERS.EXPIRED_UNIT, payload.expiredUnit);
  setAt_(context, row, HEADERS.WARNING_VALUE, Number(payload.warningValue));
  setAt_(context, row, HEADERS.WARNING_UNIT, payload.warningUnit);
  setAt_(context, row, HEADERS.ALERT, bool_(payload.alert));
  setAt_(context, row, HEADERS.HISTORY, bool_(payload.history));
}

function validateTaskFields_(payload) {
  if (!String(payload.category || '').trim() || !String(payload.type || '').trim() || !String(payload.task || '').trim()) throw new Error('Category, type, and task are required.');
  if (Number(payload.expiredValue) <= 0 || Number(payload.warningValue) < 0) throw new Error('Recurring cycle values are invalid.');
  if (!['Day', 'Month', 'Year'].includes(payload.expiredUnit) || !['Day', 'Month', 'Year'].includes(payload.warningUnit)) throw new Error('Cycle unit must be Day, Month, or Year.');
}

function validateToken_(token) {
  return token && token === getApiToken_();
}

function legacyPost_(e) {
  if (!e || !e.parameter || !validateToken_(e.parameter.token)) return json_({ success: false, error: 'Unauthorized' });
  if (e.parameter.action !== 'complete') return json_({ success: false, error: 'Legacy action is unsupported.' });
  const lock = LockService.getDocumentLock();
  lock.waitLock(30000);
  try {
    const context = taskContext_();
    const row = Number(e.parameter.rowid);
    if (!Number.isInteger(row) || row < 2 || row > context.sheet.getLastRow()) throw new Error('Invalid legacy row ID.');
    ensureRowIdentity_(context, row);
    completeTaskRow_(context, row, parseDate_(e.parameter.executedate) || new Date(), e.parameter.remark || '', `legacy-${Utilities.getUuid()}`);
    return json_({ success: true, task: taskFromRow_(context, row) });
  } catch (error) {
    return json_({ success: false, error: error.message || String(error) });
  } finally {
    lock.releaseLock();
  }
}
