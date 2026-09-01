import { useState } from 'react'
import { GoogleLogin, type CredentialResponse } from '@react-oauth/google'
import { useAuth } from './AuthContext'
import { Card, Spinner } from '../components/ui'

/**
 * Shown whenever `status !== 'signedIn'` — the loading and error states CLAUDE.md requires. This is
 * the first thing anyone opening the live URL sees, so task 09 rebuilt it on the shared design
 * primitives rather than the raw Tailwind the original (task 04a) shipped with, and fixed three real
 * defects found in that version: Google's own `onError` was silently swallowed
 * (`onError={() => undefined}`), there was no in-flight state between the credential arriving and
 * `/api/auth/google` answering, and an empty `response.credential` was dropped with no feedback.
 */
export function SignInScreen() {
  const { status, error, signIn } = useAuth()
  const [signingIn, setSigningIn] = useState(false)
  const [googleError, setGoogleError] = useState<string | null>(null)

  const handleSuccess = (response: CredentialResponse) => {
    if (!response.credential) {
      setGoogleError('Google did not return a credential. Please try again.')
      return
    }

    setGoogleError(null)
    setSigningIn(true)
    void signIn(response.credential).finally(() => setSigningIn(false))
  }

  const handleError = () => {
    setGoogleError('Google sign-in did not complete — check that third-party cookies are allowed, or try again.')
  }

  const message = googleError ?? error
  const checking = status === 'checking'

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50 p-6">
      <Card className="flex w-full max-w-sm flex-col items-center gap-5 px-8 py-10">
        <div className="flex flex-col items-center gap-2 text-center">
          <div className="flex size-9 items-center justify-center rounded-[5px] bg-slate-900 text-[13px] font-semibold text-white">
            Q
          </div>
          <h1 className="text-base font-semibold text-slate-900">QuoteDesk</h1>
          <p className="max-w-xs text-[12.5px] leading-relaxed text-slate-500">
            An agentic RFQ-to-quotation demo. Sign in with any Google account to try it — nothing is
            written outside your own session.
          </p>
        </div>

        <div className="flex min-h-[44px] w-full items-center justify-center">
          {checking ? (
            <span className="flex items-center gap-2 text-[12.5px] text-slate-500">
              <Spinner /> Checking your session…
            </span>
          ) : signingIn ? (
            <span className="flex items-center gap-2 text-[12.5px] text-slate-500">
              <Spinner /> Signing you in…
            </span>
          ) : (
            <GoogleLogin onSuccess={handleSuccess} onError={handleError} />
          )}
        </div>

        {message && <p className="text-center text-[12px] text-red-600">{message}</p>}

        <p className="text-center text-[10.5px] text-slate-400">
          Hosted on a free tier — the first request after a period of inactivity can take a few
          seconds to wake up.
        </p>
      </Card>
    </main>
  )
}
