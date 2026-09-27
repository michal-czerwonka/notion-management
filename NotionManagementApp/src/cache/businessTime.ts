import type { XpPeriod } from '../api/xpProgress';

const timeZone = 'Europe/Warsaw';
const dateFormatter = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', hourCycle: 'h23' });
const stampFormatter = new Intl.DateTimeFormat('en-GB', { timeZone, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23' });

function parts(instant: Date) {
  const values = dateFormatter.formatToParts(instant);
  const number = (type: Intl.DateTimeFormatPartTypes) => Number(values.find(value => value.type === type)?.value);
  return { year: number('year'), month: number('month'), day: number('day'), hour: number('hour') };
}

function iso(year: number, month: number, day: number) {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

export function businessDate(instant = new Date()) {
  const local = parts(instant);
  const date = new Date(Date.UTC(local.year, local.month - 1, local.day - (local.hour < 3 ? 1 : 0)));
  return iso(date.getUTCFullYear(), date.getUTCMonth() + 1, date.getUTCDate());
}

export function periodStart(period: XpPeriod, date = businessDate()) {
  const current = new Date(`${date}T00:00:00Z`);
  if (period === 'week') current.setUTCDate(current.getUTCDate() - (current.getUTCDay() + 6) % 7);
  if (period === 'month') current.setUTCDate(1);
  if (period === 'year') { current.setUTCMonth(0); current.setUTCDate(1); }
  return iso(current.getUTCFullYear(), current.getUTCMonth() + 1, current.getUTCDate());
}

export function nextBoundary(instant = new Date()) {
  const local = parts(instant);
  const targetDate = new Date(Date.UTC(local.year, local.month - 1, local.day + (local.hour >= 3 ? 1 : 0)));
  const targetUtc = Date.UTC(targetDate.getUTCFullYear(), targetDate.getUTCMonth(), targetDate.getUTCDate(), 3);
  const atTarget = parts(new Date(targetUtc));
  const offset = Date.UTC(atTarget.year, atTarget.month - 1, atTarget.day, atTarget.hour) - targetUtc;
  return targetUtc - offset;
}

export function formatFetchedAt(timestamp?: number) {
  if (timestamp === undefined) return 'Dane z —';
  const values = stampFormatter.formatToParts(new Date(timestamp));
  const part = (type: Intl.DateTimeFormatPartTypes) => values.find(value => value.type === type)?.value ?? '';
  return `Dane z ${part('day')}.${part('month')}.${part('year')} ${part('hour')}:${part('minute')}:${part('second')}`;
}
