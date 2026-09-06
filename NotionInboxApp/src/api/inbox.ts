export interface InboxItem {
  id: string;
  name: string;
}

async function request(method: 'GET' | 'POST', name?: string, signal?: AbortSignal): Promise<unknown> {
  const endpoint = import.meta.env.VITE_INBOX_API_URL?.trim();
  if (!endpoint) throw new Error('Brak adresu API. Ustaw VITE_INBOX_API_URL i przebuduj aplikację.');

  // TODO: attach a user access token here once backend authentication is implemented.
  // The URL is public configuration, not a substitute for authentication.
  let response: Response;
  try {
    response = await fetch(endpoint, {
      method,
      headers: method === 'POST' ? { 'Content-Type': 'application/json' } : undefined,
      body: method === 'POST' ? JSON.stringify({ name }) : undefined,
      signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(60_000)]) : AbortSignal.timeout(60_000),
      credentials: 'omit',
      cache: 'no-store',
    });
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new Error(method === 'POST'
      ? 'Nie udało się potwierdzić zapisu. Odśwież listę przed ponownym dodaniem, aby uniknąć duplikatu.'
      : 'Nie udało się połączyć z API. Sprawdź połączenie i spróbuj ponownie.');
  }

  if (!response.ok) {
    if (response.status === 400) throw new Error('Wpis musi mieć od 1 do 2000 znaków.');
    throw new Error(method === 'POST'
      ? 'API nie potwierdziło zapisu. Odśwież listę przed ponownym dodaniem.'
      : 'Nie udało się pobrać Inbox. Spróbuj ponownie później.');
  }
  try {
    return await response.json();
  } catch {
    throw new Error(method === 'POST'
      ? 'Nieprawidłowe potwierdzenie zapisu. Odśwież listę przed ponownym dodaniem.'
      : 'API zwróciło nieprawidłową odpowiedź. Spróbuj ponownie później.');
  }
}

function isInboxItem(value: unknown): value is InboxItem {
  return typeof value === 'object' && value !== null &&
    'id' in value && typeof value.id === 'string' &&
    'name' in value && typeof value.name === 'string';
}

export async function getInbox(signal?: AbortSignal): Promise<InboxItem[]> {
  const result = await request('GET', undefined, signal);
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
