import { useEffect, useRef, useState, type FormEvent } from 'react';
import { createInboxItem, getInbox, type InboxItem } from '../api/inbox';

export function InboxPage() {
  const [items, setItems] = useState<InboxItem[]>([]);
  const [name, setName] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [saveError, setSaveError] = useState('');
  const [notice, setNotice] = useState('');
  const input = useRef<HTMLInputElement>(null);
  const submitting = useRef(false);

  async function refresh(signal?: AbortSignal) {
    setLoading(true);
    setLoadError('');
    try {
      const result = await getInbox(signal);
      if (!signal?.aborted) setItems(result);
    } catch (error) {
      if (!signal?.aborted) setLoadError(error instanceof Error ? error.message : 'Nie udało się pobrać Inbox.');
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }

  useEffect(() => {
    const controller = new AbortController();
    void refresh(controller.signal);
    return () => controller.abort();
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = name.trim();
    if (!value || submitting.current || loading) return;
    submitting.current = true;
    setSaving(true);
    setSaveError('');
    setNotice('');
    try {
      const item = await createInboxItem(value);
      setName('');
      setItems(current => [item, ...current.filter(existing => existing.id !== item.id)]);
      setNotice('Dodano do Inbox.');
      // Keep the confirmed item visible even when the subsequent refresh fails.
      await refresh();
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : 'Nie udało się dodać wpisu.');
    } finally {
      submitting.current = false;
      setSaving(false);
      input.current?.focus();
    }
  }

  return (
    <>
      <header className="page-header">
        <span className="app-mark" aria-hidden="true">↓</span>
        <div><h1>Inbox</h1><p>Zapisz myśl. Wrócisz do niej później.</p></div>
      </header>

      <section className="capture-card" aria-label="Nowy wpis">
        <form onSubmit={event => void submit(event)}>
          <label htmlFor="thought">Co masz na myśli?</label>
          <div className="input-row">
            <input ref={input} id="thought" value={name} maxLength={2000}
              placeholder="Zapisz szybką myśl…" autoComplete="off" enterKeyHint="send"
              readOnly={saving} onChange={event => setName(event.target.value)}
              onKeyDown={event => { if (event.key === 'Enter' && event.nativeEvent.isComposing) event.preventDefault(); }} />
            <button className="primary" type="submit" disabled={!name.trim() || saving || loading}>
              {saving ? 'Zapisywanie…' : 'Dodaj'}
            </button>
          </div>
          <p className="hint">Enter lub Dodaj — i gotowe.</p>
        </form>
        {saveError && <p className="error" role="alert">{saveError}</p>}
        <p className="notice" role="status">{notice}</p>
      </section>

      <section className="inbox-list" aria-labelledby="list-heading" aria-busy={loading}>
        <div className="section-heading">
          <h2 id="list-heading">Twoje wpisy <span className="count">{items.length}</span></h2>
          <button type="button" className="text-button" disabled={loading || saving} onClick={() => void refresh()}>Odśwież</button>
        </div>
        {loading && <p className="state" role="status">Pobieranie wpisów…</p>}
        {loadError && <p className="error" role="alert">{loadError} Użyj przycisku Odśwież.</p>}
        {!loading && !loadError && items.length === 0 && (
          <div className="empty-state"><span aria-hidden="true">✓</span><h3>Miejsce na nowe myśli</h3><p>Inbox jest pusty. Dodaj swój pierwszy wpis powyżej.</p></div>
        )}
        {items.length > 0 && <ul>{items.map(item => <li key={item.id}><span className="item-dot" aria-hidden="true" /><span>{item.name || '(bez nazwy)'}</span></li>)}</ul>}
      </section>
    </>
  );
}
