import { getQuote } from '../api/endpoints'
import type { QuoteStatus } from '../api/types'
import { useAsync } from '../hooks/useAsync'
import { navigate } from '../routing/useHashRoute'
import { money, shortDate, shortDateTime } from '../lib/format'
import { TracePanel } from '../components/TracePanel'
import { AsyncBoundary, Badge, Card, Eyebrow, Mono } from '../components/ui'

const STATUS_TONE: Record<QuoteStatus, 'neutral' | 'info' | 'ok' | 'bad'> = {
  draft: 'neutral',
  approved: 'info',
  sent: 'ok',
  rejected: 'bad',
}

export function QuoteDetailScreen({ quoteId }: { quoteId: number }) {
  const { state, reload } = useAsync((signal) => getQuote(quoteId, signal), [quoteId])

  return (
    <div className="mx-auto w-full max-w-6xl p-6">
      <div className="mb-3 flex items-center gap-2 text-[12px]">
        <button
          type="button"
          onClick={() => navigate({ name: 'quotes' })}
          className="text-slate-500 hover:text-slate-700"
        >
          Quotes
        </button>
        <span className="text-slate-300">/</span>
        <Mono className="text-slate-700">#{quoteId}</Mono>
      </div>

      <AsyncBoundary
        status={state.status}
        error={state.status === 'error' ? state.error : undefined}
        onRetry={reload}
      >
        {state.status === 'ready' && (
          <div className="flex items-start gap-5">
            <div className="min-w-0 flex-1">
              <Card className="overflow-hidden">
                <div className="flex items-start justify-between border-b border-slate-200 p-[18px]">
                  <div>
                    <div className="flex items-center gap-2.5">
                      <Mono className="text-[15px] font-bold text-slate-900">
                        {state.data.number}
                      </Mono>
                      <Badge tone={STATUS_TONE[state.data.status]}>{state.data.status}</Badge>
                    </div>
                    <div className="mt-1.5 text-[12.5px] text-slate-500">
                      From enquiry #{state.data.enquiryId}
                      {state.data.shipTo ? ` · ship to ${state.data.shipTo}` : ''}
                      {state.data.requiredBy
                        ? ` · required by ${shortDate(state.data.requiredBy)}`
                        : ''}
                    </div>
                  </div>
                  <div className="text-right text-[11.5px] leading-relaxed text-slate-400">
                    <div>
                      Created <Mono className="text-slate-600">{shortDateTime(state.data.createdAt)}</Mono>
                    </div>
                    {state.data.approvedAt && (
                      <div>
                        Approved <Mono className="text-slate-600">{shortDateTime(state.data.approvedAt)}</Mono>
                      </div>
                    )}
                    {state.data.sentAt && (
                      <div>
                        Sent <Mono className="text-slate-600">{shortDateTime(state.data.sentAt)}</Mono>
                      </div>
                    )}
                  </div>
                </div>

                <table className="w-full border-collapse text-[12px]">
                  <thead>
                    <tr className="text-[10px] font-semibold uppercase tracking-[0.05em] text-slate-400">
                      <th className="border-b border-slate-200 px-3.5 py-2.5 text-left">SKU</th>
                      <th className="border-b border-slate-200 px-3.5 py-2.5 text-right">Qty</th>
                      <th className="border-b border-slate-200 px-3.5 py-2.5 text-right">Unit</th>
                      <th className="border-b border-slate-200 px-3.5 py-2.5 text-right">Disc</th>
                      <th className="border-b border-slate-200 px-3.5 py-2.5 text-right">Line total</th>
                      <th className="border-b border-slate-200 px-3.5 py-2.5 text-left">Dispatch</th>
                      <th className="border-b border-slate-200 px-3.5 py-2.5 text-left">Delivery</th>
                      <th className="border-b border-slate-200 px-3.5 py-2.5 text-left">Note</th>
                    </tr>
                  </thead>
                  <tbody>
                    {state.data.lines.map((line) => (
                      <tr key={line.id} className={line.requiresOverride ? 'bg-red-50' : undefined}>
                        <td className="border-b border-slate-100 px-3.5 py-2.5">
                          <Mono>{line.sku}</Mono>
                        </td>
                        <td className="border-b border-slate-100 px-3.5 py-2.5 text-right">
                          <Mono>{line.qty}</Mono>
                        </td>
                        <td className="border-b border-slate-100 px-3.5 py-2.5 text-right">
                          <Mono>{money(line.unitPrice)}</Mono>
                        </td>
                        <td className="border-b border-slate-100 px-3.5 py-2.5 text-right">
                          <Mono>{line.discountPct}%</Mono>
                        </td>
                        <td className="border-b border-slate-100 px-3.5 py-2.5 text-right">
                          <Mono>{money(line.lineTotal)}</Mono>
                        </td>
                        <td className="border-b border-slate-100 px-3.5 py-2.5">
                          <Mono className="text-slate-500">{shortDate(line.dispatchDate)}</Mono>
                        </td>
                        <td className="border-b border-slate-100 px-3.5 py-2.5">
                          <Mono className="text-slate-500">{shortDate(line.deliveryDate)}</Mono>
                        </td>
                        <td className="border-b border-slate-100 px-3.5 py-2.5 text-slate-500">
                          {line.note ?? '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <div className="flex justify-end border-t border-slate-200 p-[18px]">
                  <dl className="w-[280px] text-[12.5px]">
                    <Row label="Subtotal" value={money(state.data.subtotal)} />
                    <Row label="Freight" value={money(state.data.freight)} />
                    <Row label="GST 18%" value={money(state.data.tax)} />
                    <div className="mt-1 flex items-center justify-between border-t border-slate-200 pt-2 font-bold">
                      <dt>Total</dt>
                      <dd>
                        <Mono>{money(state.data.total)}</Mono>
                      </dd>
                    </div>
                    <div className="flex items-center justify-between pt-1 text-slate-400">
                      <dt>Valid until</dt>
                      <dd>
                        <Mono>{shortDate(state.data.validUntil)}</Mono>
                      </dd>
                    </div>
                  </dl>
                </div>
              </Card>
            </div>

            <div className="w-[420px] shrink-0">
              <Card className="overflow-hidden">
                <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3">
                  <Eyebrow>Trace that produced this quote</Eyebrow>
                  <span className="text-[10.5px] text-slate-300">replay</span>
                </div>
                {state.data.trace && state.data.trace.length > 0 ? (
                  <TracePanel events={state.data.trace} />
                ) : (
                  <p className="px-4 py-6 text-[12px] text-slate-400">
                    No trace was recorded for this quote.
                  </p>
                )}
              </Card>
            </div>
          </div>
        )}
      </AsyncBoundary>
    </div>
  )
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between py-1">
      <dt className="text-slate-500">{label}</dt>
      <dd>
        <Mono>{value}</Mono>
      </dd>
    </div>
  )
}
