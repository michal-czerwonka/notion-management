import { useEffect, useMemo, useState } from 'react';
import routineTasks from '../config/routine-tasks.json';

type DayOfWeek = 'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday' | 'sunday';
type RoutineTask = { id: string; name: string; daysOfWeek: DayOfWeek[] };
type TaskStatus = 'completed' | 'skipped';
type StoredRoutineState = { periodKey: string; statuses: Record<string, TaskStatus> };

const timeZone = 'Europe/Warsaw';
const storageKey = 'routine-tasks-state';
const weekdays: DayOfWeek[] = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'];

function getWarsawParts(date = new Date()) {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date);
  const value = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find(part => part.type === type)?.value);
  return { year: value('year'), month: value('month'), day: value('day'), hour: value('hour') };
}

function getPeriod(date = new Date()) {
  const { year, month, day, hour } = getWarsawParts(date);
  const periodDate = new Date(Date.UTC(year, month - 1, day - (hour < 3 ? 1 : 0)));
  const key = periodDate.toISOString().slice(0, 10);
  const weekday = weekdays[periodDate.getUTCDay()];
  return { key, weekday };
}

function loadState(periodKey: string): StoredRoutineState {
  try {
    const value = localStorage.getItem(storageKey);
    if (value) {
      const state = JSON.parse(value) as StoredRoutineState;
      if (state.periodKey === periodKey && state.statuses && typeof state.statuses === 'object') return state;
    }
  } catch {
    // A malformed local value must not prevent access to the routine list.
  }

  return { periodKey, statuses: {} };
}

function saveState(state: StoredRoutineState) {
  localStorage.setItem(storageKey, JSON.stringify(state));
}

function nextResetTime() {
  const now = new Date();
  const { year, month, day, hour } = getWarsawParts(now);
  const resetDate = new Date(Date.UTC(year, month - 1, day + (hour >= 3 ? 1 : 0)));
  const targetAtUtc = Date.UTC(resetDate.getUTCFullYear(), resetDate.getUTCMonth(), resetDate.getUTCDate(), 3);
  const displayedAtTarget = getWarsawParts(new Date(targetAtUtc));
  const offset = Date.UTC(displayedAtTarget.year, displayedAtTarget.month - 1, displayedAtTarget.day, displayedAtTarget.hour) - targetAtUtc;
  return targetAtUtc - offset;
}

export function RoutineTasksPage() {
  const [period, setPeriod] = useState(() => getPeriod());
  const [state, setState] = useState(() => loadState(getPeriod().key));
  const scheduledTasks = useMemo(
    () => (routineTasks as RoutineTask[]).filter(task => task.daysOfWeek.includes(period.weekday)),
    [period.weekday],
  );

  useEffect(() => {
    const reset = () => {
      const newPeriod = getPeriod();
      setPeriod(newPeriod);
      setState(loadState(newPeriod.key));
    };
    const timeout = window.setTimeout(reset, Math.max(0, nextResetTime() - Date.now()) + 50);
    return () => window.clearTimeout(timeout);
  }, [period.key]);

  function setStatus(taskId: string, status: TaskStatus | undefined) {
    const currentPeriod = getPeriod();
    const current = currentPeriod.key === state.periodKey ? state : loadState(currentPeriod.key);
    const statuses = { ...current.statuses };
    if (status) statuses[taskId] = status;
    else delete statuses[taskId];
    const updated = { periodKey: currentPeriod.key, statuses };
    saveState(updated);
    setPeriod(currentPeriod);
    setState(updated);
  }

  return (
    <>
      <header className="page-header">
        <span className="app-mark" aria-hidden="true">↻</span>
        <div><h1>Zadania rutynowe</h1><p>Twój dzień trwa od 03:00 do 03:00 czasu polskiego.</p></div>
      </header>

      <section className="routine-list" aria-labelledby="routine-heading">
        <div className="section-heading"><h2 id="routine-heading">Zaplanowane na dziś <span className="count">{scheduledTasks.length}</span></h2></div>
        {scheduledTasks.length === 0 ? (
          <div className="empty-state"><span aria-hidden="true">✓</span><h3>Bez zadań rutynowych</h3><p>Na ten dzień nic nie zostało zaplanowane.</p></div>
        ) : (
          <ul>{scheduledTasks.map(task => {
            const status = state.statuses[task.id];
            const isSkipped = status === 'skipped';
            return (
              <li key={task.id} className={`routine-item${isSkipped ? ' skipped' : ''}`}>
                <label className="routine-checkbox">
                  <input type="checkbox" checked={status === 'completed'} disabled={isSkipped}
                    onChange={event => setStatus(task.id, event.target.checked ? 'completed' : undefined)} />
                  <span>{task.name}</span>
                </label>
                <button type="button" className="skip-button" onClick={() => setStatus(task.id, isSkipped ? undefined : 'skipped')}>
                  {isSkipped ? 'Cofnij pominięcie' : 'Pomiń'}
                </button>
              </li>
            );
          })}</ul>
        )}
      </section>
    </>
  );
}
