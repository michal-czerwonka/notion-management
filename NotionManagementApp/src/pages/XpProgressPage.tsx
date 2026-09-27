import { useEffect, useState } from 'react';
import { getXpProgress, type CompletedTask, type XpPeriod, type XpProgress } from '../api/xpProgress';
import { CacheHeader } from '../cache/CacheHeader';
import { periodStart } from '../cache/businessTime';
import { getXpSelection, saveXpSelection, useCachedResource, xpKey, type XpSelection } from '../cache/screenCache';

const labels: Record<XpPeriod, string> = { day: 'Dzień', week: 'Tydzień', month: 'Miesiąc', year: 'Rok' };

export function XpProgressPage({ day, revisit }: { day: string; revisit: number }) {
  const [savedSelection, setSavedSelection] = useState<XpSelection | null>(null);
  useEffect(() => {
    let active = true;
    void getXpSelection().then(saved => {
      if (!active) return;
      const next = saved?.businessDate === day ? saved : { period: 'day' as const, start: day, businessDate: day };
      setSavedSelection(next);
      saveXpSelection(next);
    });
    return () => { active = false; };
  }, [day]);
  if (!savedSelection) return <>
    <header className="page-header"><span className="app-mark" aria-hidden="true">★</span><div><h1>Postęp XP</h1><p>Twoje doświadczenie w wybranym okresie.</p></div></header>
    <CacheHeader loading refresh={() => {}} disabled />
    <p className="state">Ładowanie postępu XP…</p>
  </>;
  const selection = savedSelection.businessDate === day ? savedSelection : { period: 'day' as const, start: day, businessDate: day };
  return <XpContent day={day} revisit={revisit} selection={selection} onSelect={next => { setSavedSelection(next); saveXpSelection(next); }} />;
}

function XpContent({ day, revisit, selection, onSelect }: { day: string; revisit: number; selection: XpSelection; onSelect: (selection: XpSelection) => void }) {
  const { period, start } = selection;
  const cache = useCachedResource<XpProgress>(xpKey(period, start), day, () => getXpProgress(period, start), revisit);
  const progress = cache.data ?? null;
  const displayPercent = progress && progress.targetXp > 0 ? (progress.earnedXp / progress.targetXp) * 100 : 0;
  const isOverTarget = displayPercent > 100;
  return <>
    <header className="page-header"><span className="app-mark" aria-hidden="true">★</span><div><h1>Postęp XP</h1><p>Twoje doświadczenie w wybranym okresie.</p></div></header>
    <CacheHeader fetchedAt={cache.fetchedAt} loading={cache.loading} error={cache.error} refresh={() => void cache.refresh()} />
    <section className="xp-progress-card" aria-busy={cache.loading}>
      <div className="xp-periods" role="group" aria-label="Okres postępu XP">{(Object.keys(labels) as XpPeriod[]).map(item => <button className={period === item ? 'active' : undefined} key={item} type="button" onClick={() => onSelect({ period: item, start: periodStart(item, day), businessDate: day })}>{labels[item]}</button>)}<button type="button" onClick={() => onSelect({ period: 'day', start: day, businessDate: day })}>Today</button></div>
      <div className="section-heading"><h2>{progress ? periodLabel(progress) : labels[period]}</h2></div>
      {cache.loading && !progress && <p className="state">Ładowanie postępu XP…</p>}
      {progress && (
        <>
          <p className={`xp-total${isOverTarget ? ' success' : ''}`}>{isOverTarget && <span aria-label="Cel przekroczony">🏆 </span>}{progress.earnedXp} / {progress.targetXp} XP</p>
          <div className="xp-progress-track" aria-label="Postęp XP">
            <div className={`xp-progress-fill${isOverTarget ? ' success' : ''}`} style={{ width: `${Math.min(displayPercent, 100)}%` }} />
          </div>
          <p className="hint">{Math.round(displayPercent)}% celu dla całego wybranego okresu</p>
          <button className="previous-period" type="button" disabled={!progress.previousPeriodStart} onClick={() => { if (progress.previousPeriodStart) onSelect({ period, start: progress.previousPeriodStart, businessDate: day }); }}>← Wcześniejszy okres</button>
        </>
      )}
    </section>
    {progress && progress.completedTasks && <section className="xp-history" aria-label="Ukończone zadania">
      <h2>Ukończone zadania</h2>
      <CompletionHistory period={progress.period} tasks={progress.completedTasks} />
    </section>}
  </>;
}
function periodLabel(progress: XpProgress) { return `${labels[progress.period]}: ${progress.periodStart} – ${progress.periodEndExclusive}`; }

function CompletionHistory({ period, tasks }: { period: XpPeriod; tasks: CompletedTask[] }) {
  if (period === 'day') return <>
    <CompletionGroup title="Rutyny" tasks={tasks.filter(task => task.subjectType === 'routine')} showEmpty />
    <CompletionGroup title="Zadania" tasks={tasks.filter(task => task.subjectType === 'task')} showEmpty />
  </>;

  const groups = new Map<string, CompletedTask[]>();
  for (const task of tasks) groups.set(task.businessDate, [...(groups.get(task.businessDate) ?? []), task]);
  if (groups.size === 0) return <p className="hint">Brak ukończonych zadań w tym okresie.</p>;

  return [...groups].sort(([a], [b]) => b.localeCompare(a)).map(([date, entries]) =>
    <section className="xp-history-day" key={date}>
      <h3>{date}</h3>
      <CompletionGroup title="Rutyny" tasks={entries.filter(task => task.subjectType === 'routine')} nested />
      <CompletionGroup title="Zadania" tasks={entries.filter(task => task.subjectType === 'task')} nested />
    </section>
  );
}

function CompletionGroup({ title, tasks, nested = false, showEmpty = false }: { title: string; tasks: CompletedTask[]; nested?: boolean; showEmpty?: boolean }) {
  if (tasks.length === 0 && !showEmpty) return null;
  const heading = nested ? <h4>{title}</h4> : <h3>{title}</h3>;
  return <div className="xp-history-group">
    {heading}
    {tasks.length === 0 ? <p className="hint">Brak ukończonych zadań.</p> :
      <ul>{[...tasks].sort((a, b) => b.completedAt.localeCompare(a.completedAt) || b.id.localeCompare(a.id)).map(task =>
        <li key={task.id}>
          <strong>{task.taskName}</strong>
          <time dateTime={task.completedAt}>{new Intl.DateTimeFormat('pl-PL', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Europe/Warsaw' }).format(new Date(task.completedAt))}</time>
          {task.projects?.map((project, index) => <span key={`${index}:${project}`} className="xp-history-meta">{project}</span>)}
          {task.observedEffort && <span className="xp-history-meta">{task.observedEffort}</span>}
        </li>)}</ul>}
  </div>;
}
