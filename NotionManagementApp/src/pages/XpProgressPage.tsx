import { useEffect, useState } from 'react';
import { getXpProgress, type XpPeriod, type XpProgress } from '../api/xpProgress';

const labels: Record<XpPeriod, string> = { day: 'Dzień', week: 'Tydzień', month: 'Miesiąc', year: 'Rok' };

export function XpProgressPage() {
  const [period, setPeriod] = useState<XpPeriod>('day');
  const [progress, setProgress] = useState<XpProgress | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  async function load(nextPeriod = period, periodStart?: string) {
    setLoading(true); setError('');
    try { setProgress(await getXpProgress(nextPeriod, periodStart)); }
    catch (exception) { setError(exception instanceof Error ? exception.message : 'Unable to load XP progress.'); }
    finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, [period]);
  const displayPercent = progress && progress.targetXp > 0 ? (progress.earnedXp / progress.targetXp) * 100 : 0;
  const isOverTarget = displayPercent > 100;
  return <>
    <header className="page-header"><span className="app-mark" aria-hidden="true">★</span><div><h1>Postęp XP</h1><p>Twoje doświadczenie w wybranym okresie.</p></div></header>
    <section className="xp-progress-card" aria-busy={loading}>
      <div className="xp-periods" role="group" aria-label="Okres postępu XP">{(Object.keys(labels) as XpPeriod[]).map(item => <button className={period === item ? 'active' : undefined} key={item} type="button" onClick={() => setPeriod(item)} disabled={loading}>{labels[item]}</button>)}</div>
      <div className="section-heading"><h2>{progress ? periodLabel(progress) : labels[period]}</h2><button className="text-button" type="button" onClick={() => void load()} disabled={loading}>Odśwież</button></div>
      {loading && <p className="state">Ładowanie postępu XP…</p>}
      {error && <p className="error">{error}</p>}
      {progress && !loading && !error && (
        <>
          <p className={`xp-total${isOverTarget ? ' success' : ''}`}>{isOverTarget && <span aria-label="Cel przekroczony">🏆 </span>}{progress.earnedXp} / {progress.targetXp} XP</p>
          <div className="xp-progress-track" aria-label="Postęp XP">
            <div className={`xp-progress-fill${isOverTarget ? ' success' : ''}`} style={{ width: `${Math.min(displayPercent, 100)}%` }} />
          </div>
          <p className="hint">{Math.round(displayPercent)}% celu dla całego wybranego okresu</p>
          <button className="previous-period" type="button" disabled={!progress.previousPeriodStart} onClick={() => { if (progress.previousPeriodStart) void load(period, progress.previousPeriodStart); }}>← Wcześniejszy okres</button>
        </>
      )}
    </section>
  </>;
}
function periodLabel(progress: XpProgress) { return `${labels[progress.period]}: ${progress.periodStart} – ${progress.periodEndExclusive}`; }
