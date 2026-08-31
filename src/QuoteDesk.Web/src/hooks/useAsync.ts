import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Every async surface renders loading / empty / error. Modelling those as a discriminated union
 * makes that structural — a screen `switch`es on `status` and cannot forget a case.
 */
export type AsyncState<T> =
  | { status: 'loading' }
  | { status: 'empty' }
  | { status: 'ready'; data: T }
  | { status: 'error'; error: Error }

export interface UseAsync<T> {
  state: AsyncState<T>
  reload: () => void
}

/**
 * Runs `fetcher` on mount and whenever the serialised `deps` change. `isEmpty` decides whether a
 * successful result is the empty state (an empty array, a null) so screens do not each re-check.
 */
export function useAsync<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  deps: readonly unknown[],
  isEmpty: (data: T) => boolean = () => false,
): UseAsync<T> {
  const [state, setState] = useState<AsyncState<T>>({ status: 'loading' })
  const [nonce, setNonce] = useState(0)

  // Latest-value refs so a changing `fetcher` / `isEmpty` identity does not restart the fetch —
  // only the serialised `deps` (and an explicit `reload`) do. Synced in an effect that runs before
  // the keyed effect below on the same commit.
  const fetcherRef = useRef(fetcher)
  const isEmptyRef = useRef(isEmpty)
  useEffect(() => {
    fetcherRef.current = fetcher
    isEmptyRef.current = isEmpty
  })

  const key = JSON.stringify(deps)

  useEffect(() => {
    const ac = new AbortController()
    // Reset to loading whenever the inputs change — synchronising the hook with the network.
    // oxlint-disable-next-line react/set-state-in-effect
    setState({ status: 'loading' })

    fetcherRef.current(ac.signal).then(
      (data) => {
        if (ac.signal.aborted) return
        setState(isEmptyRef.current(data) ? { status: 'empty' } : { status: 'ready', data })
      },
      (error: unknown) => {
        if (ac.signal.aborted) return
        setState({
          status: 'error',
          error: error instanceof Error ? error : new Error(String(error)),
        })
      },
    )

    return () => ac.abort()
  }, [key, nonce])

  const reload = useCallback(() => setNonce((n) => n + 1), [])
  return { state, reload }
}
