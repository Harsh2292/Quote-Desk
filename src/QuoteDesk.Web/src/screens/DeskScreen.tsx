import { useCallback, useEffect, useRef, useState } from 'react'
import { createEnquiry, getEnquiry, listApprovals } from '../api/endpoints'
import { isApprovalRequest } from '../api/types'
import type { PendingApprovalSummary } from '../api/types'
import { useAgentStream } from '../hooks/useAgentStream'
import { useAsync } from '../hooks/useAsync'
import { navigate, type Route } from '../routing/useHashRoute'
import { ApprovalCard } from '../components/ApprovalCard'
import { RateLimitedPanel } from '../components/RateLimitedPanel'
import { TracePanel } from '../components/TracePanel'
import { AsyncBoundary, Button, Eyebrow, Field } from '../components/ui'

type DeskRoute = Extract<Route, { name: 'desk' }>

const SAMPLE_ENQUIRY = `Hi Mehul bhai,
Need urgent quote —
250 nos of the 6203 bearings (same as last time)
40 mtr of the 25mm PU timing belt
12 pcs ring frame spindle tape, the thicker one

Delivery at our Sachin unit, need by 5th. Last time you gave 8% on bearings, please keep same.

Kiran — Shreeji Textiles`

export function DeskScreen({ route }: { route: DeskRoute }) {
  const {
    events,
    phase,
    errorCode,
    errorMessage,
    process: startProcess,
    decide,
    replay,
  } = useAgentStream()

  const [body, setBody] = useState('')
  const [sender, setSender] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [decided, setDecided] = useState<null | 'approve' | 'reject'>(null)
  const pendingProcess = useRef<number | null>(null)

  const load = useCallback(
    async (signal: AbortSignal) => {
      if (route.enquiryId === null) return null
      const [detail, approvals] = await Promise.all([
        getEnquiry(route.enquiryId, signal),
        listApprovals(signal).catch(() => [] as PendingApprovalSummary[]),
      ])
      const match = approvals.find((a) => a.enquiryId === route.enquiryId)
      return { detail, approvalId: match?.approvalId ?? null }
    },
    [route.enquiryId],
  )

  const { state, reload } = useAsync(load, [route.enquiryId])

  // Start processing exactly once, for an enquiry this screen just created.
  useEffect(() => {
    if (route.enquiryId !== null && pendingProcess.current === route.enquiryId) {
      pendingProcess.current = null
      setDecided(null)
      startProcess(route.enquiryId)
    }
  }, [route.enquiryId, startProcess])

  const submit = async () => {
    if (body.trim().length === 0) return
    setSubmitting(true)
    setSubmitError(null)
    try {
      const created = await createEnquiry({
        body: body.trim(),
        senderId: sender.trim() || undefined,
      })
      pendingProcess.current = created.enquiryId
      navigate({ name: 'desk', enquiryId: created.enquiryId })
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'Could not submit the enquiry.')
    } finally {
      setSubmitting(false)
    }
  }

  const loaded = state.status === 'ready' ? state.data : null
  const rawBody = loaded?.detail.rawBody ?? null

  const liveApproval = [...events].reverse().find((e) => e.type === 'approval_required')
  const liveRequest =
    liveApproval && isApprovalRequest(liveApproval.payload) ? liveApproval.payload : null
  const request = liveRequest ?? loaded?.detail.pendingApproval ?? null

  const liveApprovalId =
    liveApproval && /^\d+$/.test(liveApproval.approvalId) ? Number(liveApproval.approvalId) : null
  const approvalId = liveApprovalId ?? loaded?.approvalId ?? null

  const traceEvents = events.length > 0 ? events : (loaded?.detail.trace ?? [])
  const decideBusy = decided !== null && phase === 'streaming'
  const decideDone = decided !== null && phase === 'done'

  const onApprove = () => {
    if (approvalId === null) return
    setDecided('approve')
    decide(approvalId, 'approve')
  }
  const onReject = (reason: string) => {
    if (approvalId === null) return
    setDecided('reject')
    decide(approvalId, 'reject', reason)
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex min-h-0 flex-1">
        {/* enquiry pane */}
        <section className="flex w-[468px] shrink-0 flex-col border-r border-slate-200 bg-white">
          <div className="border-b border-slate-100 px-5 py-3.5">
            <Eyebrow>{route.enquiryId === null ? 'New enquiry' : `Enquiry #${route.enquiryId}`}</Eyebrow>
          </div>

          {route.enquiryId === null ? (
            <div className="flex flex-1 flex-col gap-3.5 p-5">
              <textarea
                value={body}
                onChange={(e) => setBody(e.target.value)}
                placeholder="Paste an enquiry — an email body, a WhatsApp message, or a customer's list…"
                className="min-h-[320px] flex-1 resize-none rounded-lg border border-slate-200 bg-slate-50 p-3.5 font-mono text-[12px] leading-relaxed text-slate-700 placeholder:text-slate-300"
              />
              <button
                type="button"
                onClick={() => setBody(SAMPLE_ENQUIRY)}
                className="self-start text-[11.5px] text-amber-700 hover:text-amber-800"
              >
                Use the worked example
              </button>
              <Field label="Sender · optional">
                <input
                  value={sender}
                  onChange={(e) => setSender(e.target.value)}
                  placeholder="kiran@shreejitextiles.co.in"
                  className="rounded-md border border-slate-300 px-2.5 py-2 text-[12.5px]"
                />
              </Field>
              <Button
                onClick={submit}
                disabled={submitting || body.trim().length === 0}
                className="self-start"
              >
                Process enquiry
              </Button>
              {submitError && <p className="text-[12px] text-red-600">{submitError}</p>}
            </div>
          ) : (
            <div className="flex-1 overflow-y-auto p-5">
              {rawBody === null ? (
                <div className="text-[12px] text-slate-400">Loading enquiry…</div>
              ) : (
                <pre className="whitespace-pre-wrap rounded-lg border border-slate-200 bg-slate-50 p-3.5 font-mono text-[12px] leading-relaxed text-slate-700">
                  {rawBody}
                </pre>
              )}
            </div>
          )}
        </section>

        {/* trace / rate-limited */}
        <div className="flex min-h-0 flex-1 flex-col">
          {errorCode === 'provider_rate_limited' ? (
            <div className="flex flex-1 items-center justify-center p-10">
              <RateLimitedPanel onReplay={replay} />
            </div>
          ) : (
            <TracePanel
              events={traceEvents}
              live={phase === 'streaming'}
              meta={
                phase === 'streaming'
                  ? `running · ${traceEvents.length} events`
                  : errorCode
                    ? errorMessage ?? 'error'
                    : traceEvents.length > 0
                      ? `${traceEvents.length} events`
                      : undefined
              }
              className="flex-1"
            />
          )}
        </div>
      </div>

      {/* approval / outcome */}
      {route.enquiryId !== null && (
        <div className="border-t border-slate-200 bg-slate-50 p-5">
          <AsyncBoundary
            status={state.status}
            error={state.status === 'error' ? state.error : undefined}
            onRetry={reload}
          >
            {decideDone ? (
              <div className="mx-auto flex max-w-2xl items-center justify-between rounded-[10px] border border-slate-200 bg-white px-5 py-4">
                <span className="text-[13px] text-slate-700">
                  {decided === 'approve'
                    ? 'Quote approved and sent.'
                    : 'Quote rejected — the enquiry is closed.'}
                </span>
                <Button variant="ghost" onClick={() => navigate({ name: 'quotes' })}>
                  Go to Quotes
                </Button>
              </div>
            ) : request ? (
              <div className="mx-auto max-w-4xl">
                <ApprovalCard
                  request={request}
                  reference={
                    route.enquiryId !== null ? `enquiry #${route.enquiryId}` : undefined
                  }
                  onApprove={onApprove}
                  onReject={onReject}
                  busy={decideBusy}
                  disabled={approvalId === null}
                  disabledNote={
                    approvalId === null ? 'Replay — approve and reject are disabled' : undefined
                  }
                />
              </div>
            ) : phase === 'streaming' ? (
              <p className="text-center text-[12px] text-slate-400">Pipeline running…</p>
            ) : (
              <p className="text-center text-[12px] text-slate-400">
                No approval is pending for this enquiry.
              </p>
            )}
          </AsyncBoundary>
        </div>
      )}
    </div>
  )
}
