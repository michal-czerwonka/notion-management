import { useState } from 'react';
import { getXpChart, type XpChartRange } from '../api/xpChart';
import { CacheHeader } from '../cache/CacheHeader';
import { useCachedResource, xpChartKey } from '../cache/screenCache';

const ranges: XpChartRange[] = [14, 30, 90];
const plotHeight = 240;

export function XpChartPage({ day, revisit }: { day: string; revisit: number }) {
  const [range, setRange] = useState<XpChartRange>(14);
  const cache = useCachedResource(xpChartKey(range), day, () => getXpChart(range, day), revisit);
  const chart = cache.data;
  const ticks = chart ? [0, 1, 2, 3, 4].map(step => chart.chartMaxXp * step / 4) : [];

  return <>
    <header className="page-header"><span className="app-mark" aria-hidden="true">★</span><div><h1>Diagram XP</h1><p>XP zdobyte w poszczególnych dniach.</p></div></header>
    <CacheHeader fetchedAt={cache.fetchedAt} loading={cache.loading} error={cache.error} refresh={() => void cache.refresh()} />
    <section className="xp-chart-card" aria-busy={cache.loading}>
      <div className="xp-periods" role="group" aria-label="Zakres diagramu XP">
        {ranges.map(value => <button type="button" key={value} className={range === value ? 'active' : undefined} aria-pressed={range === value} onClick={() => setRange(value)}>{value} dni</button>)}
      </div>
      {cache.loading && !chart && <p className="state">Ładowanie diagramu XP…</p>}
      {chart && <>
        <div className="section-heading"><h2>{chart.startDate} – {chart.endDate}</h2></div>
        <div className="xp-chart-layout">
          <div className="xp-chart-axis" style={{ height: plotHeight }} aria-hidden="true">
            {ticks.map((tick, index) => <span key={index} style={{ bottom: `${index * 25}%` }}>{new Intl.NumberFormat('pl-PL', { maximumFractionDigits: 2 }).format(tick)}</span>)}
          </div>
          <div className="xp-chart-scroll" tabIndex={0} aria-label="Przewijany diagram dzienny XP">
            <div className="xp-chart-content" style={{ width: chart.days.length * 18 }}>
              <div className="xp-chart-plot" style={{ height: plotHeight }} aria-hidden="true">
                {chart.days.map(entry => <div className="xp-chart-slot" key={entry.date}>
                  <div className="xp-chart-bar" style={{ height: `${Math.min(entry.earnedXp, chart.chartMaxXp) / chart.chartMaxXp * 100}%` }} />
                </div>)}
              </div>
              <div className="xp-chart-dates" aria-hidden="true">
                {chart.days.map((entry, index) => <div className="xp-chart-date" key={entry.date}>{index % 7 === 0 && <span>{entry.date.slice(5).replace('-', '.')}</span>}</div>)}
              </div>
            </div>
          </div>
        </div>
        <div className="sr-only">
          <table>
            <caption>Dzienne XP od {chart.startDate} do {chart.endDate}</caption>
            <thead><tr><th scope="col">Data</th><th scope="col">XP</th></tr></thead>
            <tbody>{chart.days.map(entry => <tr key={entry.date}><th scope="row">{entry.date}</th><td>{entry.earnedXp}</td></tr>)}</tbody>
          </table>
        </div>
        <p className="hint">Każdy słupek oznacza jeden dzień. Przesuń wykres w poziomie, aby zobaczyć pozostałe dni.</p>
      </>}
    </section>
  </>;
}
