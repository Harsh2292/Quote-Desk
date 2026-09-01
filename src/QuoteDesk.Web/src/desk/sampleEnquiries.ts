/**
 * "Paste one of these" — task 09's answer to a blank textarea being the only way to try the demo
 * cold. Each row ships a body *and* a sender together, because `POST /api/enquiries` falls back to
 * the signed-in Google user's own email when the sender box is left blank
 * (src/QuoteDesk.Api/Enquiries/EnquiryEndpoints.cs), which never matches a seeded customer — a body
 * alone (the old "Use the worked example" link) only ever resolves by a company name in the
 * signature, never by domain or WhatsApp number.
 *
 * Every claim below (customer name, tier, on-hand, the margin-floor override) was read back from the
 * running seeded database while writing this file, not derived from the seeder's source by hand —
 * docs/SESSION-LOG.md notes the seeder's index/tier off-by-one as a real trap for exactly that.
 */
export interface SampleEnquiry {
  id: string
  label: string
  why: string
  sender: string
  body: string
}

export const SAMPLE_ENQUIRIES: SampleEnquiry[] = [
  {
    id: 'worked-example',
    label: 'The worked example',
    why: 'Order history resolves "same as last time"; the spindle tape stays ambiguous; the belt is short on stock.',
    sender: 'kiran@shreejitextiles.com',
    body: `Hi Mehul bhai,
Need urgent quote —
250 nos of the 6203 bearings (same as last time)
40 mtr of the 25mm PU timing belt
12 pcs ring frame spindle tape, the thicker one

Delivery at our Sachin unit, need by 5th. Last time you gave 8% on bearings, please keep same.

Kiran — Shreeji Textiles`,
  },
  {
    id: 'unknown-sender',
    label: 'Unknown sender',
    why: 'No customer record matches — list price only, no tier discount, flagged for a human to verify.',
    sender: 'rfq@novafabrics.example',
    body: 'Need 50 pcs bearing 6203 asap, whats the rate',
  },
  {
    id: 'margin-floor',
    label: 'Margin floor',
    why: 'Om Textiles (Tier A) on a gear priced to trip the 10% margin floor — routed to approval as requires_override.',
    sender: 'purchase@omtextiles.com',
    body: 'Quotation needed: module 2 spur gear 40T x 100 nos.',
  },
  {
    id: 'short-stock',
    label: 'Short stock, big ask',
    why: 'Only 50 on hand against 500 requested, 5-day supplier lead time — the delivery date slips.',
    sender: 'stores@jaitextileindustries.com',
    body: 'Need 500 nos 6200 series bearing (2RS) for our next production run.',
  },
  {
    id: 'whatsapp-hinglish',
    label: 'WhatsApp, Hinglish',
    why: 'Sender resolves by WhatsApp number, not domain — Jai Mills, Tier C.',
    sender: '+91-9800000411',
    body: '20mm PU belt 15 mtr chahiye, kal tak mil jayega?',
  },
]
