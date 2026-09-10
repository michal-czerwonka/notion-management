import { useState } from 'react';
import { InboxPage } from './pages/InboxPage';
import { RoutineTasksPage } from './pages/RoutineTasksPage';

export function App() {
  const [view, setView] = useState<'inbox' | 'routines'>('inbox');

  return (
    <main className="app-shell">
      <nav className="main-navigation" aria-label="Główna nawigacja">
        <button className={view === 'inbox' ? 'active' : undefined} type="button" onClick={() => setView('inbox')}>Inbox</button>
        <button className={view === 'routines' ? 'active' : undefined} type="button" onClick={() => setView('routines')}>Rutyny</button>
      </nav>
      {view === 'inbox' ? <InboxPage /> : <RoutineTasksPage />}
    </main>
  );
}
