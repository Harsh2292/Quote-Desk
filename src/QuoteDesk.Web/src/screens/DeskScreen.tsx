import { useCallback, useState } from 'react'
import type { ReactNode } from 'react'
import { getEnquiry, listApprovals } from '../api/endpoints'
import { isApprovalRequest } from '../api/types'
import type { AgentEvent } from '../api/agentEvents'
import type { ApprovalRequest, PendingApprovalSummary } from '../api/types'
import { useDeskSession } from '../desk/DeskSessionContext'
import { SAMPLE_ENQUIRIES, type SampleEnquiry } from '../desk/sampleEnquiries'
import { useAsync } from '../hooks/useAsync'
import { navigate, type Route } from '../routing/useHashRoute'
import { ApprovalCard } from '../components/ApprovalCard'
import { RateLimitedPanel } from '../components/RateLimitedPanel'
import { TracePanel } from '../components/TracePanel'
import { AsyncBoundary, Button, Card, Eyebrow, Field, Mono } from '../components/ui'

type DeskRoute = Extract<Route, { name: 'desk' }>

function lastApprovalRequest(events: AgentEvent[]): {
  request: ApprovalRequest | null
  approvalId: number | null
} {
  const event = [...events].reverse().find((e) => e.type === 'approval_required')
  if (!event || event.type !== 'approval_required') return { request: null, approvalId: null }
  return {
    request: isApprovalRequest(event.payload) ? event.payload : null,
    approvalId: /^\d+$/.test(event.approvalId) ? Number(event.approvalId) : null,
  }
}

