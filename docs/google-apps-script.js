const SHEET_NAME = 'Tasks';
const API_KEY = 'change-me';

const HEADERS = [
  'Category',
  'Type',
  'Task',
  'Expired Date',
  'Warning Date',
  'Day Left',
  'Prev Date 1',
  'Prev Date 2',
  'Remark',
  'Completed'
];

function doGet(e) {
  assertKey_(e.parameter.key);

  if (e.parameter.action !== 'tasks') {
    return json_({ error: 'Unknown action' }, 400);
  }

  const sheet = SpreadsheetApp.getActive().getSheetByName(SHEET_NAME);
  const values = sheet.getDataRange().getValues();
  const headers = values.shift();
  const indexes = headerIndexes_(headers);

  const tasks = values
    .filter(row => !row[indexes['Completed']])
    .map(row => ({
      category: value_(row[indexes['Category']]),
      type: value_(row[indexes['Type']]),
      task: value_(row[indexes['Task']]),
      expiredDate: date_(row[indexes['Expired Date']]),
      warningDate: date_(row[indexes['Warning Date']]),
      dayLeft: numberOrNull_(row[indexes['Day Left']]),
      previousDate1: date_(row[indexes['Prev Date 1']]),
      previousDate2: date_(row[indexes['Prev Date 2']]),
      remark: value_(row[indexes['Remark']]),
      rowNumber: values.indexOf(row) + 2,
      trackId: String(values.indexOf(row) + 2)
    }));

  return json_({ tasks });
}

function doPost(e) {
  const payload = JSON.parse(e.postData.contents || '{}');
  assertKey_(payload.key);

  if (payload.action === 'updateRemark') {
    updateRemark_(payload.rowNumber, payload.remark);
    return json_({ ok: true });
  }

  if (payload.action === 'complete') {
    updateRemark_(payload.rowNumber, payload.remark);
    markComplete_(payload.rowNumber);
    runCompletionLogic_(payload.rowNumber);
    return json_({ ok: true });
  }

  return json_({ error: 'Unknown action' }, 400);
}

function updateRemark_(rowNumber, remark) {
  const context = findRow_(rowNumber);
  context.sheet.getRange(context.rowNumber, context.indexes['Remark'] + 1).setValue(remark || '');
}

function markComplete_(rowNumber) {
  const context = findRow_(rowNumber);
  context.sheet.getRange(context.rowNumber, context.indexes['Completed'] + 1).setValue(true);
}

function runCompletionLogic_(rowNumber) {
  // Put your existing completion script logic here.
}

function findRow_(rowNumber) {
  const sheet = SpreadsheetApp.getActive().getSheetByName(SHEET_NAME);
  const values = sheet.getDataRange().getValues();
  const headers = values[0];
  const indexes = headerIndexes_(headers);
  const parsedRowNumber = Number(rowNumber);

  if (Number.isInteger(parsedRowNumber) && parsedRowNumber >= 2 && parsedRowNumber <= values.length) {
    return { sheet, indexes, rowNumber: parsedRowNumber };
  }

  throw new Error(`Row number not found: ${rowNumber}`);
}

function headerIndexes_(headers) {
  const indexes = {};
  headers.forEach((header, index) => indexes[String(header)] = index);
  HEADERS.forEach(header => {
    if (!(header in indexes)) {
      throw new Error(`Missing header: ${header}`);
    }
  });
  return indexes;
}

function assertKey_(key) {
  if (key !== API_KEY) {
    throw new Error('Invalid API key');
  }
}

function json_(value) {
  return ContentService
    .createTextOutput(JSON.stringify(value))
    .setMimeType(ContentService.MimeType.JSON);
}

function value_(value) {
  return value == null ? '' : String(value);
}

function date_(value) {
  return value instanceof Date ? value.toISOString() : null;
}

function numberOrNull_(value) {
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}
