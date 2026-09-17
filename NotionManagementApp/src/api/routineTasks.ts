export type RoutineState = 'pending' | 'completed' | 'skipped';
export interface RoutineTask { id: string; name: string; effort: string; xp: number; state: RoutineState; version: number; }
export interface RoutineTaskList { businessDate: string; tasks: RoutineTask[]; }
export interface RoutineMutationRequest { operationId: string; expectedBusinessDate: string; expectedVersion: number; state: RoutineState; }
export interface RoutineMutationResult { occurrence: RoutineTask; replayed: boolean; }
export interface RoutineConflict { businessDate: string; occurrence: RoutineTask | null; error: string; }

export class RoutineApiError extends Error {
  constructor(message: string, public readonly status: number, public readonly conflict?: RoutineConflict) { super(message); }
}

const endpoint = () => {
  const value = import.meta.env.VITE_ROUTINE_TASKS_API_URL?.trim();
  if (!value) throw new Error('Brakuje adresu API zadań rutynowych.');
  return value.replace(/\/$/, '');
};

export async function getRoutineTasks(): Promise<RoutineTaskList> {
  const response = await fetch(endpoint(), { cache: 'no-store' });
  if (!response.ok) throw new RoutineApiError('Nie udało się pobrać zadań rutynowych.', response.status);
  return toList(await response.json() as unknown);
}

export async function updateRoutineTask(routineId: string, request: RoutineMutationRequest): Promise<RoutineMutationResult> {
  const response = await fetch(`${endpoint()}/${encodeURIComponent(routineId)}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, cache: 'no-store', body: JSON.stringify(request) });
  const body = await response.json().catch(() => null) as unknown;
  if (response.status === 409) throw new RoutineApiError('Stan rutyny zmienił się na serwerze.', response.status, toConflict(body));
  if (!response.ok) throw new RoutineApiError(response.status === 503 ? 'Nie udało się zapisać zmiany. Spróbuj ponownie.' : 'Zmiana zadania rutynowego została odrzucona.', response.status);
  if (!body || typeof body !== 'object') throw new RoutineApiError('Serwer zwrócił nieprawidłowe potwierdzenie.', response.status);
  const value = body as Record<string, unknown>;
  if (typeof value.replayed !== 'boolean') throw new RoutineApiError('Serwer zwrócił nieprawidłowe potwierdzenie.', response.status);
  return { occurrence: toTask(value.occurrence), replayed: value.replayed };
}

function toList(body: unknown): RoutineTaskList {
  if (!body || typeof body !== 'object') throw new Error('Nieprawidłowa odpowiedź API zadań rutynowych.');
  const value = body as Record<string, unknown>;
  if (typeof value.businessDate !== 'string' || !Array.isArray(value.tasks)) throw new Error('Nieprawidłowa odpowiedź API zadań rutynowych.');
  return { businessDate: value.businessDate, tasks: value.tasks.map(toTask) };
}

function toTask(body: unknown): RoutineTask {
  if (!body || typeof body !== 'object') throw new Error('Nieprawidłowy element zadania rutynowego.');
  const value = body as Record<string, unknown>;
  if (typeof value.id !== 'string' || typeof value.name !== 'string' || typeof value.effort !== 'string' || !Number.isInteger(value.xp) || !isState(value.state) || !Number.isInteger(value.version)) throw new Error('Nieprawidłowy element zadania rutynowego.');
  return { id: value.id, name: value.name, effort: value.effort, xp: Number(value.xp), state: value.state, version: Number(value.version) };
}

function toConflict(body: unknown): RoutineConflict {
  if (!body || typeof body !== 'object') throw new Error('Nieprawidłowa odpowiedź konfliktu.');
  const value = body as Record<string, unknown>;
  if (typeof value.businessDate !== 'string' || typeof value.error !== 'string') throw new Error('Nieprawidłowa odpowiedź konfliktu.');
  return { businessDate: value.businessDate, occurrence: value.occurrence === null ? null : toTask(value.occurrence), error: value.error };
}

function isState(value: unknown): value is RoutineState { return value === 'pending' || value === 'completed' || value === 'skipped'; }
