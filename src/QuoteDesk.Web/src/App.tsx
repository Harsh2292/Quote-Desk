import { useEffect, useState } from 'react'
import { useAuth } from './auth/AuthContext'
import { SignInScreen } from './auth/SignInScreen'

type HealthStatus = 'checking' | 'healthy' | 'unreachable'

/** The smallest honest version of "loading / healthy / error" — task 08 builds the real thing. */
function useHealthCheck(): HealthStatus {
  const [status, setStatus] = useState<HealthStatus>('checking')

  useEffect(() => {
    let cancelled = false

    fetch('/health/live')
      .then((response) => {
        if (!cancelled) setStatus(response.ok ? 'healthy' : 'unreachable')
      })
      .catch(() => {
        if (!cancelled) setStatus('unreachable')
      })

    return () => {
      cancelled = true
    }
  }, [])

  return status
}

const statusText: Record<HealthStatus, string> = {
  checking: 'Checking API…',
  healthy: 'API is healthy.',
  unreachable: 'API is unreachable.',
}

const statusDotClass: Record<HealthStatus, string> = {
  checking: 'bg-amber-400',
  healthy: 'bg-emerald-500',
  unreachable: 'bg-red-500',
}

function App() {
  const { user, status, signOut } = useAuth()
  const health = useHealthCheck()

  if (status !== 'signedIn' || !user) {
    return <SignInScreen />
  }

  return (
    <main className="flex min-h-screen flex-col items-center justify-center gap-6 bg-slate-50">
      <div className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white px-8 py-6 shadow-sm">
        <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${statusDotClass[health]}`} />
        <div>
          <h1 className="text-lg font-semibold text-slate-900">QuoteDesk</h1>
          <p className="mt-1 text-sm text-slate-600">{statusText[health]}</p>
        </div>
      </div>

      <div className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white px-6 py-3 shadow-sm">
        {user.pictureUrl && (
          <img src={user.pictureUrl} alt="" className="h-8 w-8 rounded-full" referrerPolicy="no-referrer" />
        )}
        <div className="text-sm">
          <p className="font-medium text-slate-900">{user.name}</p>
          <p className="text-slate-500">{user.role}</p>
        </div>
        <button
          type="button"
          onClick={signOut}
          className="ml-2 rounded-md border border-slate-200 px-3 py-1.5 text-sm text-slate-600 hover:bg-slate-50"
        >
          Sign out
        </button>
      </div>
    </main>
  )
}

export default App
