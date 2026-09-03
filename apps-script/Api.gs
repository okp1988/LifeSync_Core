function doGet(e) {
  if (!validateToken_(e && e.parameter && e.parameter.token)) return json_({ success: false, error: 'Unauthorized' });
  if (!e.parameter || e.parameter.action !== 'tasks') return json_({ success: false, error: 'Unknown action' });
  try {
    const context = taskContext_();
    requireHeaders_(context, Object.values(HEADERS));
    const minorsByParent = minorTasksByParent_();
    const tasks = [];
    for (let row = 2; row <= context.sheet.getLastRow(); row++) {
      const task = taskFromRow_(context, row);
      if ((!task.category && !task.task) || task.archived) continue;
      task.minorTasks = minorsByParent.get(task.taskId) || [];
      tasks.push(task);
    }
    return json_({ success: true, serverTime: new Date(), tasks, historyRecords: auditHistory_(), filters: taskFilters_() });
  } catch (error) {
    return json_({ success: false, error: error.message || String(error) });
  }
}

function auditHistory_() {
  const audit = SpreadsheetApp.getActiveSpreadsheet().getSheetByName(AUDIT_SHEET);
  if (!audit || audit.getLastRow() < 2) return [];
  const lastColumn = Math.max(audit.getLastColumn(), 9);
  const values = audit.getRange(2, 1, audit.getLastRow() - 1, lastColumn).getValues();
  return values.map((row, index) => {
    const taskId = String(row[7] || '').trim();
    const completedDate = dateText_(row[0]);
    if (!taskId || !completedDate) return null;
    const operationId = String(row[8] || '').trim();
    return {
      recordId: operationId ? `audit-${operationId}` : `audit-${taskId}-${completedDate}-${index + 2}`,
      operationId,
      taskId,
      completedDate,
      recordedAt: row[0] instanceof Date ? row[0] : `${completedDate}T12:00:00`,
      category: String(row[1] || ''),
      type: String(row[2] || ''),
      task: String(row[3] || ''),
      remark: String(row[6] || ''),
      state: 'Synced',
      minorCompletionSummary: String(row[9] || '')
    };
  }).filter(record => record !== null);
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
    if (String(request.action || '') === 'saveFilters') {
      saveTaskFilters_(request.filters || [], request.operationId || '');
      return json_({ success: true });
    }
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

function saveTaskFilters_(filters, operationId) {
  if (!Array.isArray(filters)) throw new Error('Filters must be an array.');
  const filterContext = systemSheetContext_(FILTER_SHEET, FILTER_HEADERS);
  const membershipContext = systemSheetContext_(FILTER_MEMBERSHIP_SHEET, FILTER_MEMBERSHIP_HEADERS);
  const seen = new Set();
  let favouriteCount = 0;
  const filterRows = [];
  const membershipRows = [];
  filters.forEach((filter, index) => {
    const id = String(filter.filterId || '').trim();
    const name = String(filter.name || '').trim();
    if (!id || !name || seen.has(id)) throw new Error('Every filter needs a unique ID and name.');
    seen.add(id);
    if (bool_(filter.isFavourite)) favouriteCount++;
    filterRows.push([id, name, bool_(filter.isSystem), bool_(filter.isFavourite), Number(filter.sortOrder || index), new Date(), operationId]);
    (filter.taskIds || []).forEach(taskId => {
      const normalizedTaskId = String(taskId || '').trim();
      if (normalizedTaskId) membershipRows.push([id, normalizedTaskId, new Date(), operationId]);
    });
  });
  if (favouriteCount > 1) throw new Error('Only one favourite filter is allowed.');
  if (filterContext.sheet.getLastRow() > 1) filterContext.sheet.getRange(2, 1, filterContext.sheet.getLastRow() - 1, filterContext.sheet.getLastColumn()).clearContent();
  if (membershipContext.sheet.getLastRow() > 1) membershipContext.sheet.getRange(2, 1, membershipContext.sheet.getLastRow() - 1, membershipContext.sheet.getLastColumn()).clearContent();
  if (filterRows.length) filterContext.sheet.getRange(2, 1, filterRows.length, filterRows[0].length).setValues(filterRows);
  if (membershipRows.length) membershipContext.sheet.getRange(2, 1, membershipRows.length, membershipRows[0].length).setValues(membershipRows);
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
  if (current.lastLifeSyncOperationId === operationId) {
    attachMinorTasks_([current]);
    return { success: true, task: current, affectedTasks: affectedTasksForOperation_(context, taskId, operationId) };
  }
  if (current.revision !== expectedRevision) {
    attachMinorTasks_([current]);
    return { success: false, errorCode: 'REVISION_CONFLICT', error: 'Task changed in Google Sheet.', serverTask: current };
  }

  let affectedTasks = [];
  if (action === 'update') {
    if (Object.prototype.hasOwnProperty.call(payload, 'predecessorTaskId')) validateRelationship_(context, taskId, payload.predecessorTaskId);
    updateTaskFields_(context, row, payload);
    if (Array.isArray(payload.minorTasks)) syncMinorTasks_(taskId, payload.minorTasks, operationId);
  }
  else if (action === 'updateRemark') {
    setAt_(context, row, HEADERS.REMARK, String(payload.remark || ''));
  }
  else if (action === 'updateMinors') {
    if (!Array.isArray(payload.minorTasks)) return { success: false, errorCode: 'INVALID_REQUEST', error: 'Minor tasks are required.' };
    syncMinorTasks_(taskId, payload.minorTasks, operationId);
  }
  else if (action === 'complete') affectedTasks = completeCompoundTask_(context, row, parseDate_(payload.executeDate) || new Date(), payload.remark || '', operationId, payload.minorCompletions || []);
  else if (action === 'snooze') {
    const snoozeUntil = parseDate_(payload.snoozeUntil);
    if (!snoozeUntil) return { success: false, errorCode: 'INVALID_REQUEST', error: 'Snooze date is required.' };
    setAt_(context, row, HEADERS.SNOOZE_UNTIL, snoozeUntil);
    setAt_(context, row, HEADERS.SNOOZE_NOTE, payload.snoozeNote || '');
  } else if (action === 'clearSnooze') {
    setAt_(context, row, HEADERS.SNOOZE_UNTIL, '');
    setAt_(context, row, HEADERS.SNOOZE_NOTE, '');
  } else if (action === 'archive') {
    for (let followerRow = 2; followerRow <= context.sheet.getLastRow(); followerRow++) {
      const follower = taskFromRow_(context, followerRow);
      if (!follower.archived && follower.predecessorTaskId === taskId) {
        return { success: false, errorCode: 'INVALID_REQUEST', error: 'Unlink followers before archiving their source.' };
      }
    }
    setAt_(context, row, HEADERS.ARCHIVED, true);
  }
  else if (action === 'pause') {
    const resumeDate = parseDate_(payload.resumeDate);
    if (resumeDate && resumeDate <= parseDate_(new Date())) return { success: false, errorCode: 'INVALID_REQUEST', error: 'Resume date must be after today.' };
    const activeFollowers = [];
    for (let followerRow = 2; followerRow <= context.sheet.getLastRow(); followerRow++) {
      const follower = taskFromRow_(context, followerRow);
      const followerPaused = follower.paused && (!follower.resumeDate || parseDate_(follower.resumeDate) > parseDate_(new Date()));
      if (!follower.archived && follower.predecessorTaskId === taskId && !followerPaused) activeFollowers.push(follower.task);
    }
    if (activeFollowers.length) return { success: false, errorCode: 'INVALID_REQUEST', error: `Pause follower tasks first: ${activeFollowers.join(', ')}` };
    setAt_(context, row, HEADERS.PAUSED, true);
    setAt_(context, row, HEADERS.RESUME_DATE, resumeDate || '');
  } else if (action === 'resume') {
    if (current.predecessorTaskId) {
      const predecessorRow = findTaskRow_(context, current.predecessorTaskId);
      const predecessor = predecessorRow ? taskFromRow_(context, predecessorRow) : null;
      const predecessorPaused = predecessor && predecessor.paused
        && (!predecessor.resumeDate || parseDate_(predecessor.resumeDate) > parseDate_(new Date()));
      if (predecessorPaused) return { success: false, errorCode: 'INVALID_REQUEST', error: `Resume main task first: ${predecessor.task}` };
    }
    setAt_(context, row, HEADERS.PAUSED, false);
    setAt_(context, row, HEADERS.RESUME_DATE, '');
  }
  else return { success: false, errorCode: 'UNKNOWN_ACTION', error: `Unknown action: ${action}` };

  if (action !== 'complete') bumpRevision_(context, row, operationId);
  const responseTask = taskFromRow_(context, row);
  attachMinorTasks_([responseTask]);
  attachMinorTasks_(affectedTasks);
  return { success: true, task: responseTask, affectedTasks };
}

function createTask_(context, taskId, operationId, payload) {
  const existingRow = findTaskRow_(context, taskId);
  if (existingRow) {
    const existing = taskFromRow_(context, existingRow);
    if (existing.lastLifeSyncOperationId === operationId) {
      attachMinorTasks_([existing]);
      return { success: true, task: existing, affectedTasks: [] };
    }
    attachMinorTasks_([existing]);
    return { success: false, errorCode: 'DUPLICATE_TASK_ID', error: `Task ID already exists: ${taskId}`, serverTask: existing };
  }
  validateTaskFields_(payload);
  validateRelationship_(context, taskId, payload.predecessorTaskId);
  const row = Math.max(context.sheet.getLastRow() + 1, 2);
  setAt_(context, row, HEADERS.TASK_ID, taskId);
  updateTaskFields_(context, row, payload);
  setAt_(context, row, HEADERS.REVISION, 1);
  setAt_(context, row, HEADERS.UPDATED_AT, new Date());
  setAt_(context, row, HEADERS.ARCHIVED, false);
  setAt_(context, row, HEADERS.LAST_OPERATION_ID, operationId);
  setAt_(context, row, HEADERS.CHECKBOX, false);
  syncMinorTasks_(taskId, payload.minorTasks || [], operationId);
  context.sheet.getRange(row, context.indexes[HEADERS.DAY_LEFT]).setFormula(`=IF(F${row}="","",F${row}-TODAY())`);
  const task = taskFromRow_(context, row);
  attachMinorTasks_([task]);
  return { success: true, task, affectedTasks: [] };
}

function updateTaskFields_(context, row, payload) {
  validateTaskFields_(payload);
  const recurrenceAnchor = parseDate_(context.sheet.getRange(row, context.indexes[HEADERS.LAST_EXECUTED_DATE]).getValue())
    || parseDate_(context.sheet.getRange(row, context.indexes[HEADERS.PREV_DATE_1]).getValue());
  const expiredDate = recurrenceAnchor ? addByUnit_(recurrenceAnchor, payload.expiredValue, payload.expiredUnit) : null;
  const warningDate = recurrenceAnchor ? addByUnit_(recurrenceAnchor, payload.warningValue, payload.warningUnit) : null;
  if (warningDate && expiredDate && warningDate > expiredDate) throw new Error('Warning date cannot be after expired date.');
  setAt_(context, row, HEADERS.LEVEL, level_(payload.level));
  setAt_(context, row, HEADERS.CATEGORY, String(payload.category).trim());
  setAt_(context, row, HEADERS.TYPE, String(payload.type).trim());
  setAt_(context, row, HEADERS.TASK, String(payload.task).trim());
  setAt_(context, row, HEADERS.EXPIRED_VALUE, Number(payload.expiredValue));
  setAt_(context, row, HEADERS.EXPIRED_UNIT, payload.expiredUnit);
  setAt_(context, row, HEADERS.WARNING_VALUE, Number(payload.warningValue));
  setAt_(context, row, HEADERS.WARNING_UNIT, payload.warningUnit);
  setAt_(context, row, HEADERS.ALERT, bool_(payload.alert));
  setAt_(context, row, HEADERS.HISTORY, bool_(payload.history));
  const hasRelationship = Object.prototype.hasOwnProperty.call(payload, 'predecessorTaskId');
  const predecessorTaskId = hasRelationship
    ? String(payload.predecessorTaskId || '').trim()
    : context.sheet.getRange(row, context.indexes[HEADERS.PREDECESSOR_TASK_ID]).getDisplayValue().trim();
  if (hasRelationship) {
    setAt_(context, row, HEADERS.PREDECESSOR_TASK_ID, predecessorTaskId);
    setAt_(context, row, HEADERS.LINKED_UNLOCKED, predecessorTaskId ? bool_(payload.isLinkedUnlocked) : true);
    setAt_(context, row, HEADERS.LINKED_ACTIVATION_DATE, predecessorTaskId ? parseDate_(payload.linkedActivationDate) || '' : '');
  }
  if (Object.prototype.hasOwnProperty.call(payload, 'paused')) setAt_(context, row, HEADERS.PAUSED, bool_(payload.paused));
  if (Object.prototype.hasOwnProperty.call(payload, 'resumeDate')) setAt_(context, row, HEADERS.RESUME_DATE, parseDate_(payload.resumeDate) || '');
  if (recurrenceAnchor) {
    context.sheet.getRange(row, context.indexes[HEADERS.EXPIRED_DATE]).setValue(expiredDate).setNumberFormat('dd MMM yyyy');
    context.sheet.getRange(row, context.indexes[HEADERS.WARNING_DATE]).setValue(warningDate).setNumberFormat('dd MMM yyyy');
  }
}

function validateRelationship_(context, taskId, predecessorTaskId) {
  const sourceId = String(predecessorTaskId || '').trim();
  if (!sourceId) return;
  if (sourceId === taskId) throw new Error('A task cannot link to itself.');
  const sourceRow = findTaskRow_(context, sourceId);
  if (!sourceRow) throw new Error(`Linked source not found: ${sourceId}`);
  const source = taskFromRow_(context, sourceRow);
  if (source.archived) throw new Error('The selected main task is archived.');
  const byId = new Map();
  for (let row = 2; row <= context.sheet.getLastRow(); row++) {
    const candidate = taskFromRow_(context, row);
    if (candidate.taskId) byId.set(candidate.taskId, candidate);
  }
  const visited = new Set();
  let current = source;
  while (current && current.predecessorTaskId) {
    if (visited.has(current.taskId) || current.predecessorTaskId === taskId) {
      throw new Error('This main task would create a linked-task cycle.');
    }
    visited.add(current.taskId);
    current = byId.get(current.predecessorTaskId);
  }
}

function syncMinorTasks_(taskId, minorTasks, operationId) {
  const context = minorTaskContext_();
  const incomingIds = new Set();
  minorTasks.forEach((minor, index) => {
    const id = String(minor.minorTaskId || '').trim();
    const name = String(minor.name || '').trim();
    if (!id || !name) throw new Error('Minor task ID and name are required.');
    if (incomingIds.has(id)) throw new Error(`Duplicate minor task in payload: ${id}`);
    incomingIds.add(id);
    const intervalValue = minor.intervalValue === null || minor.intervalValue === undefined || minor.intervalValue === ''
      ? null
      : Number(minor.intervalValue);
    if (intervalValue !== null && (!Number.isFinite(intervalValue) || intervalValue <= 0)) throw new Error(`Invalid minor interval: ${name}`);
    if (intervalValue !== null && !['Day', 'Month', 'Year'].includes(minor.intervalUnit)) throw new Error(`Invalid minor interval unit: ${name}`);
    let row = findMinorTaskRow_(context, id);
    if (!row) row = Math.max(context.sheet.getLastRow() + 1, 2);
    const existingParent = String(context.sheet.getRange(row, context.indexes[MINOR_HEADERS.PARENT_ID]).getDisplayValue() || '').trim();
    if (existingParent && existingParent !== taskId) throw new Error(`Minor task belongs to another parent: ${id}`);
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.ID]).setValue(id);
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.PARENT_ID]).setValue(taskId);
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.NAME]).setValue(name);
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.INTERVAL_VALUE]).setValue(intervalValue === null ? '' : intervalValue);
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.INTERVAL_UNIT]).setValue(intervalValue === null ? '' : minor.intervalUnit);
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.LAST_COMPLETED]).setValue(parseDate_(minor.latestCompletionDate) || '').setNumberFormat('dd MMM yyyy');
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.DUE_DATE]).setValue(parseDate_(minor.dueDate) || '').setNumberFormat('dd MMM yyyy');
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.ORDER]).setValue(Number.isFinite(Number(minor.order)) ? Number(minor.order) : index);
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.ARCHIVED]).setValue(bool_(minor.archived));
    context.sheet.getRange(row, context.indexes[MINOR_HEADERS.LAST_OPERATION_ID]).setValue(operationId || '');
  });

  if (context.sheet.getLastRow() < 2) return;
  for (let row = 2; row <= context.sheet.getLastRow(); row++) {
    const parentId = context.sheet.getRange(row, context.indexes[MINOR_HEADERS.PARENT_ID]).getDisplayValue().trim();
    const id = context.sheet.getRange(row, context.indexes[MINOR_HEADERS.ID]).getDisplayValue().trim();
    if (parentId === taskId && id && !incomingIds.has(id)) {
      context.sheet.getRange(row, context.indexes[MINOR_HEADERS.ARCHIVED]).setValue(true);
      context.sheet.getRange(row, context.indexes[MINOR_HEADERS.LAST_OPERATION_ID]).setValue(operationId || '');
    }
  }
}

