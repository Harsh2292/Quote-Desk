/**
 * TypeScript mirrors of the C# response records the API returns. These change in the same commit as
 * their C# counterparts, the same rule `agentEvents.ts` follows for `AgentEvent`.
 *
 * Wire conventions (every Api serializer uses `JsonSerializerDefaults.Web`):
 *  - property names are camelCase
 *  - `decimal` is a JSON number (rupees, two decimal places) — kept as `number` here
 *  - `DateTimeOffset` is an ISO-8601 string with offset; `DateOnly` is a `"YYYY-MM-DD"` string
 *  - both are left as `string`; formatting for display happens at the edge, never by `new Date()` math
 */

import type { AgentEvent } from './agentEvents'

export type { AgentEvent }

// ── enquiries ────────────────────────────────────────────────────────────────

export interface PasteEnquiryRequest {
  body: string
  senderId?: string
}

export interface EnquiryCreatedResponse {
  enquiryId: number
  status: string
}

/** `GET /api/enquiries/{id}` — the transcript plus the replayed trace, both surviving a refresh. */
export interface EnquiryDetailResponse {
  id: number
  channel: string
  senderId: string
  rawBody: string
  receivedAt: string
  customerId: number | null
  status: string
  runStatus: RunStatus | null
  pendingApproval: ApprovalRequest | null
  trace: AgentEvent[] | null
}

/** `AgentRun.Status` — see `AgentRunRepository`. `null` when no run has been started for the enquiry. */
export type RunStatus =
  | 'running'
  | 'pending_approval'
  | 'completed'
  | 'rejected'
  | 'failed'

// ── approvals ────────────────────────────────────────────────────────────────

export interface PendingApprovalSummary {
  /** `AgentRun.Id`. The SSE `ApprovalRequiredEvent` carries the same value but as a string. */
  approvalId: number
  enquiryId: number
  createdAt: string
  request: ApprovalRequest
}

export interface ApprovalDecisionRequest {
  /** `"approve"` or `"reject"`. `"edit"` is rejected 400 by the Api until a later task defines it. */
  decision: 'approve' | 'reject'
  rejectionReason?: string
}

/**
 * The payload of `ApprovalRequiredEvent` and the `request` of `PendingApprovalSummary` — the one
 * shape the approval card renders, wherever it came from.
 */
export interface ApprovalRequest {
  enquiryId: number
  customerId: number | null
  customerName: string | null
  pricedQuote: PricedQuote
  unresolved: UnresolvedLine[]
  narration: string
  shipTo: string | null
  requiredBy: string | null
}

/**
 * A line the pricing engine produced. Note the key names: `quantity` / `netUnitPrice` here, but
 * `qty` / `unitPrice` on {@link QuoteLineResponse} for the same numbers — the two are deliberately
 * not one shared type.
 */
export interface PricedQuoteLine {
  sku: string
  quantity: number
  listPrice: number
  discountPct: number
  netUnitPrice: number
  lineTotal: number
  /** The line is outside policy (margin floor) and needs a human override. Shown red on the card. */
  requiresOverride: boolean
  dispatchDate: string | null
  deliveryDate: string | null
}

export interface PricedQuote {
  customerId: number | null
  lines: PricedQuoteLine[]
  subtotal: number
  freight: number
  tax: number
  grandTotal: number
  validUntil: string
  /** Free-text notes from `PricingTools`. Match on `requiresOverride` / `customerId`, not on text. */
  warnings: string[]
}

/** A line the Resolve agent would not guess at. Carries no SKU candidates — see the plan. */
export interface UnresolvedLine {
  originalDescription: string
  quantity: number
  reason: string
}

// ── quotes ───────────────────────────────────────────────────────────────────

export type QuoteStatus = 'draft' | 'approved' | 'sent' | 'rejected'

export interface QuoteSummaryResponse {
  id: number
  enquiryId: number
  number: string
  status: QuoteStatus
  customerId: number | null
  customerName: string | null
  total: number
  createdAt: string
  validUntil: string
}

export interface QuoteLineResponse {
  id: number
  sku: string
  qty: number
  unitPrice: number
  discountPct: number
  lineTotal: number
  requiresOverride: boolean
  dispatchDate: string | null
  deliveryDate: string | null
  note: string | null
}

export interface QuoteDetailResponse {
  id: number
  enquiryId: number
  number: string
  status: QuoteStatus
  subtotal: number
  freight: number
  tax: number
  total: number
  createdAt: string
  validUntil: string
  shipTo: string | null
  requiredBy: string | null
  approvedByUserId: number | null
  approvedAt: string | null
  sentAt: string | null
  lines: QuoteLineResponse[]
  /** Trace of the latest run for this quote's enquiry — replayed once the live SSE stream closed. */
  trace: AgentEvent[] | null
}

// ── guards ───────────────────────────────────────────────────────────────────

/**
 * `ApprovalRequiredEvent.payload` is typed `unknown` (the C# union declares `object`). Narrow it
 * before rendering the approval card off a live SSE frame.
 */
export function isApprovalRequest(payload: unknown): payload is ApprovalRequest {
  if (typeof payload !== 'object' || payload === null) return false
  const p = payload as Record<string, unknown>
  return (
    typeof p['enquiryId'] === 'number' &&
    typeof p['narration'] === 'string' &&
    typeof p['pricedQuote'] === 'object' &&
    p['pricedQuote'] !== null &&
    Array.isArray(p['unresolved'])
  )
}
