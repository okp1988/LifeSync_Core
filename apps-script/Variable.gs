const TASK_SHEET = 'Tasks';
const AUDIT_SHEET = 'Audit';
const TIME_ZONE = 'Asia/Kuala_Lumpur';
const DEFAULT_TASK_LIST = '@default';

const HEADERS = Object.freeze({
  CHECKBOX: 'Complete',
  CATEGORY: 'Category',
  TYPE: 'Type',
  TASK: 'Task',
  EXPIRED_DATE: 'Expired Date',
  WARNING_DATE: 'Warning Date',
  DAY_LEFT: 'Day Left',
  PREV_DATE_1: 'Prev Date 01',
  PREV_DATE_2: 'Prev Date 02',
  REMARK: 'Remark',
  LAST_EXECUTED_DATE: 'Last Executed Date',
  EXECUTED_DATE: 'Executed Date',
  EXPIRED_VALUE: 'Expired Value',
  EXPIRED_UNIT: 'Expired Unit',
  WARNING_VALUE: 'Warning Value',
  WARNING_UNIT: 'Warning Unit',
  ALERT: 'Alert',
  HISTORY: 'History',
  LAST_GOOGLE_TASK_ID: 'Last Google Task ID',
  TASK_ID: 'Task ID',
  REVISION: 'Revision',
  UPDATED_AT: 'Updated At',
  ARCHIVED: 'Archived',
  SNOOZE_UNTIL: 'Snooze Until',
  SNOOZE_NOTE: 'Snooze Note',
  LAST_GOOGLE_TASK_KEY: 'Last Google Task Key',
  LAST_GOOGLE_TASK_CREATED_DATE: 'Last Google Task Created Date',
  LAST_OPERATION_ID: 'Last LifeSync Operation ID'
});

const SYSTEM_HEADERS = [
  HEADERS.TASK_ID,
  HEADERS.REVISION,
  HEADERS.UPDATED_AT,
  HEADERS.ARCHIVED,
  HEADERS.SNOOZE_UNTIL,
  HEADERS.SNOOZE_NOTE,
  HEADERS.LAST_GOOGLE_TASK_KEY,
  HEADERS.LAST_GOOGLE_TASK_CREATED_DATE,
  HEADERS.LAST_OPERATION_ID
];

function getApiToken_() {
  return PropertiesService.getScriptProperties().getProperty('LIFESYNC_API_TOKEN') || 'LIFESYNC';
}
