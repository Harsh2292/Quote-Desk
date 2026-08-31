import { apiJson } from './client'
import type {
  EnquiryCreatedResponse,
  EnquiryDetailResponse,
  PasteEnquiryRequest,
  PendingApprovalSummary,
  QuoteDetailResponse,
  QuoteSummaryResponse,
} from './types'

const jsonBody = (value: unknown): RequestInit => ({
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(value),
})

export const createEnquiry = (body: PasteEnquiryRequest): Promise<EnquiryCreatedResponse> =>
  apiJson('/api/enquiries', jsonBody(body))

export const getEnquiry = (id: number, signal?: AbortSignal): Promise<EnquiryDetailResponse> =>
  apiJson(`/api/enquiries/${id}`, { signal })

export const listApprovals = (signal?: AbortSignal): Promise<PendingApprovalSummary[]> =>
  apiJson('/api/approvals', { signal })

export const listQuotes = (signal?: AbortSignal): Promise<QuoteSummaryResponse[]> =>
  apiJson('/api/quotes', { signal })

export const getQuote = (id: number, signal?: AbortSignal): Promise<QuoteDetailResponse> =>
  apiJson(`/api/quotes/${id}`, { signal })
