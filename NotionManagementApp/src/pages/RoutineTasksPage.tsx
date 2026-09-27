import { useEffect, useState } from 'react';
import { getRoutineTasks, RoutineApiError, updateRoutineTask, type RoutineMutationRequest, type RoutineState, type RoutineTask, type RoutineTaskList } from '../api/routineTasks';
import { CacheHeader } from '../cache/CacheHeader';
import { businessDate } from '../cache/businessTime';
import { correctRoutine, invalidate, invalidateXp, useCachedResource } from '../cache/screenCache';

type FailedOperation = { request: RoutineMutationRequest; message: string };
export function RoutineTasksPage({ day, revisit }: { day: string; revisit: number }) {
  const cache = useCachedResource<RoutineTaskList>('routines', day, getRoutineTasks, revisit);
  const list = cache.data ?? null;
  const [error, setError] = useState('');
  const [busy, setBusy] = useState<Record<string, boolean>>({});
  const [failed, setFailed] = useState<Record<string, FailedOperation>>({});
  useEffect(() => { setFailed({}); }, [cache.fetchedAt]);

  async function change(task: RoutineTask, target: RoutineState, retry?: RoutineMutationRequest) {
    if (!list) return;
    const request = retry ?? { operationId: crypto.randomUUID(), expectedBusinessDate: list.businessDate, expectedVersion: task.version, state: target };
    setError('');
    setBusy(current => ({ ...current, [task.id]: true }));
    setFailed(current => { const next = { ...current }; delete next[task.id]; return next; });
    try {
      const result = await updateRoutineTask(task.id, request);
      correctRoutine(result.occurrence, request.expectedBusinessDate);
      invalidateXp(request.expectedBusinessDate);
    } catch (exception) {
      if (exception instanceof RoutineApiError && exception.status === 409) {
        const conflict = exception.conflict;
        if (conflict?.occurrence && conflict.businessDate === request.expectedBusinessDate) {
          correctRoutine(conflict.occurrence, conflict.businessDate);
          setError('Stan zadania został odświeżony po zmianie z innego żądania.');
        } else invalidate('routines', businessDate());
      } else {
        setFailed(current => ({ ...current, [task.id]: { request, message: exception instanceof Error ? exception.message : 'Nie udało się zapisać zmiany.' } }));
      }
    } finally { setBusy(current => ({ ...current, [task.id]: false })); }
  }

  const tasks = list?.tasks ?? [];
  return <>
    <header className="page-header"><span className="app-mark" aria-hidden="true">↻</span><div><h1>Zadania rutynowe</h1><p>Twój dzień trwa od 03:00 do 03:00 czasu polskiego.</p></div></header>
    <CacheHeader fetchedAt={cache.fetchedAt} loading={cache.loading} error={cache.error} refresh={() => void cache.refresh()} />
    <section className="routine-list" aria-labelledby="routine-heading" aria-busy={cache.loading}>
      <div className="section-heading"><h2 id="routine-heading">Zaplanowane na dziś <span className="count">{tasks.length}</span></h2></div>
      {cache.loading && !cache.data && <p className="state">Ładowanie zadań rutynowych…</p>}
      {error && <p className="error">{error}</p>}
      {list && tasks.length === 0 && <div className="empty-state"><span aria-hidden="true">✓</span><h3>Bez zadań rutynowych</h3><p>Na ten dzień nic nie zostało zaplanowane.</p></div>}
      {tasks.length > 0 && <ul>{tasks.map(task => {
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
