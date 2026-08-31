const TOKEN_KEY = 'qd.auth'

/** Read directly from storage rather than through AuthContext, so apiFetch has no dependency on React. */
export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY)
  } catch {
    return null
  }
}

export function setToken(token: string | null): void {
  try {
    if (token) {
      localStorage.setItem(TOKEN_KEY, token)
    } else {
      localStorage.removeItem(TOKEN_KEY)
    }
  } catch {
    // Storage can be unavailable (private browsing, blocked site data) — the session simply
    // won't survive a refresh; it is not worth failing the sign-in over.
  }
}

/**
 * Called when any request comes back 401. `AuthProvider` registers a handler here so the whole UI
 * flips to signed-out immediately, rather than staying on a dead screen until the next reload.
 */
let onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  onUnauthorized = handler
}

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/** Pull a human message out of an RFC 9457 ProblemDetails body, falling back to a generic line. */
async function problemDetail(response: Response, path: string): Promise<string> {
  const fallback = `Request to ${path} failed with ${response.status}.`
  if (!response.headers.get('content-type')?.includes('json')) return fallback
  try {
    const body = (await response.json()) as { detail?: unknown; title?: unknown }
    if (typeof body.detail === 'string' && body.detail.length > 0) return body.detail
    if (typeof body.title === 'string' && body.title.length > 0) return body.title
    return fallback
  } catch {
    return fallback
  }
}

/**
 * The single place a bearer token meets the network for non-streaming calls. `useAgentStream` reads
 * its token from {@link getToken} the same way but does its own `fetch`, because it needs the raw
 * `ReadableStream` and `EventSource` cannot send an `Authorization` header.
 */
export async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  const token = getToken()
  const headers = new Headers(init?.headers)
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(path, { ...init, headers })

  if (response.status === 401) {
    setToken(null)
    onUnauthorized?.()
    throw new ApiError(401, 'Your session has expired. Please sign in again.')
  }

  if (!response.ok) {
    throw new ApiError(response.status, await problemDetail(response, path))
  }

  return response
}

/** `apiFetch` + `.json()`, for the common case. */
export async function apiJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await apiFetch(path, init)
  return (await response.json()) as T
}
