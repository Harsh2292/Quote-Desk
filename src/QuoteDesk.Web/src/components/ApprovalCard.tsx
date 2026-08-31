import { useState } from 'react'
import type { ReactNode } from 'react'
import type { ApprovalRequest, PricedQuoteLine } from '../api/types'
import { money, shortDate } from '../lib/format'
import { cn } from '../lib/cn'
import { Badge, Button, Card, Eyebrow, Mono, Spinner, StatusDot } from './ui'

/**
 * The one card a salesperson acts on — rendered identically from a live `ApprovalRequiredEvent`
 * payload and from `GET /api/approvals`. Approve / reject only: resolving an ambiguous line needs
 * backend work that is deferred, so unresolved lines are shown in red with the agent's reason and
 * the quote cannot be sent until a human deals with them.
 */

function hasDateConflict(line: PricedQuoteLine, requiredBy: string | null): boolean {
  if (!requiredBy || !line.deliveryDate) return false
  return line.deliveryDate.slice(0, 10) > requiredBy.slice(0, 10)
}

export function ApprovalCard({
  request,
  reference,
  onApprove,
  onReject,
  busy = false,
  disabled = false,
  disabledNote,
}: {
  request: ApprovalRequest
  reference?: string
  onApprove: () => void
  onReject: (reason: string) => void
  busy?: boolean
  disabled?: boolean
  disabledNote?: string
}) {
  const [reason, setReason] = useState('')
  const { pricedQuote: quote, unresolved } = request

  const anyOverride = quote.lines.some((l) => l.requiresOverride)
  const anyDateConflict = quote.lines.some((l) => hasDateConflict(l, request.requiredBy))
  const anyPolicyDiscount = quote.lines.some(
    (l) => l.discountPct > 0 && !l.requiresOverride && request.customerId !== null,
  )

  const actionsDisabled = busy || disabled

  return (
    <Card className="overflow-hidden">
      <header className="flex items-center justify-between border-b border-slate-200 bg-amber-50 px-[18px] py-3.5">
        <div className="flex items-center gap-2.5">
          <svg
            className="text-amber-600"
            width="15"
            height="15"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            aria-hidden="true"
          >
            <path d="M12 9v4M12 17h.01M10.3 3.9 2 18a1.7 1.7 0 0 0 1.5 2.5h17A1.7 1.7 0 0 0 22 18L13.7 3.9a1.7 1.7 0 0 0-3 0Z" />
          </svg>
          <span className="text-[12px] font-semibold uppercase tracking-[0.06em] text-amber-700">
            Approval required
          </span>
        </div>
        {reference && (
          <Mono className="text-[11.5px] text-slate-400">{reference} · approve_quote</Mono>
        )}
      </header>

      <div className="border-b border-slate-100 px-[18px] py-4">
        <div className="text-[13.5px] font-semibold text-slate-900">
          {request.customerName ?? 'Unmatched sender'}
        </div>
        <div className="mt-0.5 text-[12px] text-slate-500">
          {request.customerId === null
            ? 'No customer on file — verify before sending'
            : `Customer #${request.customerId}`}
          {request.shipTo ? ` · ship to ${request.shipTo}` : ''}
          {request.requiredBy ? ` · required by ${shortDate(request.requiredBy)}` : ''}
        </div>
        <p className="mt-2.5 text-[12.5px] leading-relaxed text-slate-600">{request.narration}</p>
      </div>

      <table className="w-full border-collapse text-[12px]">
        <thead>
          <tr className="border-b border-slate-200 text-[10px] font-semibold uppercase tracking-[0.05em] text-slate-400">
            <th className="px-[18px] py-2.5 text-left">SKU</th>
            <th className="py-2.5 pr-3 text-right">Qty</th>
            <th className="py-2.5 pr-3 text-right">List</th>
            <th className="py-2.5 pr-3 text-right">Disc</th>
            <th className="py-2.5 pr-3 text-right">Net unit</th>
            <th className="py-2.5 pr-3 text-right">Line total</th>
            <th className="px-[18px] py-2.5 text-right">Status</th>
          </tr>
        </thead>
        <tbody>
          {quote.lines.map((line, i) => {
            const dateConflict = hasDateConflict(line, request.requiredBy)
            return (
              <tr
                key={`${line.sku}-${i}`}
                className={cn(
                  'border-b border-slate-100 align-top',
                  line.requiresOverride && 'bg-red-50',
                )}
              >
                <td className="px-[18px] py-2.5">
                  <Mono className="text-slate-900">{line.sku}</Mono>
                  {dateConflict && (
                    <div className="mt-1 flex items-start gap-1.5 text-[11.5px] text-amber-700">
                      <StatusDot tone="warn" />
                      <span>
                        Delivery {shortDate(line.deliveryDate)} · asked{' '}
                        {shortDate(request.requiredBy)}
                      </span>
                    </div>
                  )}
                  {line.requiresOverride && (
                    <div className="mt-1 text-[11.5px] leading-snug text-red-700">
                      Outside the margin floor — needs a manual override to quote at this discount.
                    </div>
                  )}
                </td>
                <td className="py-2.5 pr-3 text-right">
                  <Mono>{line.quantity}</Mono>
                </td>
                <td className="py-2.5 pr-3 text-right">
                  <Mono>{money(line.listPrice)}</Mono>
                </td>
                <td className="py-2.5 pr-3 text-right">
                  <Mono>{line.discountPct}%</Mono>
                </td>
                <td className="py-2.5 pr-3 text-right">
                  <Mono>{money(line.netUnitPrice)}</Mono>
                </td>
                <td className="py-2.5 pr-3 text-right">
                  <Mono>{money(line.lineTotal)}</Mono>
                </td>
                <td className="px-[18px] py-2.5 text-right">
                  {line.requiresOverride ? (
                    <Badge tone="bad">override</Badge>
                  ) : dateConflict ? (
                    <Badge tone="warn">date</Badge>
                  ) : (
                    <Badge tone="ok">resolved</Badge>
                  )}
                </td>
              </tr>
            )
          })}

          {unresolved.map((line, i) => (
            <tr key={`unresolved-${i}`} className="border-b border-slate-100 bg-red-50 align-top">
              <td className="px-[18px] py-2.5" colSpan={6}>
                <span className="text-slate-700">“{line.originalDescription}”</span>
                <div className="mt-1 text-[11.5px] leading-snug text-red-700">
                  {line.reason} Not quoted — the agent will not guess. Resolve before sending.
                </div>
              </td>
              <td className="px-[18px] py-2.5 text-right">
                <Badge tone="bad">unresolved</Badge>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="flex gap-6 border-t border-slate-200 px-[18px] py-4">
        <div className="flex-1 space-y-2">
          <Eyebrow>Notes</Eyebrow>
          {anyPolicyDiscount && (
            <Note tone="ok">Discounts on quoted lines are within policy — no override needed.</Note>
          )}
          {anyDateConflict && (
            <Note tone="warn">
              At least one line is delivered after the requested date — consider a split dispatch.
            </Note>
          )}
          {anyOverride && (
            <Note tone="bad">A line is below the margin floor and needs your override.</Note>
          )}
          {quote.warnings.map((w, i) => (
            <Note key={i} tone="neutral">
              {w}
            </Note>
          ))}
          {!anyPolicyDiscount && !anyDateConflict && !anyOverride && quote.warnings.length === 0 && (
            <p className="text-[12px] text-slate-400">Nothing flagged.</p>
          )}
        </div>

        <dl className="w-[264px] shrink-0 text-[12.5px]">
          <TotalRow label="Subtotal" value={money(quote.subtotal)} />
          <TotalRow label="Freight" value={money(quote.freight)} />
          <TotalRow label="GST 18%" value={money(quote.tax)} />
          <div className="mt-1 flex items-center justify-between border-t border-slate-200 pt-2 font-bold">
            <dt>Grand total</dt>
            <dd>
              <Mono>{money(quote.grandTotal)}</Mono>
            </dd>
          </div>
          <div className="flex items-center justify-between pt-1 text-slate-400">
            <dt>Valid until</dt>
            <dd>
              <Mono>{shortDate(quote.validUntil)}</Mono>
            </dd>
          </div>
        </dl>
      </div>

      <div className="flex items-center justify-end gap-2.5 border-t border-slate-200 bg-slate-50 px-[18px] py-3.5">
        {disabled && disabledNote && (
          <span className="mr-auto text-[11.5px] text-slate-400">{disabledNote}</span>
        )}
        <input
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Reason — required to reject"
          disabled={actionsDisabled}
          className="w-full max-w-[360px] rounded-md border border-slate-300 px-2.5 py-2 text-[12px] disabled:bg-slate-100"
        />
        <Button
          variant="danger"
          disabled={actionsDisabled || reason.trim().length === 0}
          onClick={() => onReject(reason.trim())}
        >
          Reject
        </Button>
        <Button variant="primary" disabled={actionsDisabled} onClick={onApprove}>
          {busy && <Spinner className="text-white" />}
          Approve &amp; send
        </Button>
      </div>
    </Card>
  )
}

function Note({ tone, children }: { tone: 'ok' | 'warn' | 'bad' | 'neutral'; children: ReactNode }) {
  const dot = tone === 'neutral' ? 'idle' : tone
  return (
    <div className="flex gap-2 text-[12px] leading-relaxed text-slate-600">
      <span className="mt-1.5">
        <StatusDot tone={dot} />
      </span>
      <span>{children}</span>
    </div>
  )
}

function TotalRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between py-1">
      <dt className="text-slate-500">{label}</dt>
      <dd>
        <Mono>{value}</Mono>
      </dd>
    </div>
  )
}
