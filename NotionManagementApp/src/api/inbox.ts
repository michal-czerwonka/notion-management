export interface InboxItem {
  id: string;
  name: string;
}

async function request(method: 'GET' | 'POST' | 'PATCH', name?: string, id?: string, signal?: AbortSignal, action?: 'move-to-tasks'): Promise<unknown> {
  const endpoint = import.meta.env.VITE_INBOX_API_URL?.trim();
  if (!endpoint) throw new Error('Brak adresu API. Ustaw VITE_INBOX_API_URL i przebuduj aplikację.');
  const itemUrl = id ? `${endpoint}/${encodeURIComponent(id)}` : endpoint;
  const url = action ? `${itemUrl}/${action}` : itemUrl;

  // TODO: attach a user access token here once backend authentication is implemented.
  // The URL is public configuration, not a substitute for authentication.
  let response: Response;
  try {
    response = await fetch(url, {
      method,
      headers: method === 'GET' ? undefined : { 'Content-Type': 'application/json' },
      body: method === 'GET' || action ? undefined : JSON.stringify({ name }),
      signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(60_000)]) : AbortSignal.timeout(60_000),
      credentials: 'omit',
      cache: 'no-store',
    });
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new Error(action
      ? 'Nie udało się przenieść wpisu. Odśwież Inbox przed ponowną próbą.'
      : method === 'GET'
      ? 'Nie udało się połączyć z API. Sprawdź połączenie i spróbuj ponownie.'
      : method === 'POST'
      ? 'Nie udało się potwierdzić zapisu. Odśwież listę przed ponownym dodaniem, aby uniknąć duplikatu.'
      : 'Nie udało się zapisać zmian. Spróbuj ponownie.');
  }

  if (!response.ok) {
    if (action) {
      let errorMessage: string | undefined;
      try {
        const body: unknown = await response.json();
        if (typeof body === 'object' && body !== null && 'error' in body && typeof body.error === 'string') {
          errorMessage = body.error;
        }
      } catch { }
      if (errorMessage) throw new Error(errorMessage);
      throw new Error(response.status === 404
        ? 'Ten wpis nie jest już dostępny. Odśwież listę.'
        : 'Nie udało się przenieść wpisu. Odśwież Inbox przed ponowną próbą.');
    }
    if (response.status === 400) throw new Error('Wpis musi mieć od 1 do 2000 znaków.');
    if (response.status === 404) throw new Error('Ten wpis nie jest już dostępny. Odśwież listę.');
    throw new Error(method === 'GET'
      ? 'Nie udało się pobrać Inbox. Spróbuj ponownie później.'
      : method === 'POST'
      ? 'API nie potwierdziło zapisu. Odśwież listę przed ponownym dodaniem.'
      : 'API nie potwierdziło zapisu zmian. Spróbuj ponownie.');
  }
  try {
    return await response.json();
  } catch {
    throw new Error(method === 'GET'
      ? 'API zwróciło nieprawidłową odpowiedź. Spróbuj ponownie później.'
      : method === 'POST'
      ? 'Nieprawidłowe potwierdzenie zapisu. Odśwież listę przed ponownym dodaniem.'
      : 'Nieprawidłowe potwierdzenie zapisu zmian. Spróbuj ponownie.');
  }
}

function isInboxItem(value: unknown): value is InboxItem {
  return typeof value === 'object' && value !== null &&
    'id' in value && typeof value.id === 'string' &&
    'name' in value && typeof value.name === 'string';
}

export async function getInbox(signal?: AbortSignal): Promise<InboxItem[]> {
  const result = await request('GET', undefined, undefined, signal);
  if (!Array.isArray(result) || !result.every(isInboxItem)) {
    throw new Error('API zwróciło nieprawidłową listę Inbox.');
  }
  return result;
}

export async function createInboxItem(name: string): Promise<InboxItem> {
  const result = await request('POST', name);
  if (!isInboxItem(result)) throw new Error('Nieprawidłowe potwierdzenie zapisu. Odśwież listę przed ponownym dodaniem.');
  return result;
}

export async function updateInboxItem(id: string, name: string): Promise<InboxItem> {
  const result = await request('PATCH', name, id);
  if (!isInboxItem(result)) throw new Error('Nieprawidłowe potwierdzenie zapisu zmian. Odśwież listę przed ponowną próbą.');
  return result;
}

export async function moveInboxItemToTasks(id: string): Promise<void> {
  await request('POST', undefined, id, undefined, 'move-to-tasks');
}
