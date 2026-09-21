const TOKEN_KEY = 'rebeldesk.token'
const USER_KEY = 'rebeldesk.user'

export interface UserSummary {
  userId: string
  email: string
  displayName: string
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function getUser(): UserSummary | null {
  const raw = localStorage.getItem(USER_KEY)
  return raw ? (JSON.parse(raw) as UserSummary) : null
}

export function setSession(token: string, user: UserSummary): void {
  localStorage.setItem(TOKEN_KEY, token)
  localStorage.setItem(USER_KEY, JSON.stringify(user))
}

export function clearSession(): void {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_KEY)
}

export class ApiError extends Error {
  status: number
  code?: string
  constructor(status: number, code: string | undefined, message: string) {
    super(message)
    this.status = status
    this.code = code
  }
}

async function request<T>(path: string, opts: RequestInit = {}): Promise<T> {
  const headers = new Headers(opts.headers ?? {})
  if (opts.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  const token = getToken()
  if (token && !headers.has('Authorization')) headers.set('Authorization', `Bearer ${token}`)
  const res = await fetch(path, { ...opts, headers })
  if (!res.ok) {
    let code: string | undefined
    try {
      const body = await res.json()
      code = body?.error
    } catch { /* ignore */ }
    throw new ApiError(res.status, code, `HTTP ${res.status}${code ? `: ${code}` : ''}`)
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

export const api = {
  login: (email: string, password: string) =>
    request<{ token: string; userId: string; email: string; displayName: string }>(
      '/api/auth/login',
      { method: 'POST', body: JSON.stringify({ email, password }) },
    ),
  register: (email: string, displayName: string, password: string) =>
    request<{ token: string; userId: string; email: string; displayName: string }>(
      '/api/auth/register',
      { method: 'POST', body: JSON.stringify({ email, displayName, password }) },
    ),
  devices: () =>
    request<Array<{ id: string; deviceName: string; osPlatform: string; lastSeenUtc: string | null; online: boolean }>>(
      '/api/devices',
    ),
  revokeDevice: (id: string) => request<void>(`/api/devices/${id}`, { method: 'DELETE' }),
  createPairingCode: (suggestedDeviceName: string) =>
    request<{ code: string; expiresAtUtc: string }>('/api/devices/pairing', {
      method: 'POST',
      body: JSON.stringify({ suggestedDeviceName }),
    }),
  createSession: (deviceId: string) =>
    request<{ sessionId: string; sessionToken: string }>('/api/sessions', {
      method: 'POST',
      body: JSON.stringify({ deviceId }),
    }),
  getTurn: (sessionId: string) =>
    request<{ username: string; password: string; ttl: number; stunUris: string[]; turnUris: string[] }>(
      `/api/sessions/${sessionId}/turn`,
    ),
}
