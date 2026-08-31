import { listQuotes } from '../api/endpoints'
import type { QuoteStatus } from '../api/types'
import { useAsync } from '../hooks/useAsync'
import { navigate } from '../routing/useHashRoute'
import { money, shortDate } from '../lib/format'
import { AsyncBoundary, Badge, Card, Mono } from '../components/ui'

const STATUS_TONE: Record<QuoteStatus, 'neutral' | 'info' | 'ok' | 'bad'> = {
  draft: 'neutral',
  approved: 'info',
  sent: 'ok',
  rejected: 'bad',
}

export function QuotesScreen() {
  const { state, reload } = useAsync(
    (signal) => listQuotes(signal),
    [],
    (list) => list.length === 0,
  )

  return (
    <div className="mx-auto w-full max-w-6xl p-6">
      <div className="mb-4 flex items-baseline justify-between">
        <h1 className="text-base font-semibold text-slate-900">Quotes</h1>
        {state.status === 'ready' && (
          <span className="text-xs text-slate-400">{state.data.length} quotes</span>
        )}
      </div>

      <AsyncBoundary
        status={state.status}
        error={state.status === 'error' ? state.error : undefined}
        onRetry={reload}
        empty="No quotes yet — approve one from the Desk or Approvals to see it here."
      >
        {state.status === 'ready' && (
          <Card className="overflow-hidden">
            <table className="w-full border-collapse text-[12.5px]">
              <thead>
                <tr className="text-[10px] font-semibold uppercase tracking-[0.05em] text-slate-400">
                  <th className="border-b border-slate-200 px-4 py-2.5 text-left">Number</th>
                  <th className="border-b border-slate-200 px-4 py-2.5 text-left">Customer</th>
                  <th className="border-b border-slate-200 px-4 py-2.5 text-left">Status</th>
                  <th className="border-b border-slate-200 px-4 py-2.5 text-right">Total</th>
                  <th className="border-b border-slate-200 px-4 py-2.5 text-left">Created</th>
                  <th className="border-b border-slate-200 px-4 py-2.5 text-left">Valid until</th>
                </tr>
              </thead>
              <tbody>
                {state.data.map((quote) => (
                  <tr
                    key={quote.id}
                    onClick={() => navigate({ name: 'quote', quoteId: quote.id })}
                    className="cursor-pointer hover:bg-slate-50"
                  >
                    <td className="border-b border-slate-100 px-4 py-3">
                      <Mono className="text-amber-700">{quote.number}</Mono>
                    </td>
                    <td className="border-b border-slate-100 px-4 py-3 text-slate-700">
                      {quote.customerName ?? 'Unverified sender'}
                    </td>
                    <td className="border-b border-slate-100 px-4 py-3">
                      <Badge tone={STATUS_TONE[quote.status]}>{quote.status}</Badge>
                    </td>
                    <td className="border-b border-slate-100 px-4 py-3 text-right">
                      <Mono>{money(quote.total)}</Mono>
                    </td>
                    <td className="border-b border-slate-100 px-4 py-3">
                      <Mono className="text-slate-500">{shortDate(quote.createdAt)}</Mono>
                    </td>
                    <td className="border-b border-slate-100 px-4 py-3">
                      <Mono className="text-slate-500">{shortDate(quote.validUntil)}</Mono>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Card>
        )}
      </AsyncBoundary>
    </div>
  )
}