function attachMinorTasks_(tasks) {
  if (!tasks || tasks.length === 0) return;
  const byParent = minorTasksByParent_();
  tasks.forEach(task => task.minorTasks = byParent.get(task.taskId) || []);
}

function affectedTasksForOperation_(context, primaryTaskId, operationId) {
  const affected = [];
  for (let row = 2; row <= context.sheet.getLastRow(); row++) {
    const task = taskFromRow_(context, row);
    if (task.taskId !== primaryTaskId && task.lastLifeSyncOperationId === operationId) affected.push(task);
  }
  attachMinorTasks_(affected);
  return affected;
}

function validateTaskFields_(payload) {
  if (!String(payload.category || '').trim() || !String(payload.type || '').trim() || !String(payload.task || '').trim()) throw new Error('Category, type, and task are required.');
  if (payload.level !== undefined && payload.level !== null
      && (!Number.isInteger(Number(payload.level)) || Number(payload.level) < 1 || Number(payload.level) > 5)) throw new Error('Level must be an integer from 1 to 5.');
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
    completeCompoundTask_(context, row, parseDate_(e.parameter.executedate) || new Date(), e.parameter.remark || '', `legacy-${Utilities.getUuid()}`, []);
    return json_({ success: true, task: taskFromRow_(context, row) });
  } catch (error) {
    return json_({ success: false, error: error.message || String(error) });
  } finally {
    lock.releaseLock();
  }
}