export function DeskScreen({ route }: { route: DeskRoute }) {
  const session = useDeskSession()
  const { stream } = session

  const isActiveRun = route.enquiryId !== null && route.enquiryId === session.activeEnquiryId
  const isHistorical = route.enquiryId !== null && route.enquiryId !== session.activeEnquiryId

  // A historical / deep-linked enquiry (reached from Approvals or Quotes) — load it read-only.
  const load = useCallback(
    async (signal: AbortSignal) => {
      if (!isHistorical || route.enquiryId === null) return null
      const [detail, approvals] = await Promise.all([
        getEnquiry(route.enquiryId, signal),
        listApprovals(signal).catch(() => [] as PendingApprovalSummary[]),
      ])
      const match = approvals.find((a) => a.enquiryId === route.enquiryId)
      return { detail, approvalId: match?.approvalId ?? null }
    },
    [isHistorical, route.enquiryId],
  )
  const { state, reload } = useAsync(load, [isHistorical, route.enquiryId])
  const historical = state.status === 'ready' ? state.data : null

  // ── the blank desk ────────────────────────────────────────────────────────
  if (!isActiveRun && !isHistorical) {
    return (
      <div className="flex min-h-0 flex-1">
        <EnquiryPane title="New enquiry">
          <div className="flex flex-1 flex-col gap-3.5 p-5">
            <textarea
              value={session.draftBody}
              onChange={(e) => session.setDraftBody(e.target.value)}
              placeholder="Paste an enquiry — an email body, a WhatsApp message, or a customer's list…"
              className="min-h-[320px] flex-1 resize-none rounded-lg border border-slate-200 bg-slate-50 p-3.5 font-mono text-[12px] leading-relaxed text-slate-700 placeholder:text-slate-300"
            />
            <SampleEnquiryPicker
              onPick={(sample) => {
                session.setDraftBody(sample.body)
                session.setDraftSender(sample.sender)
              }}
            />
            <Field label="Sender · optional">
              <input
                value={session.draftSender}
                onChange={(e) => session.setDraftSender(e.target.value)}
                placeholder="kiran@shreejitextiles.co.in"
                className="rounded-md border border-slate-300 px-2.5 py-2 text-[12.5px]"
              />
            </Field>
            <Button
              onClick={() => void session.submitDraft()}
              disabled={session.submitting || session.draftBody.trim().length === 0}
              className="self-start"
            >
              Process enquiry
            </Button>
            {session.submitError && (
              <p className="text-[12px] text-red-600">{session.submitError}</p>
            )}
          </div>
        </EnquiryPane>
        <div className="flex min-h-0 flex-1 flex-col">
          <TracePanel events={[]} className="flex-1" />
        </div>
      </div>
    )
  }

  // ── an active run this session is driving ─────────────────────────────────
  if (isActiveRun) {
    const { request, approvalId } = lastApprovalRequest(stream.events)
    const decideBusy = session.decided !== null && stream.phase === 'streaming'
    const decideDone = session.decided !== null && stream.phase === 'done'
    const failed = stream.phase === 'error'

    return (
      <div className="flex min-h-0 flex-1 flex-col">
        <div className="flex min-h-0 flex-1">
          <EnquiryPane
            title={`Enquiry #${route.enquiryId}`}
            actions={
              <div className="flex gap-2">
                {failed && (
                  <>
                    <button
                      type="button"
                      onClick={session.retry}
                      className="text-[11.5px] font-medium text-slate-600 hover:text-slate-900"
                    >
                      Retry
                    </button>
                    <button
                      type="button"
                      onClick={() => session.editForRerun(session.draftBody)}
                      className="text-[11.5px] font-medium text-slate-600 hover:text-slate-900"
                    >
                      Edit &amp; re-run
                    </button>
                  </>
                )}
                <button
                  type="button"
                  onClick={session.reset}
                  className="text-[11.5px] font-medium text-amber-700 hover:text-amber-800"
                >
                  New enquiry
                </button>
              </div>
            }
          >
            <div className="flex-1 overflow-y-auto p-5">
              <pre className="whitespace-pre-wrap rounded-lg border border-slate-200 bg-slate-50 p-3.5 font-mono text-[12px] leading-relaxed text-slate-700">
                {session.draftBody}
              </pre>
              {failed && (
                <p className="mt-3 text-[12px] text-red-600">
                  {stream.errorMessage ?? 'The run failed.'} Your enquiry text is kept — Retry runs it
                  again, or Edit &amp; re-run to change it first.
                </p>
              )}
            </div>
          </EnquiryPane>

          <div className="flex min-h-0 flex-1 flex-col">
            {stream.errorCode === 'provider_rate_limited' ? (
              <div className="flex flex-1 items-center justify-center p-10">
                <RateLimitedPanel onReplay={stream.replay} />
              </div>
            ) : (
              <TracePanel
                events={stream.events}
                live={stream.phase === 'streaming'}
                meta={traceMeta(stream)}
                className="flex-1"
              />
            )}
          </div>
        </div>

        <OutcomeBar
          decideDone={decideDone}
          decided={session.decided}
          request={request}
          approvalId={approvalId}
          decideBusy={decideBusy}
          reference={`enquiry #${route.enquiryId}`}
          streaming={stream.phase === 'streaming'}
          failed={failed}
          onApprove={() => {
            if (approvalId === null) return
            session.setDecided('approve')
            stream.decide(approvalId, 'approve')
          }}
          onReject={(reason) => {
            if (approvalId === null) return
            session.setDecided('reject')
            stream.decide(approvalId, 'reject', reason)
          }}
        />
      </div>
    )
  }

  // ── a historical enquiry reached by deep link ─────────────────────────────
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="flex min-h-0 flex-1">
        <EnquiryPane
          title={`Enquiry #${route.enquiryId}`}
          actions={
            <button
              type="button"
              onClick={session.reset}
              className="text-[11.5px] font-medium text-amber-700 hover:text-amber-800"
            >
              New enquiry
            </button>
          }
        >
          <div className="flex-1 overflow-y-auto p-5">
            {historical === null ? (
              <div className="text-[12px] text-slate-400">Loading enquiry…</div>
            ) : (
              <pre className="whitespace-pre-wrap rounded-lg border border-slate-200 bg-slate-50 p-3.5 font-mono text-[12px] leading-relaxed text-slate-700">
                {historical.detail.rawBody}
              </pre>
            )}
          </div>
        </EnquiryPane>
        <div className="flex min-h-0 flex-1 flex-col">
          <TracePanel
            events={historical?.detail.trace ?? []}
            meta={
              historical?.detail.trace && historical.detail.trace.length > 0
                ? `${historical.detail.trace.length} events`
                : undefined
            }
            className="flex-1"
          />
        </div>
      </div>

      {historical?.detail.pendingApproval && (
        <div className="border-t border-slate-200 bg-slate-50 p-5">
          <AsyncBoundary
            status={state.status}
            error={state.status === 'error' ? state.error : undefined}
            onRetry={reload}
          >
            <div className="mx-auto max-w-4xl">
              <ApprovalCard
                request={historical.detail.pendingApproval}
                reference={`enquiry #${route.enquiryId}`}
                disabled
                disabledNote="Open this approval from the Approvals screen to act on it"
                onApprove={() => navigate({ name: 'approvals' })}
                onReject={() => navigate({ name: 'approvals' })}
              />
            </div>
          </AsyncBoundary>
        </div>
      )}
    </div>
  )
}

