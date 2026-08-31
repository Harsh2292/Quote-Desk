import { useEffect, useState } from 'react'
import { listApprovals } from '../api/endpoints'
import { useAgentStream } from '../hooks/useAgentStream'
import { useAsync } from '../hooks/useAsync'
import { ApprovalCard } from '../components/ApprovalCard'
import { AsyncBoundary } from '../components/ui'

export function ApprovalsScreen() {
  const { phase, decide } = useAgentStream()
  const { state, reload } = useAsync(
    (signal) => listApprovals(signal),
    [],
    (list) => list.length === 0,
  )
  const [deciding, setDeciding] = useState<number | null>(null)

  // React to the decision stream finishing (an external system) — clear the busy row and refetch,
  // so the approved / rejected card drops off the list.
  useEffect(() => {
    if (deciding !== null && (phase === 'done' || phase === 'error')) {
      // oxlint-disable-next-line react/set-state-in-effect
      setDeciding(null)
      reload()
    }
  }, [deciding, phase, reload])

  const run = (id: number, decision: 'approve' | 'reject', reason?: string) => {
    setDeciding(id)
    decide(id, decision, reason)
  }

  return (
    <div className="mx-auto w-full max-w-5xl p-6">
      <div className="mb-4 flex items-baseline justify-between">
        <h1 className="text-base font-semibold text-slate-900">Pending approvals</h1>
        {state.status === 'ready' && (
          <span className="text-xs text-slate-400">{state.data.length} waiting</span>
        )}
      </div>

      <AsyncBoundary
        status={state.status}
        error={state.status === 'error' ? state.error : undefined}
        onRetry={reload}
        empty="No approvals are waiting."
      >
        {state.status === 'ready' && (
          <div className="space-y-4">
            {state.data.map((approval) => (
              <ApprovalCard
                key={approval.approvalId}
                request={approval.request}
                reference={`enquiry #${approval.enquiryId} · run #${approval.approvalId}`}
                busy={deciding === approval.approvalId && phase === 'streaming'}
                disabled={deciding !== null && deciding !== approval.approvalId}
                onApprove={() => run(approval.approvalId, 'approve')}
                onReject={(reason) => run(approval.approvalId, 'reject', reason)}
              />
            ))}
          </div>
        )}
      </AsyncBoundary>
    </div>
  )
}
