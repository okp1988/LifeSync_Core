const TASK_SHEET = 'Tasks';
const AUDIT_SHEET = 'Audit';
const MINOR_TASK_SHEET = 'Minor Tasks';
const FILTER_SHEET = 'Filters';
const FILTER_MEMBERSHIP_SHEET = 'Filter Memberships';
const TIME_ZONE = 'Asia/Kuala_Lumpur';
const DEFAULT_TASK_LIST = '@default';
const AUDIT_OPERATION_ID_HEADER = 'LifeSync Operation ID';

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
  LEVEL: 'Level',
  TASK_ID: 'Task ID',
  REVISION: 'Revision',
  UPDATED_AT: 'Updated At',
  ARCHIVED: 'Archived',
  SNOOZE_UNTIL: 'Snooze Until',
  SNOOZE_NOTE: 'Snooze Note',
  LAST_GOOGLE_TASK_KEY: 'Last Google Task Key',
  LAST_GOOGLE_TASK_CREATED_DATE: 'Last Google Task Created Date',
  LAST_OPERATION_ID: 'Last LifeSync Operation ID',
  PREDECESSOR_TASK_ID: 'Predecessor Task ID',
  LINKED_UNLOCKED: 'Linked Unlocked',
  LINKED_ACTIVATION_DATE: 'Linked Activation Date',
  PAUSED: 'Paused',
  RESUME_DATE: 'Resume Date'
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
  HEADERS.LAST_OPERATION_ID,
  HEADERS.LEVEL,
  HEADERS.PREDECESSOR_TASK_ID,
  HEADERS.LINKED_UNLOCKED,
  HEADERS.LINKED_ACTIVATION_DATE,
  HEADERS.PAUSED,
  HEADERS.RESUME_DATE
];

const MINOR_HEADERS = Object.freeze({
  ID: 'Minor Task ID',
  PARENT_ID: 'Parent Task ID',
  NAME: 'Minor Task',
  INTERVAL_VALUE: 'Interval Value',
  INTERVAL_UNIT: 'Interval Unit',
  LAST_COMPLETED: 'Last Completed Date',
  DUE_DATE: 'Due Date',
  ORDER: 'Sort Order',
  ARCHIVED: 'Archived',
  LAST_OPERATION_ID: 'Last LifeSync Operation ID'
});

const FILTER_HEADERS = Object.freeze({
  ID: 'Filter ID', NAME: 'Filter Name', SYSTEM: 'System Filter', FAVOURITE: 'Favourite',
  SORT_ORDER: 'Sort Order', UPDATED_AT: 'Updated At', LAST_OPERATION_ID: 'Last LifeSync Operation ID'
});

const FILTER_MEMBERSHIP_HEADERS = Object.freeze({
  FILTER_ID: 'Filter ID', TASK_ID: 'Task ID', UPDATED_AT: 'Updated At', LAST_OPERATION_ID: 'Last LifeSync Operation ID'
});

function getApiToken_() {
  return PropertiesService.getScriptProperties().getProperty('LIFESYNC_API_TOKEN') || 'LIFESYNC';
}
