import { useCallback, useEffect, useRef, useState } from 'react'
import type { AgentEvent } from '../api/agentEvents'
import { apiJson } from '../api/client'
import { openAgentStream, StreamProblem } from '../api/stream'
import type { EnquiryDetailResponse } from '../api/types'

export type StreamPhase = 'idle' | 'streaming' | 'done' | 'error'
export type StreamErrorCode = 'provider_rate_limited' | 'demo_rate_limited' | 'budget_exceeded' | 'internal'

interface AgentStreamState {
  phase: StreamPhase
  events: AgentEvent[]
  /** Set when the run ended on an `error` event or a transport failure. */
  errorCode: StreamErrorCode | null
  errorMessage: string | null
}

export interface UseAgentStream extends AgentStreamState {
  /** Whether the last event is an approval gate — the run suspended, waiting for a human. */
  awaitingApproval: boolean
  /** POST an enquiry into the pipeline: Extract → Resolve → Price, then suspend at the approval gate. */
  process: (enquiryId: number) => void
  /** POST an approval decision and stream the Approve stage. */
  decide: (approvalId: number, decision: 'approve' | 'reject', rejectionReason?: string) => void
  /** Show a recorded run with no network — the `provider_rate_limited` fallback. */
  replay: (events: AgentEvent[]) => void
  /** Re-fetch the persisted trace after a dropped connection (the server has no SSE resume). */
  recover: (enquiryId: number) => void
  reset: () => void
}

const IDLE: AgentStreamState = { phase: 'idle', events: [], errorCode: null, errorMessage: null }

/**
 * The one hook that owns SSE reading. Built on `fetch` + `ReadableStream`, not `EventSource`: both
 * streaming endpoints are POST and need a bearer `Authorization` header, which `EventSource` cannot
 * send. Do not read streams anywhere else.
 */
export function useAgentStream(): UseAgentStream {
  const [state, setState] = useState<AgentStreamState>(IDLE)
  const controller = useRef<AbortController | null>(null)

  const stop = useCallback(() => {
    controller.current?.abort()
    controller.current = null
  }, [])

  useEffect(() => stop, [stop])

  const run = useCallback(
    async (path: string, body?: unknown) => {
      stop()
      const ac = new AbortController()
      controller.current = ac
      setState({ ...IDLE, phase: 'streaming' })

      try {
        for await (const event of openAgentStream(path, ac.signal, body)) {
          setState((s) => {
            const events = [...s.events, event]
            if (event.type === 'error') {
              return { phase: 'error', events, errorCode: event.code, errorMessage: event.message }
            }
            return { ...s, events }
          })
        }
        setState((s) => (s.phase === 'error' ? s : { ...s, phase: 'done' }))
      } catch (err) {
        if (ac.signal.aborted) return
        // A 429 here is transport-level — it arrived as a plain JSON response, not an SSE stream —
        // which only ever happens now because our own rate limiter (task 09) rejected the request
        // before the pipeline ran. A model-provider rate limit is a completely different path: the
        // pipeline already started, the response is a normal event-stream, and the failure arrives as
        // an `error` event with code `provider_rate_limited` (handled above, in the `for await` loop).
        const code: StreamErrorCode =
          err instanceof StreamProblem && err.status === 429 ? 'demo_rate_limited' : 'internal'
        const message =
          err instanceof StreamProblem ? err.message : 'The connection to the server was lost.'
        setState((s) => ({ ...s, phase: 'error', errorCode: code, errorMessage: message }))
      } finally {
        if (controller.current === ac) controller.current = null
      }
    },
    [stop],
  )

  const process = useCallback(
    (enquiryId: number) => {
      void run(`/api/enquiries/${enquiryId}/process`)
    },
    [run],
  )

  const decide = useCallback(
    (approvalId: number, decision: 'approve' | 'reject', rejectionReason?: string) => {
      void run(`/api/approvals/${approvalId}`, { decision, rejectionReason })
    },
    [run],
  )

  const replay = useCallback(
    (events: AgentEvent[]) => {
      stop()
      const errorEvent = events.find((e) => e.type === 'error')
      setState({
        phase: errorEvent ? 'error' : 'done',
        events,
        errorCode: errorEvent?.type === 'error' ? errorEvent.code : null,
        errorMessage: errorEvent?.type === 'error' ? errorEvent.message : null,
      })
    },
    [stop],
  )

  const recover = useCallback(
    (enquiryId: number) => {
      stop()
      void (async () => {
        try {
          const detail = await apiJson<EnquiryDetailResponse>(`/api/enquiries/${enquiryId}`)
          setState({ ...IDLE, phase: 'done', events: detail.trace ?? [] })
        } catch {
          setState((s) => ({
            ...s,
            phase: 'error',
            errorCode: 'internal',
            errorMessage: 'Could not reload the trace.',
          }))
        }
      })()
    },
    [stop],
  )

  const reset = useCallback(() => {
    stop()
    setState(IDLE)
  }, [stop])

  const last = state.events[state.events.length - 1]

  return {
    ...state,
    awaitingApproval: last?.type === 'approval_required',
    process,
    decide,
    replay,
    recover,
    reset,
  }
}
