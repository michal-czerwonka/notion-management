export type XpPeriod = 'day' | 'week' | 'month' | 'year';

export interface XpProgress { period: XpPeriod; periodStart: string; periodEndExclusive: string; earnedXp: number; targetXp: number; previousPeriodStart: string | null; }

const endpoint = () => {
  const value = import.meta.env.VITE_XP_PROGRESS_API_URL?.trim();
  if (!value) throw new Error('Missing XP progress API address.');
  return value;
};

export async function getXpProgress(period: XpPeriod, periodStart?: string) {
  const url = new URL(endpoint());
  url.searchParams.set('period', period);
  if (periodStart) url.searchParams.set('periodStart', periodStart);
  const response = await fetch(url, { cache: 'no-store' });
  if (!response.ok) throw new Error('Unable to load XP progress. Please refresh and try again.');
  return toProgress(await response.json() as unknown);
}

function toProgress(value: unknown): XpProgress {
  if (!value || typeof value !== 'object') throw new Error('Invalid XP progress response.');
  const item = value as Record<string, unknown>;
  if (!isPeriod(item.period) || typeof item.periodStart !== 'string' || typeof item.periodEndExclusive !== 'string' || !Number.isInteger(item.earnedXp) || !Number.isInteger(item.targetXp) || (item.previousPeriodStart !== null && typeof item.previousPeriodStart !== 'string')) throw new Error('Invalid XP progress response.');
  return { period: item.period, periodStart: item.periodStart, periodEndExclusive: item.periodEndExclusive, earnedXp: Number(item.earnedXp), targetXp: Number(item.targetXp), previousPeriodStart: item.previousPeriodStart };
}
function isPeriod(value: unknown): value is XpPeriod { return value === 'day' || value === 'week' || value === 'month' || value === 'year'; }
