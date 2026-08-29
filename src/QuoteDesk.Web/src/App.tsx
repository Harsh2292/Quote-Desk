import { useEffect, useState } from 'react'

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
  const status = useHealthCheck()

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-50">
      <div className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white px-8 py-6 shadow-sm">
        <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${statusDotClass[status]}`} />
        <div>
          <h1 className="text-lg font-semibold text-slate-900">QuoteDesk</h1>
          <p className="mt-1 text-sm text-slate-600">{statusText[status]}</p>
        </div>
      </div>
    </main>
  )
}

export default App
