import { useCallback, useEffect, useRef, useState } from 'react';
import type { InboxItem } from '../api/inbox';
import type { RoutineTask, RoutineTaskList } from '../api/routineTasks';
import type { TodayTask } from '../api/todayTasks';
import type { XpPeriod } from '../api/xpProgress';
import { businessDate, periodStart } from './businessTime';

type Correction = { kind: 'inbox-upsert'; item: InboxItem } | { kind: 'inbox-remove'; id: string } |
  { kind: 'today-status'; id: string; status: string } | { kind: 'today-remove'; id: string };
type Entry = { key: string; businessDate: string; data?: unknown; fetchedAt?: number; attemptedAt?: number; error?: string; invalidated: boolean; corrections?: Correction[] };
export type CacheView<T> = { data?: T; fetchedAt?: number; error?: string; loading: boolean };
export type XpSelection = { period: XpPeriod; start: string; businessDate: string };

const entries = new Map<string, Entry>();
const listeners = new Set<() => void>();
const flights = new Map<string, Promise<void>>();
const generations = new Map<string, number>();
let hydration: Promise<void> | undefined;
let selection: XpSelection | undefined;
let pendingWrite = Promise.resolve();
const databaseName = 'notion-management-screen-cache-v1';

function notify() { for (const listener of listeners) listener(); }
function isEntry(value: unknown): value is Entry {
  if (!value || typeof value !== 'object') return false;
  const item = value as Partial<Entry>;
  return typeof item.key === 'string' && typeof item.businessDate === 'string' &&
    typeof item.invalidated === 'boolean' && (item.fetchedAt === undefined || Number.isFinite(item.fetchedAt)) &&
    (item.attemptedAt === undefined || Number.isFinite(item.attemptedAt)) &&
    (item.error === undefined || typeof item.error === 'string') &&
    (item.corrections === undefined || Array.isArray(item.corrections) && item.corrections.every(validCorrection)) && validData(item.key, item.data);
}

function validCorrection(value: unknown) {
  if (!value || typeof value !== 'object') return false;
  const item = value as Record<string, unknown>;
  if (item.kind === 'inbox-upsert') return !!item.item && typeof item.item === 'object' && typeof (item.item as InboxItem).id === 'string' && typeof (item.item as InboxItem).name === 'string';
  if (item.kind === 'inbox-remove' || item.kind === 'today-remove') return typeof item.id === 'string';
  if (item.kind === 'today-status') return typeof item.id === 'string' && typeof item.status === 'string';
  return false;
}

function validData(key: string, data: unknown) {
  if (data === undefined) return true;
  if (key === 'inbox') return Array.isArray(data) && data.every(item => item && typeof item.id === 'string' && typeof item.name === 'string');
  if (!data || typeof data !== 'object') return false;
  const value = data as Record<string, unknown>;
  if (key === 'today') return Array.isArray(value.tasks) && value.tasks.every(item => item && typeof item.id === 'string' && typeof item.status === 'string' && typeof item.name === 'string' && Array.isArray(item.projects) && item.projects.every((project: unknown) => typeof project === 'string') && (item.effort === null || typeof item.effort === 'string')) && Array.isArray(value.statuses) && value.statuses.every(item => item && typeof item.name === 'string' && typeof item.color === 'string');
  if (key === 'routines') return typeof value.businessDate === 'string' && Array.isArray(value.tasks) && value.tasks.every(item => item && typeof item.id === 'string' && typeof item.name === 'string' && typeof item.effort === 'string' && Number.isInteger(item.xp) && Number.isInteger(item.version) && ['pending', 'completed', 'skipped'].includes(item.state));
  if (key.startsWith('xp:')) return key === `xp:${value.period}:${value.periodStart}` && typeof value.periodEndExclusive === 'string' && Number.isInteger(value.earnedXp) && Number.isInteger(value.targetXp) && (value.previousPeriodStart === null || typeof value.previousPeriodStart === 'string') && (value.completedTasks === null || Array.isArray(value.completedTasks) && value.completedTasks.every(item => item && typeof item.id === 'string' && typeof item.taskName === 'string' && typeof item.completedAt === 'string' && typeof item.businessDate === 'string' && (item.subjectType === 'task' || item.subjectType === 'routine') && typeof item.subjectId === 'string' && (item.observedEffort === null || typeof item.observedEffort === 'string') && (item.projects === null || Array.isArray(item.projects) && item.projects.every((project: unknown) => typeof project === 'string'))));
  return false;
}

function openDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(databaseName, 1);
    request.onupgradeneeded = () => { request.result.createObjectStore('records'); };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function readRecords(): Promise<unknown[]> {
  const db = await openDatabase();
  try {
    return await new Promise((resolve, reject) => {
      const request = db.transaction('records', 'readonly').objectStore('records').getAll();
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  } finally { db.close(); }
}

function writeRecord(key: string, value: unknown) {
  pendingWrite = pendingWrite.then(async () => {
    try {
      const db = await openDatabase();
      try {
        await new Promise<void>((resolve, reject) => {
          const transaction = db.transaction('records', 'readwrite');
          transaction.objectStore('records').put(value, key);
          transaction.oncomplete = () => resolve();
          transaction.onerror = () => reject(transaction.error);
        });
      } finally { db.close(); }
    } catch { /* Storage is optional; requests still work without it. */ }
  });
  return pendingWrite;
}

function deleteRecord(key: string) {
  pendingWrite = pendingWrite.then(async () => {
    try {
      const db = await openDatabase();
      try {
        await new Promise<void>((resolve, reject) => {
          const transaction = db.transaction('records', 'readwrite');
          transaction.objectStore('records').delete(key);
          transaction.oncomplete = () => resolve();
          transaction.onerror = () => reject(transaction.error);
        });
      } finally { db.close(); }
    } catch { /* Storage is optional. */ }
  });
  return pendingWrite;
}

export function hydrateCache() {
  hydration ??= (async () => {
    try {
      const records = await readRecords();
      const day = businessDate();
      for (const record of records) {
        if (isEntry(record)) {
          if (record.businessDate === day && !entries.has(record.key)) entries.set(record.key, record);
          else if (record.businessDate !== day) void deleteRecord(record.key);
        }
        if (record && typeof record === 'object' && 'selection' in record) {
          const saved = record.selection as XpSelection;
          if (saved && saved.businessDate === day && ['day', 'week', 'month', 'year'].includes(saved.period) && typeof saved.start === 'string') selection = saved;
        }
      }
    } catch { /* A missing or unavailable database is a cache miss. */ }
    notify();
  })();
  return hydration;
}

export async function getXpSelection() { await hydrateCache(); return selection; }
export function saveXpSelection(value: XpSelection) {
  selection = value;
  void writeRecord('selection', { selection: value });
}

export function discardOldDay(day: string) {
  for (const [key, entry] of entries) {
    if (entry.businessDate !== day) { entries.delete(key); void deleteRecord(key); }
  }
  if (selection?.businessDate !== day) selection = undefined;
  notify();
}

function currentEntry(key: string, day: string) {
  const entry = entries.get(key);
  return entry?.businessDate === day ? entry : undefined;
}

function view<T>(key: string, day: string): CacheView<T> {
  const entry = currentEntry(key, day);
  return { data: entry?.data as T | undefined, fetchedAt: entry?.fetchedAt, error: entry?.error,
    loading: flights.has(`${day}:${key}`) };
}

function applyCorrections(key: string, incoming: unknown, previous?: Entry): { data: unknown; corrections?: Correction[] } {
  if (key === 'routines' && previous?.data) {
    const prior = previous.data as RoutineTaskList;
    const next = incoming as RoutineTaskList;
    if (prior.businessDate === next.businessDate) {
      const versions = new Map(prior.tasks.map(task => [task.id, task]));
      return { data: { ...next, tasks: next.tasks.map(task => {
        const old = versions.get(task.id);
        return old && old.version > task.version ? old : task;
      }) } };
    }
  }
  const corrections = previous?.corrections ?? [];
  if (corrections.length === 0) return { data: incoming };
  const outstanding: Correction[] = [];
  if (key === 'inbox') {
    let items = incoming as InboxItem[];
    for (const correction of corrections) {
      if (correction.kind === 'inbox-upsert') {
        const found = items.find(item => item.id === correction.item.id);
        if (found?.name === correction.item.name) continue;
        items = [correction.item, ...items.filter(item => item.id !== correction.item.id)];
      } else if (correction.kind === 'inbox-remove') {
        items = items.filter(item => item.id !== correction.id);
      }
      outstanding.push(correction);
    }
    return { data: items, corrections: outstanding };
  }
  if (key === 'today') {
    const result = incoming as { tasks: TodayTask[]; statuses: unknown[] };
    let tasks = result.tasks;
    for (const correction of corrections) {
      if (correction.kind === 'today-status') {
        const found = tasks.find(task => task.id === correction.id);
        if (found?.status === correction.status) continue;
        tasks = tasks.map(task => task.id === correction.id ? { ...task, status: correction.status } : task);
      } else if (correction.kind === 'today-remove') {
        tasks = tasks.filter(task => task.id !== correction.id);
      }
      outstanding.push(correction);
    }
    return { data: { ...result, tasks }, corrections: outstanding };
  }
  return { data: incoming };
}

export function requestData<T>(key: string, day: string, loader: () => Promise<T>, force = false) {
  const flightKey = `${day}:${key}`;
  const existing = flights.get(flightKey);
  if (existing) return existing;
  const entry = currentEntry(key, day);
  if (!force && entry && !entry.invalidated && entry.attemptedAt !== undefined && Date.now() - entry.attemptedAt < 60_000) return Promise.resolve();
  if (!entry) entries.set(key, { key, businessDate: day, invalidated: false });
  const generation = (generations.get(key) ?? 0) + 1;
  generations.set(key, generation);
  const flight = (async () => {
    try {
      const result = await loader();
      if (businessDate() !== day || generations.get(key) !== generation) return;
      if (key === 'routines' && (result as RoutineTaskList).businessDate !== day) throw new Error('Dane rutyn dotyczą innego dnia. Odśwież ponownie.');
      const corrected = applyCorrections(key, result, currentEntry(key, day));
      const updated: Entry = { key, businessDate: day, data: corrected.data, corrections: corrected.corrections,
        fetchedAt: Date.now(), attemptedAt: Date.now(), invalidated: false };
      entries.set(key, updated); notify(); void writeRecord(key, updated);
    } catch (error) {
      if (businessDate() !== day || generations.get(key) !== generation) return;
      const updated: Entry = { ...(currentEntry(key, day) ?? { key, businessDate: day, invalidated: false }),
        attemptedAt: Date.now(), error: error instanceof Error ? error.message : 'Nie udało się pobrać danych', invalidated: false };
      entries.set(key, updated); notify(); void writeRecord(key, updated);
    }
  })();
  flights.set(flightKey, flight);
  notify();
  void flight.finally(() => { if (flights.get(flightKey) === flight) { flights.delete(flightKey); notify(); } });
  return flight;
}

export function useCachedResource<T>(key: string, day: string, loader: () => Promise<T>, revisit: number) {
  const loaderRef = useRef(loader);
  loaderRef.current = loader;
  const [state, setState] = useState(() => ({ key, day, value: view<T>(key, day) }));
  useEffect(() => {
    let active = true;
    const update = () => { if (active) setState({ key, day, value: view<T>(key, day) }); };
    listeners.add(update);
    update();
    void hydrateCache().then(() => { if (active) { update(); void requestData(key, day, () => loaderRef.current()); } });
    return () => { active = false; listeners.delete(update); };
  }, [key, day, revisit]);
  const refresh = useCallback(() => requestData(key, day, () => loaderRef.current(), true), [key, day]);
  return { ...(state.key === key && state.day === day ? state.value : view<T>(key, day)), refresh };
}

function changeEntry(key: string, day: string, transform: (entry: Entry) => Entry) {
  const entry = currentEntry(key, day) ?? { key, businessDate: day, invalidated: true };
  const updated = transform(entry);
  entries.set(key, updated);
  generations.set(key, (generations.get(key) ?? 0) + 1);
  flights.delete(`${day}:${key}`);
  notify(); void writeRecord(key, updated);
}

export function invalidate(key: string, day = businessDate()) {
  changeEntry(key, day, entry => ({ ...entry, invalidated: true }));
}

export function invalidateXp(day = businessDate()) {
  for (const key of entries.keys()) {
    if (!key.startsWith('xp:')) continue;
    const [, period, start] = key.split(':') as [string, XpPeriod, string];
    if (periodStart(period, day) === start) invalidate(key, day);
  }
}

export function xpKey(period: XpPeriod, start: string) { return `xp:${period}:${start}`; }

export function correctInbox(correction: Correction, day = businessDate()) {
  changeEntry('inbox', day, entry => {
    const id = 'item' in correction ? correction.item.id : correction.id;
    const corrections = [...(entry.corrections ?? []).filter(item => ('item' in item ? item.item.id : item.id) !== id), correction];
    const data = entry.data as InboxItem[] | undefined;
    const updated = correction.kind === 'inbox-upsert' ? [correction.item, ...(data ?? []).filter(item => item.id !== correction.item.id)] :
      data && correction.kind === 'inbox-remove' ? data.filter(item => item.id !== correction.id) : data;
    return { ...entry, data: updated, corrections, invalidated: true };
  });
}

export function correctToday(correction: Correction, day = businessDate()) {
  changeEntry('today', day, entry => {
    const result = entry.data as { tasks: TodayTask[]; statuses: unknown[] } | undefined;
    const tasks = result && correction.kind === 'today-status' ? result.tasks.map(task => task.id === correction.id ? { ...task, status: correction.status } : task) :
      result && correction.kind === 'today-remove' ? result.tasks.filter(task => task.id !== correction.id) : result?.tasks;
    const id = 'item' in correction ? correction.item.id : correction.id;
    return { ...entry, data: result && { ...result, tasks }, corrections: [...(entry.corrections ?? []).filter(item => ('item' in item ? item.item.id : item.id) !== id), correction], invalidated: true };
  });
}

export function correctRoutine(occurrence: RoutineTask, day: string) {
  changeEntry('routines', day, entry => {
    const list = entry.data as RoutineTaskList | undefined;
    if (!list) return { ...entry, invalidated: true };
    return { ...entry, data: { ...list, tasks: list.tasks.map(task => task.id === occurrence.id && occurrence.version >= task.version ? occurrence : task) }, invalidated: true };
  });
}
