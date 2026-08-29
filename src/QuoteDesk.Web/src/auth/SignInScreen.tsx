import { GoogleLogin, type CredentialResponse } from '@react-oauth/google'
import { useAuth } from './AuthContext'

/** Shown whenever `status !== 'signedIn'` — the loading and error states CLAUDE.md requires. */
export function SignInScreen() {
  const { status, error, signIn } = useAuth()

  const handleSuccess = (response: CredentialResponse) => {
    if (response.credential) {
      void signIn(response.credential)
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50">
      <div className="flex w-full max-w-sm flex-col items-center gap-4 rounded-lg border border-slate-200 bg-white px-8 py-10 shadow-sm">
        <h1 className="text-lg font-semibold text-slate-900">QuoteDesk</h1>
        <p className="text-center text-sm text-slate-600">Sign in with your Google account to continue.</p>

        {status === 'checking' ? (
          <p className="text-sm text-slate-500">Checking your session…</p>
        ) : (
          <GoogleLogin onSuccess={handleSuccess} onError={() => undefined} />
        )}

        {error && <p className="text-sm text-red-600">{error}</p>}
      </div>
    </main>
  )
}
