export interface TodayTask { id: string; name: string; status: string; projects: string[]; }
export interface TodayTaskStatus { name: string; color: string; }

const endpoint = () => {
  const value = import.meta.env.VITE_TODAY_TASKS_API_URL?.trim();
  if (!value) throw new Error('Missing today tasks API address.');
  return value;
};

async function request(method: 'GET' | 'PATCH' | 'DELETE', id?: string, status?: string) {
  const response = await fetch(id ? `${endpoint()}/${encodeURIComponent(id)}` : endpoint(), {
    method, headers: method === 'PATCH' ? { 'Content-Type': 'application/json' } : undefined,
    body: method === 'PATCH' ? JSON.stringify({ status }) : undefined, cache: 'no-store',
  });
  if (!response.ok) throw new Error('Unable to update today\'s tasks. Please refresh and try again.');
  return method === 'DELETE' ? undefined : response.json() as Promise<unknown>;
}

export async function getTodayTasks() {
  const result = await request('GET');
  if (!result || typeof result !== 'object') throw new Error('Invalid today tasks response.');

  const body = result as Record<string, unknown>;
  const tasks = body.tasks;
  const statuses = body.statuses;
  if (!Array.isArray(tasks) || !Array.isArray(statuses)) throw new Error('Invalid today tasks response.');

  return {
    tasks: tasks.map(toTask),
    statuses: statuses.map(toStatus),
  };
}

function toTask(value: unknown): TodayTask {
  if (!value || typeof value !== 'object') throw new Error('Invalid today tasks response.');
  const task = value as Record<string, unknown>;
  const id = task.id;
  const name = task.name;
  const status = task.status;
  const projects = task.projects;
  if (typeof id !== 'string' || typeof name !== 'string' || typeof status !== 'string' ||
    !Array.isArray(projects) || !projects.every(project => typeof project === 'string')) {
    throw new Error('Invalid today tasks response.');
  }
  return { id, name, status, projects };
}

function toStatus(value: unknown): TodayTaskStatus {
  if (!value || typeof value !== 'object') throw new Error('Invalid today tasks response.');
  const status = value as Record<string, unknown>;
  const name = status.name;
  const color = status.color;
  if (typeof name !== 'string' || typeof color !== 'string') throw new Error('Invalid today tasks response.');
  return { name, color };
}
export async function updateTodayTaskStatus(id: string, status: string) { await request('PATCH', id, status); }
export async function archiveTodayTask(id: string) { await request('DELETE', id); }