function traceMeta(stream: ReturnType<typeof useDeskSession>['stream']): string | undefined {
  if (stream.phase === 'streaming') return `running · ${stream.events.length} events`
  if (stream.errorCode) return stream.errorMessage ?? 'error'
  if (stream.events.length > 0) return `${stream.events.length} events`
  return undefined
}

/**
 * Replaces the old bare "Use the worked example" link. Collapsed by default so a first-time visitor
 * sees the textarea first, not a wall of sample copy — CLAUDE.md's "no landing page" applies here in
 * spirit even though this is the Desk, not a separate screen.
 */
function SampleEnquiryPicker({ onPick }: { onPick: (sample: SampleEnquiry) => void }) {
  const [open, setOpen] = useState(false)

  return (
    <Card className="overflow-hidden">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-center justify-between px-3.5 py-2.5 text-left hover:bg-slate-50"
      >
        <Eyebrow>Try a sample enquiry</Eyebrow>
        <span className="text-[11px] font-medium text-amber-700">{open ? 'Hide' : 'Show'}</span>
      </button>

      {open && (
        <div className="divide-y divide-slate-100 border-t border-slate-100">
          {SAMPLE_ENQUIRIES.map((sample) => (
            <button
              key={sample.id}
              type="button"
              onClick={() => onPick(sample)}
              className="flex w-full flex-col gap-0.5 px-3.5 py-2.5 text-left hover:bg-slate-50"
            >
              <span className="flex items-center gap-2">
                <span className="text-[12.5px] font-medium text-slate-900">{sample.label}</span>
                <Mono className="text-[10.5px] text-slate-400">{sample.sender}</Mono>
              </span>
              <span className="text-[11px] leading-relaxed text-slate-500">{sample.why}</span>
            </button>
          ))}
        </div>
      )}
    </Card>
  )
}

function EnquiryPane({
  title,
  actions,
  children,
}: {
  title: string
  actions?: ReactNode
  children: ReactNode
}) {
  return (
    <section className="flex w-[468px] shrink-0 flex-col border-r border-slate-200 bg-white">
      <div className="flex items-center justify-between border-b border-slate-100 px-5 py-3.5">
        <Eyebrow>{title}</Eyebrow>
        {actions}
      </div>
      {children}
    </section>
  )
}

function OutcomeBar({
  decideDone,
  decided,
  request,
  approvalId,
  decideBusy,
  reference,
  streaming,
  failed,
  onApprove,
  onReject,
}: {
  decideDone: boolean
  decided: 'approve' | 'reject' | null
  request: ApprovalRequest | null
  approvalId: number | null
  decideBusy: boolean
  reference: string
  streaming: boolean
  failed: boolean
  onApprove: () => void
  onReject: (reason: string) => void
}) {
  if (failed) return null

  return (
    <div className="border-t border-slate-200 bg-slate-50 p-5">
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
            reference={reference}
            onApprove={onApprove}
            onReject={onReject}
            busy={decideBusy}
            disabled={approvalId === null}
            disabledNote={approvalId === null ? 'Replay — approve and reject are disabled' : undefined}
          />
        </div>
      ) : streaming ? (
        <p className="text-center text-[12px] text-slate-400">Pipeline running…</p>
      ) : (
        <p className="text-center text-[12px] text-slate-400">
          No approval is pending for this enquiry.
        </p>
      )}
    </div>
  )
}
