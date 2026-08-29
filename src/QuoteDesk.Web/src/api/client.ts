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

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/**
 * The single place a bearer token meets the network. A future `useAgentStream` (task 07/08) reads
 * its token from {@link getToken} the same way, rather than duplicating this logic — SSE there is
 * built on `fetch` + `ReadableStream`, not `EventSource`, because `EventSource` cannot send an
 * `Authorization` header.
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
    throw new ApiError(401, 'Your session has expired. Please sign in again.')
  }

  if (!response.ok) {
    throw new ApiError(response.status, `Request to ${path} failed with ${response.status}.`)
  }

  return response
}
