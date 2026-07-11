function checkExpiredAndCreateGoogleTask() {
  const lock = LockService.getDocumentLock();
  lock.waitLock(30000);
  try {
    const context = taskContext_();
    requireHeaders_(context, Object.values(HEADERS));
    const today = parseDate_(new Date());
    const todayKey = dateText_(today).replaceAll('-', '');

    for (let row = 2; row <= context.sheet.getLastRow(); row++) {
      const task = taskFromRow_(context, row);
      if (!task.taskId || !task.category || !task.task || task.archived || !task.alert) continue;
      const warning = parseDate_(task.warningDate);
      const expired = parseDate_(task.expiredDate);
      if (!warning || !expired) continue;
      const snoozeUntil = parseDate_(task.snoozeUntil);
      if (snoozeUntil && snoozeUntil >= today) continue;

      const stage = reminderStage_(today, warning, expired);
      if (!stage) continue;
      const cycleKey = dateText_(expired).replaceAll('-', '');
      const reminderKey = `${task.taskId}|${cycleKey}|${stage.key}`;
      if (task.lastGoogleTaskKey === reminderKey) continue;

      const dueDate = stage.kind === 'warning' ? warning : stage.kind === 'expired' ? expired : today;
      const created = Task_Tracker_Push.Tasks.insert({
        title: `${stage.title}: ${task.task}`,
        due: toTasksDueISO_(dueDate),
        notes: buildReminderNotes_(task, stage)
      }, DEFAULT_TASK_LIST);

      setAt_(context, row, HEADERS.LAST_GOOGLE_TASK_ID, created.id || '');
      setAt_(context, row, HEADERS.LAST_GOOGLE_TASK_KEY, reminderKey);
      setAt_(context, row, HEADERS.LAST_GOOGLE_TASK_CREATED_DATE, today);
    }
  } finally {
    lock.releaseLock();
  }
}

function reminderStage_(today, warning, expired) {
  if (today < warning) return null;
  if (warning < expired && today < expired) return { key: 'warning', kind: 'warning', title: 'Warning', overdueDays: 0 };
  const overdueDays = wholeDays_(expired, today);
  if (overdueDays < 7) return { key: 'expired', kind: 'expired', title: 'Expired', overdueDays };
  const slot = Math.floor(overdueDays / 7);
  return { key: `overdue-${slot}`, kind: 'overdue', title: 'Overdue', overdueDays };
}

function wholeDays_(from, to) {
  const fromKey = Date.UTC(from.getFullYear(), from.getMonth(), from.getDate());
  const toKey = Date.UTC(to.getFullYear(), to.getMonth(), to.getDate());
  return Math.floor((toKey - fromKey) / 86400000);
}

function toTasksDueISO_(localDate) {
  const date = new Date(localDate);
  date.setHours(12, 0, 0, 0);
  return date.toISOString();
}

function buildReminderNotes_(task, stage) {
  const lines = [
    `Category: ${task.category}`,
    `Type: ${task.type}`,
    `Warning: ${task.warningDate}`,
    `Expired: ${task.expiredDate}`
  ];
  if (stage.overdueDays > 0) lines.push(`Overdue: ${stage.overdueDays} days`);
  if (task.remark) lines.push('', task.remark);
  return lines.join('\n');
}

function setupLifeSyncTriggers() {
  ScriptApp.getProjectTriggers().forEach(trigger => {
    if (['checkExpiredAndCreateGoogleTask', 'logCompletedTasksToSheet'].includes(trigger.getHandlerFunction())) {
      ScriptApp.deleteTrigger(trigger);
    }
  });
  ScriptApp.newTrigger('checkExpiredAndCreateGoogleTask').timeBased().everyDays(1).atHour(6).create();
}
