import { useCallback, useEffect, useRef, useState } from 'react';
import { getRoutineTasks, RoutineApiError, updateRoutineTask, type RoutineMutationRequest, type RoutineState, type RoutineTask, type RoutineTaskList } from '../api/routineTasks';

type FailedOperation = { request: RoutineMutationRequest; message: string };
const timeZone = 'Europe/Warsaw';

function getWarsawParts(date = new Date()) {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', hourCycle: 'h23' }).formatToParts(date);
  const value = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find(part => part.type === type)?.value);
  return { year: value('year'), month: value('month'), day: value('day'), hour: value('hour') };
}

function nextResetTime() {
  const { year, month, day, hour } = getWarsawParts();
  const resetDate = new Date(Date.UTC(year, month - 1, day + (hour >= 3 ? 1 : 0)));
  const targetAtUtc = Date.UTC(resetDate.getUTCFullYear(), resetDate.getUTCMonth(), resetDate.getUTCDate(), 3);
  const displayedAtTarget = getWarsawParts(new Date(targetAtUtc));
  const offset = Date.UTC(displayedAtTarget.year, displayedAtTarget.month - 1, displayedAtTarget.day, displayedAtTarget.hour) - targetAtUtc;
  return targetAtUtc - offset;
}

function mergeLoadedList(current: RoutineTaskList | null, incoming: RoutineTaskList) {
  if (!current || current.businessDate !== incoming.businessDate) return incoming;
  const currentTasks = new Map(current.tasks.map(task => [task.id, task]));
  return {
    ...incoming,
    tasks: incoming.tasks.map(task => {
      const currentTask = currentTasks.get(task.id);
      return currentTask && currentTask.version > task.version ? currentTask : task;
    }),
  };
}

function mergeOccurrence(current: RoutineTaskList | null, expectedBusinessDate: string, occurrence: RoutineTask) {
  if (!current || current.businessDate !== expectedBusinessDate) return current;
  const currentTask = current.tasks.find(task => task.id === occurrence.id);
  if (!currentTask || occurrence.version < currentTask.version) return current;
  return { ...current, tasks: current.tasks.map(task => task.id === occurrence.id ? occurrence : task) };
}

export function RoutineTasksPage() {
  const [list, setList] = useState<RoutineTaskList | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState<Record<string, boolean>>({});
  const [failed, setFailed] = useState<Record<string, FailedOperation>>({});
  const loadGeneration = useRef(0);

  const load = useCallback(async () => {
    const generation = ++loadGeneration.current;
    setLoading(true); setError('');
    try {
      const incoming = await getRoutineTasks();
      if (generation !== loadGeneration.current) return;
      setList(current => mergeLoadedList(current, incoming));
      setFailed({});
    } catch (exception) {
      if (generation === loadGeneration.current) setError(exception instanceof Error ? exception.message : 'Nie udało się pobrać zadań rutynowych.');
    } finally {
      if (generation === loadGeneration.current) setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), Math.max(0, nextResetTime() - Date.now()) + 100);
    return () => window.clearTimeout(timeout);
  }, [list?.businessDate, load]);

  async function change(task: RoutineTask, target: RoutineState, retry?: RoutineMutationRequest) {
    if (!list) return;
    const request = retry ?? { operationId: crypto.randomUUID(), expectedBusinessDate: list.businessDate, expectedVersion: task.version, state: target };
    setError('');
    setBusy(current => ({ ...current, [task.id]: true }));
    setFailed(current => { const next = { ...current }; delete next[task.id]; return next; });
    try {
      const result = await updateRoutineTask(task.id, request);
      setList(current => mergeOccurrence(current, request.expectedBusinessDate, result.occurrence));
    } catch (exception) {
      if (exception instanceof RoutineApiError && exception.status === 409) {
        const conflict = exception.conflict;
        if (conflict?.occurrence && conflict.businessDate === request.expectedBusinessDate) {
          setList(current => mergeOccurrence(current, conflict.businessDate, conflict.occurrence!));
          setError('Stan zadania został odświeżony po zmianie z innego żądania.');
        } else await load();
      } else {
        setFailed(current => ({ ...current, [task.id]: { request, message: exception instanceof Error ? exception.message : 'Nie udało się zapisać zmiany.' } }));
      }
    } finally { setBusy(current => ({ ...current, [task.id]: false })); }
  }

  const tasks = list?.tasks ?? [];
  return <>
    <header className="page-header"><span className="app-mark" aria-hidden="true">↻</span><div><h1>Zadania rutynowe</h1><p>Twój dzień trwa od 03:00 do 03:00 czasu polskiego.</p></div></header>
    <section className="routine-list" aria-labelledby="routine-heading" aria-busy={loading}>
      <div className="section-heading"><h2 id="routine-heading">Zaplanowane na dziś <span className="count">{tasks.length}</span></h2><button className="text-button" type="button" onClick={() => void load()} disabled={loading}>Odśwież</button></div>
      {loading && <p className="state">Ładowanie zadań rutynowych…</p>}
      {error && <p className="error">{error}</p>}
      {!loading && list && tasks.length === 0 && <div className="empty-state"><span aria-hidden="true">✓</span><h3>Bez zadań rutynowych</h3><p>Na ten dzień nic nie zostało zaplanowane.</p></div>}
      {!loading && tasks.length > 0 && <ul>{tasks.map(task => {
        const isSkipped = task.state === 'skipped';
        const isBusy = busy[task.id] === true;
        const failure = failed[task.id];
        return <li key={task.id} className={`routine-item${isSkipped ? ' skipped' : ''}`}>
          <div className="routine-main">
            <label className="routine-checkbox"><input type="checkbox" checked={task.state === 'completed'} disabled={isSkipped || isBusy} onChange={event => void change(task, event.target.checked ? 'completed' : 'pending')} /><span>{task.name}</span></label>
            <small>{task.effort} · {task.xp} XP</small>
            {failure && <p className="item-error">{failure.message}</p>}
          </div>
          <div className="routine-actions">
            {failure && <button type="button" className="retry-button" disabled={isBusy} onClick={() => void change(task, failure.request.state, failure.request)}>Ponów</button>}
            <button type="button" className="skip-button" disabled={isBusy} onClick={() => void change(task, isSkipped ? 'pending' : 'skipped')}>{isBusy ? 'Zapisywanie…' : isSkipped ? 'Cofnij pominięcie' : 'Pomiń'}</button>
          </div>
        </li>;
      })}</ul>}
    </section>
  </>;
}
