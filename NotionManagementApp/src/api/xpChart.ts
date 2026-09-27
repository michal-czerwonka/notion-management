export type XpChartRange = 14 | 30 | 90;
export interface XpChartDay { date: string; earnedXp: number; }
export interface XpChart { startDate: string; endDate: string; chartMaxXp: number; days: XpChartDay[]; }

export async function getXpChart(range: XpChartRange, businessDay: string): Promise<XpChart> {
  const endpoint = import.meta.env.VITE_XP_CHART_API_URL?.trim();
  if (!endpoint) throw new Error('Brak adresu API diagramu XP.');
  const url = new URL(endpoint);
  url.searchParams.set('days', String(range));
  const response = await fetch(url, { cache: 'no-store' });
  if (!response.ok) throw new Error('Nie udało się pobrać diagramu XP. Odśwież i spróbuj ponownie.');
  const result = await response.json() as unknown;
  if (!validXpChart(result, range) || result.endDate !== businessDay) throw new Error('Nieprawidłowa odpowiedź diagramu XP.');
  return result;
}

export function validXpChart(value: unknown, range: XpChartRange): value is XpChart {
  if (!value || typeof value !== 'object') return false;
  const item = value as Record<string, unknown>;
  if (!isIsoDate(item.startDate) || !isIsoDate(item.endDate) || !Number.isInteger(item.chartMaxXp) || Number(item.chartMaxXp) <= 0 || !Array.isArray(item.days) || item.days.length !== range) return false;
  const start = Date.parse(`${item.startDate}T00:00:00Z`);
  if (Date.parse(`${item.endDate}T00:00:00Z`) !== start + (range - 1) * 86_400_000) return false;
  return item.days.every((day, index) => day && typeof day === 'object' &&
    isIsoDate(day.date) && Date.parse(`${day.date}T00:00:00Z`) === start + index * 86_400_000 &&
    Number.isSafeInteger(day.earnedXp) && day.earnedXp >= 0);
}

function isIsoDate(value: unknown): value is string {
  if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = new Date(`${value}T00:00:00Z`);
  return Number.isFinite(date.getTime()) && date.toISOString().slice(0, 10) === value;
}
