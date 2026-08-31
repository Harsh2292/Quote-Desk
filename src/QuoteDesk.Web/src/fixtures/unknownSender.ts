import type { AgentEvent } from '../api/agentEvents'

/**
 * A recorded run where the sender matches no customer record — list price and the quantity slab
 * still apply, but no tier discount and no credit terms, and the quote is flagged for a human to
 * verify the customer before it goes out.
 */
export const unknownSenderRun: AgentEvent[] = [
  { type: 'stage', stage: 'extract', at: '2026-08-31T11:30:00.000+05:30' },
  { type: 'stage', stage: 'resolve', at: '2026-08-31T11:30:01.100+05:30' },
  {
    type: 'tool_start',
    name: 'resolve_customer',
    args: { companyName: 'Nova Fabrics', senderId: 'nova.fabrics.rfq@gmail.com' },
    at: '2026-08-31T11:30:01.110+05:30',
  },
  {
    type: 'tool_end',
    name: 'resolve_customer',
    ms: 280,
    ok: true,
    result: { customerId: null, matchedOn: null, reason: 'No customer matches this name or sender address.' },
  },
  {
    type: 'tool_start',
    name: 'search_catalog',
    args: { queries: [{ query: '6203 2RS bearing', hints: [] }, { query: 'V-belt B-section 1200mm', hints: [] }] },
    at: '2026-08-31T11:30:01.600+05:30',
  },
  {
    type: 'tool_end',
    name: 'search_catalog',
    ms: 760,
    ok: true,
    result: [
      { query: '6203 2RS bearing', outcome: 'resolved', resolvedSku: 'BRG-6203-2RS', candidates: [], reason: 'Exact match.' },
      { query: 'V-belt B-section 1200mm', outcome: 'resolved', resolvedSku: 'BELT-V-B1200', candidates: [], reason: 'Section and length stated explicitly.' },
    ],
  },
  {
    type: 'tool_start',
    name: 'check_stock',
    args: { sku: 'BRG-6203-2RS', qty: 50 },
    at: '2026-08-31T11:30:02.500+05:30',
  },
  {
    type: 'tool_end',
    name: 'check_stock',
    ms: 240,
    ok: true,
    result: { sku: 'BRG-6203-2RS', onHand: 900, leadTimeDays: 5, dispatchDate: '2026-09-01', shortBy: 0 },
  },
  { type: 'stage', stage: 'price', at: '2026-08-31T11:30:02.900+05:30' },
  {
    type: 'approval_required',
    approvalId: 'replay-unknown-sender',
    action: 'approve_quote',
    payload: {
      enquiryId: 11,
      customerId: null,
      customerName: null,
      pricedQuote: {
        customerId: null,
        lines: [
          {
            sku: 'BRG-6203-2RS',
            quantity: 50,
            listPrice: 190.0,
            discountPct: 3,
            netUnitPrice: 184.3,
            lineTotal: 9215.0,
            requiresOverride: false,
            dispatchDate: '2026-09-01',
            deliveryDate: '2026-09-03',
          },
          {
            sku: 'BELT-V-B1200',
            quantity: 20,
            listPrice: 240.0,
            discountPct: 0,
            netUnitPrice: 240.0,
            lineTotal: 4800.0,
            requiresOverride: false,
            dispatchDate: '2026-09-01',
            deliveryDate: '2026-09-03',
          },
        ],
        subtotal: 14015.0,
        freight: 450.0,
        tax: 2603.7,
        grandTotal: 17068.7,
        validUntil: '2026-09-21T00:00:00+05:30',
        warnings: [
          'Sender did not match a known customer — list price and quantity discount only, no tier discount or credit terms. Flag for verification before sending.',
        ],
      },
      unresolved: [],
      narration:
        'The sender does not match any customer on file. Both lines were priced at list with the quantity slab only — no tier discount, no credit terms. Verify who this is before the quote is sent.',
      shipTo: null,
      requiredBy: null,
    },
  },
]
