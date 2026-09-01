import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { apiFetch, apiUrl, setToken, setUnauthorizedHandler } from '../api/client'

export type UserRole = 'admin' | 'sales'

export interface AuthUser {
  id: number
  email: string
  name: string
  pictureUrl: string | null
  role: UserRole
}

interface AuthResponse {
  token: string
  expiresAt: string
  user: AuthUser
}

type AuthStatus = 'checking' | 'signedOut' | 'signedIn'

interface AuthContextValue {
  user: AuthUser | null
  status: AuthStatus
  error: string | null
  signIn: (googleCredential: string) => Promise<void>
  signOut: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

/** Reads the RFC 9457 ProblemDetails body a failed `/api/auth/google` call returns (CLAUDE.md's
 * Security section) so the sign-in screen can show the real reason — an expired Google session, a
 * client id mismatch — instead of one hardcoded string for every failure. */
async function problemDetailMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string; title?: string }
    return body.detail ?? body.title ?? 'Google sign-in was rejected by the server.'
  } catch {
    return 'Google sign-in was rejected by the server.'
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [status, setStatus] = useState<AuthStatus>('checking')
  const [error, setError] = useState<string | null>(null)

  // A token surviving a refresh is only trustworthy once /api/auth/me confirms it — a stale or
  // revoked token must never render a fake signed-in UI.
  useEffect(() => {
    let cancelled = false

    apiFetch('/api/auth/me')
      .then((response) => response.json() as Promise<AuthUser>)
      .then((currentUser) => {
        if (!cancelled) {
          setUser(currentUser)
          setStatus('signedIn')
        }
      })
      .catch(() => {
        if (!cancelled) {
          setUser(null)
          setStatus('signedOut')
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  // Any request that comes back 401 clears the token in `apiFetch`; this flips the whole UI to
  // signed-out at the same moment, rather than leaving a dead screen until the next reload.
  useEffect(() => {
    setUnauthorizedHandler(() => {
      setUser(null)
      setStatus('signedOut')
    })
    return () => setUnauthorizedHandler(null)
  }, [])

  const signIn = useCallback(async (googleCredential: string) => {
    setError(null)
    try {
      const response = await fetch(apiUrl('/api/auth/google'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ idToken: googleCredential }),
      })

      if (!response.ok) {
        throw new Error(await problemDetailMessage(response))
      }

      const body = (await response.json()) as AuthResponse
      setToken(body.token)
      setUser(body.user)
      setStatus('signedIn')
    } catch (err) {
      setToken(null)
      setUser(null)
      setStatus('signedOut')
      setError(err instanceof Error ? err.message : 'Sign-in failed. Please try again.')
    }
  }, [])

  const signOut = useCallback(() => {
    // Stateless token — there is nothing on the server to revoke, so this just forgets it locally.
    setToken(null)
    setUser(null)
    setStatus('signedOut')
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({ user, status, error, signIn, signOut }),
    [user, status, error, signIn, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }

  return context
}
