import { useEffect, useState } from 'react';
import { Capacitor, registerPlugin, type PluginListenerHandle } from '@capacitor/core';
import { InboxPage } from './pages/InboxPage';
import { RoutineTasksPage } from './pages/RoutineTasksPage';
import { TodayTasksPage } from './pages/TodayTasksPage';

type AppView = 'inbox' | 'routines' | 'today';

interface LauncherRoutePlugin {
  addListener(
    eventName: 'shortcutOpen',
    listener: (payload: { view: string }) => void,
  ): Promise<PluginListenerHandle>;
}

const launcherRoute = registerPlugin<LauncherRoutePlugin>('LauncherRoute');

export function App() {
  const [view, setView] = useState<AppView>('inbox');

  useLauncherRoute(setView);

  return (
    <main className="app-shell">
      <nav className="main-navigation" aria-label="Główna nawigacja">
        <button className={view === 'inbox' ? 'active' : undefined} type="button" onClick={() => setView('inbox')}>Inbox</button>
        <button className={view === 'routines' ? 'active' : undefined} type="button" onClick={() => setView('routines')}>Rutyny</button>
        <button className={view === 'today' ? 'active' : undefined} type="button" onClick={() => setView('today')}>Dzisiaj</button>
      </nav>
      {view === 'inbox' ? <InboxPage /> : view === 'routines' ? <RoutineTasksPage /> : <TodayTasksPage />}
    </main>
  );
}

function useLauncherRoute(setView: (view: AppView) => void) {
  useEffect(() => {
    if (!Capacitor.isNativePlatform()) return;

    const listener = launcherRoute.addListener('shortcutOpen', payload => {
      setView(payload.view === 'routines' ? 'routines' : payload.view === 'today' ? 'today' : 'inbox');
    });

    return () => { void listener.then(handle => handle.remove()); };
  }, []);
}
