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
  if (!result || typeof result !== 'object' || !('tasks' in result) || !('statuses' in result) || !Array.isArray(result.tasks) || !Array.isArray(result.statuses)) throw new Error('Invalid today tasks response.');
  return result as { tasks: TodayTask[]; statuses: TodayTaskStatus[] };
}
export async function updateTodayTaskStatus(id: string, status: string) { await request('PATCH', id, status); }
export async function archiveTodayTask(id: string) { await request('DELETE', id); }
