export type XpPeriod = 'day' | 'week' | 'month' | 'year';

export interface CompletedTask { id: string; subjectType: 'task' | 'routine'; subjectId: string; taskName: string; completedAt: string; businessDate: string; observedEffort: string | null; projects: string[] | null; }
export interface XpProgress { period: XpPeriod; periodStart: string; periodEndExclusive: string; earnedXp: number; targetXp: number; previousPeriodStart: string | null; completedTasks: CompletedTask[] | null; }

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
  if (item.period === 'year' ? item.completedTasks != null : !Array.isArray(item.completedTasks)) throw new Error('Invalid completed task history.');
  const completedTasks = item.period === 'year' ? null : (item.completedTasks as unknown[]).map(toCompletedTask);
  return { period: item.period, periodStart: item.periodStart, periodEndExclusive: item.periodEndExclusive, earnedXp: Number(item.earnedXp), targetXp: Number(item.targetXp), previousPeriodStart: item.previousPeriodStart, completedTasks };
}
function toCompletedTask(value: unknown): CompletedTask {
  if (!value || typeof value !== 'object') throw new Error('Invalid completed task history.');
  const item = value as Record<string, unknown>;
  if (typeof item.id !== 'string' || (item.subjectType !== 'task' && item.subjectType !== 'routine') || typeof item.subjectId !== 'string' || typeof item.taskName !== 'string' || typeof item.completedAt !== 'string' || typeof item.businessDate !== 'string' || (item.observedEffort !== null && typeof item.observedEffort !== 'string') || (item.projects !== null && (!Array.isArray(item.projects) || !item.projects.every(project => typeof project === 'string')))) throw new Error('Invalid completed task history.');
  return item as unknown as CompletedTask;
}
function isPeriod(value: unknown): value is XpPeriod { return value === 'day' || value === 'week' || value === 'month' || value === 'year'; }
