import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { archiveTodayTask, getTodayTasks, updateTodayTaskStatus, type TodayTask, type TodayTaskStatus } from '../api/todayTasks';

type MenuPlacement = 'above' | 'below';

export function TodayTasksPage() {
  const [tasks, setTasks] = useState<TodayTask[]>([]);
  const [statuses, setStatuses] = useState<TodayTaskStatus[]>([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState<string | null>(null);
  const [menu, setMenu] = useState<string | null>(null);
  const [menuPlacement, setMenuPlacement] = useState<MenuPlacement>('below');
  const [menuMaxHeight, setMenuMaxHeight] = useState<number | null>(null);
  const [archiveId, setArchiveId] = useState<string | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  async function load() {
    setError('');
    try {
      const result = await getTodayTasks();
      setTasks(result.tasks);
      setStatuses(result.statuses);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Unable to load tasks.');
    }
  }

  useEffect(() => { void load(); }, []);

  useLayoutEffect(() => {
    if (!menu) return;

    const visualViewport = window.visualViewport;

    const positionMenu = () => {
      const element = menuRef.current;
      const item = element?.parentElement;
      if (!element || !item) return;

      const viewportTop = visualViewport?.offsetTop ?? 0;
      const viewportBottom = viewportTop + (visualViewport?.height ?? window.innerHeight);
      const itemRect = item.getBoundingClientRect();
      const fullMenuHeight = element.scrollHeight + element.offsetHeight - element.clientHeight;
      const viewportMargin = 16;
      const itemOverlap = 8;
      const spaceBelow = Math.max(0, viewportBottom - itemRect.bottom + itemOverlap - viewportMargin);
      const spaceAbove = Math.max(0, itemRect.top + itemOverlap - viewportTop - viewportMargin);

      const placement: MenuPlacement = fullMenuHeight <= spaceBelow
        ? 'below'
        : fullMenuHeight <= spaceAbove || spaceAbove >= spaceBelow
          ? 'above'
          : 'below';
      const availableHeight = placement === 'below' ? spaceBelow : spaceAbove;

      setMenuPlacement(current => current === placement ? current : placement);
      setMenuMaxHeight(current => {
        const next = fullMenuHeight <= availableHeight ? null : Math.floor(availableHeight);
        return current === next ? current : next;
      });
    };

    positionMenu();
    window.addEventListener('resize', positionMenu);
    window.addEventListener('scroll', positionMenu);
    visualViewport?.addEventListener('resize', positionMenu);
    visualViewport?.addEventListener('scroll', positionMenu);

    return () => {
      window.removeEventListener('resize', positionMenu);
      window.removeEventListener('scroll', positionMenu);
      visualViewport?.removeEventListener('resize', positionMenu);
      visualViewport?.removeEventListener('scroll', positionMenu);
    };
  }, [menu, statuses.length]);

  function toggleMenu(taskId: string) {
    if (menu === taskId) {
      setMenu(null);
      return;
    }

    setMenuPlacement('below');
    setMenuMaxHeight(null);
    setMenu(taskId);
  }

  async function changeStatus(task: TodayTask, status: string) {
    setBusy(task.id);
    setMenu(null);
    try {
      await updateTodayTaskStatus(task.id, status);
      setTasks(items => items.map(item => item.id === task.id ? { ...item, status } : item));
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Unable to update task.');
    } finally {
      setBusy(null);
    }
  }

  async function archive() {
    if (!archiveId) return;
    setBusy(archiveId);
    try {
      await archiveTodayTask(archiveId);
      setTasks(items => items.filter(item => item.id !== archiveId));
      setArchiveId(null);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : 'Unable to archive task.');
    } finally {
      setBusy(null);
    }
  }

  return (
    <>
      <header className="page-header">
        <span className="app-mark" aria-hidden="true">✓</span>
        <div><h1>Today's tasks</h1><p>A quick view of what is planned for today.</p></div>
      </header>
      <section className="today-list">
        <div className="section-heading">
          <h2>Tasks <span className="count">{tasks.length}</span></h2>
          <button className="text-button" onClick={() => void load()}>Refresh</button>
        </div>
        {error && <p className="error">{error}</p>}
        {tasks.length === 0 && !error ? <div className="empty-state"><span>✓</span><h3>Nothing planned</h3><p>Your Notion today view is empty.</p></div> : (
          <ul>{tasks.map(task => (
            <li className="today-item" key={task.id}>
              <div className="today-details"><strong>{task.name}</strong><small>{task.projects.length ? task.projects.join(', ') : 'No project'} · {task.effort?.trim() || 'Brak effortu'}</small></div>
              <div className="today-actions">
                <button className={`status-button notion-${colorOf(task.status, statuses)}`} disabled={busy === task.id} aria-haspopup="menu" aria-expanded={menu === task.id} onClick={() => toggleMenu(task.id)}>
                  {busy === task.id ? 'Saving…' : task.status}
                </button>
                <button className="archive-icon" aria-label={`Archive ${task.name}`} title="Archive" disabled={busy === task.id} onClick={() => setArchiveId(task.id)}>×</button>
              </div>
              {menu === task.id && <div ref={menuRef} className={`status-options status-options--${menuPlacement}`} style={{ maxHeight: menuMaxHeight === null ? undefined : `${menuMaxHeight}px` }} role="menu">
                {statuses.map(status => <button className={`notion-${status.color}`} key={status.name} role="menuitem" onClick={() => void changeStatus(task, status.name)}>{status.name}</button>)}
              </div>}
            </li>
          ))}</ul>
        )}
      </section>
      {archiveId && <div className="confirm-backdrop" role="presentation"><section className="confirm-dialog" role="dialog" aria-modal="true" aria-label="Confirm archive"><h2>Archive task?</h2><p>The task will be moved to the Notion trash.</p><div><button className="delete-button" onClick={() => void archive()}>Archive</button><button className="text-button" onClick={() => setArchiveId(null)}>Cancel</button></div></section></div>}
    </>
  );
}

function colorOf(statusName: string, statuses: TodayTaskStatus[]) {
  return statuses.find(status => status.name === statusName)?.color ?? 'default';
}
