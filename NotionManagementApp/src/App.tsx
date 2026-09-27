import { useEffect, useState } from 'react';
import { Capacitor, registerPlugin, type PluginListenerHandle } from '@capacitor/core';
import { App as CapacitorApp } from '@capacitor/app';
import { businessDate, nextBoundary } from './cache/businessTime';
import { discardOldDay } from './cache/screenCache';
import { InboxPage } from './pages/InboxPage';
import { RoutineTasksPage } from './pages/RoutineTasksPage';
import { TodayTasksPage } from './pages/TodayTasksPage';
import { XpProgressPage } from './pages/XpProgressPage';

type AppView = 'inbox' | 'routines' | 'today' | 'progress';

interface LauncherRoutePlugin {
  addListener(
    eventName: 'shortcutOpen',
    listener: (payload: { view: string }) => void,
  ): Promise<PluginListenerHandle>;
}

const launcherRoute = registerPlugin<LauncherRoutePlugin>('LauncherRoute');

export function App() {
  const [view, setView] = useState<AppView>('inbox');
  const [day, setDay] = useState(businessDate);
  const [revisit, setRevisit] = useState(0);

  useLauncherRoute(setView);
  useEffect(() => {
    let timer: number;
    const check = () => { const current = businessDate(); discardOldDay(current); setDay(current); setRevisit(value => value + 1); schedule(); };
    const schedule = () => { window.clearTimeout(timer); timer = window.setTimeout(check, Math.max(0, nextBoundary() - Date.now()) + 100); };
    schedule();
    const listener = CapacitorApp.addListener('appStateChange', state => { if (state.isActive) check(); });
    return () => { window.clearTimeout(timer); void listener.then(handle => handle.remove()); };
  }, []);

  return (
    <main className="app-shell">
      <nav className="main-navigation" aria-label="Główna nawigacja">
        <button className={view === 'today' ? 'active' : undefined} type="button" onClick={() => setView('today')}>Dzisiaj</button>
        <button className={view === 'routines' ? 'active' : undefined} type="button" onClick={() => setView('routines')}>Rutyny</button>
        <button className={view === 'inbox' ? 'active' : undefined} type="button" onClick={() => setView('inbox')}>Inbox</button>
        <button className={view === 'progress' ? 'active' : undefined} type="button" onClick={() => setView('progress')}>Postęp XP</button>
      </nav>
      {view === 'inbox' ? <InboxPage day={day} revisit={revisit} /> : view === 'routines' ? <RoutineTasksPage day={day} revisit={revisit} /> : view === 'progress' ? <XpProgressPage day={day} revisit={revisit} /> : <TodayTasksPage day={day} revisit={revisit} />}
    </main>
  );
}

function useLauncherRoute(setView: (view: AppView) => void) {
  useEffect(() => {
    if (!Capacitor.isNativePlatform()) return;

    const listener = launcherRoute.addListener('shortcutOpen', payload => {
      setView(payload.view === 'routines' ? 'routines' : payload.view === 'today' ? 'today' : payload.view === 'progress' ? 'progress' : 'inbox');
    });

    return () => { void listener.then(handle => handle.remove()); };
  }, []);
}
