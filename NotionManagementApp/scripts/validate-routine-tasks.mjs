import { readFile } from 'node:fs/promises';

const weekdays = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];
const configurationUrl = new URL('../../config/routine-tasks.json', import.meta.url);
const efforts = ['Trivial', 'Easy', 'Medium', 'Hard', 'Epic'];

function fail(message) {
  throw new Error(`Invalid routine tasks configuration: ${message}`);
}

function isRecord(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

const contents = await readFile(configurationUrl, 'utf8');
let schedule;

try {
  schedule = JSON.parse(contents);
} catch {
  fail('the JSON cannot be parsed.');
}

if (!isRecord(schedule)) fail('the top-level value must be an object.');

const configuredWeekdays = Object.keys(schedule);
for (const weekday of weekdays) {
  if (!Object.hasOwn(schedule, weekday)) fail(`missing weekday "${weekday}".`);
}
for (const weekday of configuredWeekdays) {
  if (!weekdays.includes(weekday)) fail(`unsupported weekday "${weekday}".`);
}

const namesById = new Map();
for (const weekday of weekdays) {
  const tasks = schedule[weekday];
  if (!Array.isArray(tasks)) fail(`weekday "${weekday}" must contain an array.`);

  const ids = new Set();
  for (const [index, task] of tasks.entries()) {
    const location = `weekday "${weekday}", task ${index + 1}`;
    if (!isRecord(task)) fail(`${location} must be an object.`);

    const properties = Object.keys(task);
    if (properties.length !== 3 || !properties.includes('id') || !properties.includes('name') || !properties.includes('effort')) {
      fail(`${location} must contain only "id", "name", and "effort".`);
    }
    if (typeof task.id !== 'string' || task.id.trim() === '') fail(`${location} has an invalid id.`);
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(task.id)) fail(`${location} has an id that is not stable kebab-case.`);
    if (typeof task.name !== 'string' || task.name.trim() === '') fail(`${location} has an invalid name.`);
    if (typeof task.effort !== 'string' || !efforts.includes(task.effort)) fail(`${location} has an unsupported effort.`);
    if (ids.has(task.id)) fail(`${location} repeats id "${task.id}" within the weekday.`);

    ids.add(task.id);
    const knownName = namesById.get(task.id);
    if (knownName !== undefined && knownName !== task.name) {
      fail(`${location} uses name "${task.name}" for id "${task.id}", which is already named "${knownName}".`);
    }
    namesById.set(task.id, task.name);
  }
}
