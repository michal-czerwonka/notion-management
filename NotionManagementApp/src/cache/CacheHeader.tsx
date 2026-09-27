import { formatFetchedAt } from './businessTime';

export function CacheHeader({ fetchedAt, loading, error, refresh, disabled = false }: { fetchedAt?: number; loading: boolean; error?: string; refresh: () => void; disabled?: boolean }) {
  const status = loading ? 'Pobieranie danych' : error ? `Błąd pobierania: ${error}` : 'Dane aktualne';
  return <div className="cache-header">
    <span className="cache-timestamp">{formatFetchedAt(fetchedAt)}</span>
    <button type="button" className="cache-refresh" aria-label="Odśwież dane" title="Odśwież dane" onClick={refresh} disabled={disabled}>↻</button>
    <span className={`cache-indicator ${loading ? 'loading' : error ? 'failed' : 'ready'}`} role="status" aria-label={status} title={status} />
    {error && !loading && <span className="cache-error" role="alert">{error}</span>}
  </div>;
}
