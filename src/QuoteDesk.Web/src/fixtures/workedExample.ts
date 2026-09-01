import type { AgentEvent } from '../api/agentEvents'

/**
 * A recorded run of `docs/DOMAIN.md`'s worked example — Shreeji Textiles, three lines, one
 * ambiguous, one date conflict, an 8% ask that is within policy. Replayed when the live provider is
 * rate limited so a visitor still sees the whole pipeline. Ends at the approval gate, exactly as the
 * real stream does when the run suspends.
 */
export const workedExampleRun: AgentEvent[] = [
  { type: 'stage', stage: 'extract', at: '2026-08-31T08:41:02.100+05:30' },
  { type: 'stage', stage: 'resolve', at: '2026-08-31T08:41:03.400+05:30' },
  {
    type: 'tool_start',
    name: 'resolve_customer',
    args: { companyName: 'Shreeji Textiles', senderId: 'kiran@shreejitextiles.co.in' },
    at: '2026-08-31T08:41:03.410+05:30',
  },
  {
    type: 'tool_end',
    name: 'resolve_customer',
    ms: 340,
    ok: true,
    result: { customerId: 4, name: 'Shreeji Textiles', tier: 'B', creditDays: 45, matchedOn: 'email_domain' },
  },
  {
    type: 'tool_start',
    name: 'search_catalog',
    args: {
      queries: [
        { query: '6203 bearing', hints: ['same as last time'] },
        { query: '25mm PU timing belt', hints: [] },
        { query: 'ring frame spindle tape', hints: ['the thicker one'] },
      ],
    },
    at: '2026-08-31T08:41:03.900+05:30',
  },
  {
    type: 'tool_end',
    name: 'search_catalog',
    ms: 1080,
    ok: true,
    result: [
      { query: '6203 bearing', outcome: 'resolved', resolvedSku: 'BRG-6203-2RS', candidates: [], reason: 'Three prior orders of the 2RS variant.' },
      { query: '25mm PU timing belt', outcome: 'resolved', resolvedSku: 'BELT-PU-25', candidates: [], reason: 'Exact match on name and width.' },
      {
        query: 'ring frame spindle tape',
        outcome: 'ambiguous',
        candidates: [
          { sku: 'SPT-TAPE-06', name: 'Ring frame spindle tape, 6 mm', confidence: 0.55, reason: '"thicker" is relative — 6 mm is the thinner of two.' },
          { sku: 'SPT-TAPE-08', name: 'Ring frame spindle tape, 8 mm', confidence: 0.55, reason: '"thicker" is relative — 8 mm is the thicker of two.' },
        ],
        reason: 'Two variants match and there is no purchase history to choose between them.',
      },
    ],
  },
  {
    type: 'tool_start',
    name: 'get_customer_history',
    args: { customerId: 4, sku: 'BRG-6203-2RS' },
    at: '2026-08-31T08:41:05.100+05:30',
  },
  {
    type: 'tool_end',
    name: 'get_customer_history',
    ms: 210,
    ok: true,
    result: [
      { sku: 'BRG-6203-2RS', qty: 200, unitPrice: 174.8, orderedAt: '2026-05-12' },
      { sku: 'BRG-6203-2RS', qty: 150, unitPrice: 174.8, orderedAt: '2026-02-03' },
      { sku: 'BRG-6203-2RS', qty: 300, unitPrice: 176.7, orderedAt: '2025-11-19' },
    ],
  },
  {
    type: 'tool_start',
    name: 'check_stock',
    args: { sku: 'BELT-PU-25', qty: 40 },
    at: '2026-08-31T08:41:05.500+05:30',
  },
  {
    type: 'tool_end',
    name: 'check_stock',
    ms: 440,
    ok: true,
    result: { sku: 'BELT-PU-25', onHand: 12, leadTimeDays: 9, dispatchDate: '2026-09-04', shortBy: 28 },
  },
  { type: 'stage', stage: 'price', at: '2026-08-31T08:41:06.000+05:30' },
  {
    type: 'approval_required',
    approvalId: 'replay-worked-example',
    action: 'approve_quote',
    payload: {
      enquiryId: 4,
      customerId: 4,
      customerName: 'Shreeji Textiles',
      pricedQuote: {
        customerId: 4,
        lines: [
          {
            sku: 'BRG-6203-2RS',
            quantity: 250,
            listPrice: 190.0,
            discountPct: 0.08,
            netUnitPrice: 174.8,
            lineTotal: 43700.0,
            requiresOverride: false,
            dispatchDate: '2026-09-01',
            deliveryDate: '2026-09-02',
          },
          {
            sku: 'BELT-PU-25',
            quantity: 40,
            listPrice: 120.0,
            discountPct: 0.02,
            netUnitPrice: 117.6,
            lineTotal: 4704.0,
            requiresOverride: false,
            dispatchDate: '2026-09-01',
            deliveryDate: '2026-09-06',
          },
        ],
        subtotal: 48404.0,
        freight: 0.0,
        tax: 8712.72,
        grandTotal: 57116.72,
        validUntil: '2026-09-21T00:00:00+05:30',
        warnings: [],
      },
      unresolved: [
        {
          originalDescription: 'ring frame spindle tape, the thicker one',
          quantity: 12,
          reason: 'Two variants match — 6 mm and 8 mm — and there is no purchase history to choose between them.',
        },
      ],
      narration:
        'Shreeji Textiles matched on their email domain — Tier B, 45-day credit. The 6203 bearing resolves to BRG-6203-2RS from three prior orders. The 25 mm PU timing belt matches cleanly, but only 12 m are in stock against 40, so the balance ships on a 9-day lead and delivery lands 6 Sep — one day past the requested 5th. The spindle tape has 6 mm and 8 mm variants with no history to choose between, so it is left for you. The requested 8% on bearings is exactly the 200+ slab (6%) plus Tier B (2%), so no override is needed.',
      shipTo: 'Sachin unit',
      requiredBy: '2026-09-05',
    },
  },
]
