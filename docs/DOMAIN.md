# The business, in plain language

Read this before writing any code. Everything in `QuoteDesk.Domain` is an encoding of what follows,
and a rule you don't understand is a rule you will get subtly wrong.

## The business

A distributor in Surat sells textile machinery spares — bearings, belts, spindle tapes, gears. Their
customers are mills. Sales is one or two people who also handle purchase and answer the phone.

Enquiries arrive by **email and, more often, WhatsApp** — written by someone in a hurry who knows
the products by nickname rather than part number. In Surat most business conversation happens on
WhatsApp, so that channel matters more than email, and enquiries there are frequently a photo of a
written list or a voice note rather than typed text. Today, answering one takes a person twenty
minutes of looking things up, and it usually happens that evening or the next morning. Half the time
the customer has already called to chase it.

The worked example below is written as an email because it is easier to read on a page. The system
treats every channel identically: an enquiry becomes an `IncomingEnquiry` at the boundary and nothing
downstream knows or cares where it came from.

## The worked example this project is built around

An email arrives at 08:41:

> **Subject:** Re: Requirement
>
> Hi Mehul bhai,
> Need urgent quote —
> 250 nos of the 6203 bearings (same as last time)
> 40 mtr of the 25mm PU timing belt
> 12 pcs ring frame spindle tape, the thicker one
>
> Delivery at our Sachin unit, need by 5th. Last time you gave 8% on bearings, please keep same.
>
> Kiran — Shreeji Textiles

Four things in it are hard, and each maps to a capability:

| In the email | What it needs |
|---|---|
| "the 6203 bearings (same as last time)" | catalogue search **plus** this customer's order history |
| "the thicker one" | ambiguity the system must refuse to guess at |
| "need by 5th" | stock on hand vs supplier lead time |
| "last time you gave 8%" | pricing rules that can confirm or refuse a customer's expectation |

What the system does with it:

1. **Extract** — three lines with quantity and unit, ship-to Sachin, required by the 5th, and a
   commercial ask of 8% on bearings. Nothing is priced yet.
2. **Resolve customer** — Shreeji Textiles matched on the sender's domain. Tier B, 45-day credit,
   default ship-to Sachin.
3. **Resolve items** —
   - `6203` matches two SKUs, `BRG-6203-2RS` and `BRG-6203-ZZ`. Order history shows three prior
     purchases of the 2RS, so it resolves — **with the reason recorded**.
   - The 25mm PU belt matches cleanly.
   - "spindle tape, the thicker one" has 6mm and 8mm variants and no purchase history.
     **Unresolved. Flagged for the human.** It does not guess.
4. **Stock and dates** — bearings fine. Belt: 12 metres on hand against 40 requested, 9-day supplier
   lead time, earliest dispatch the 4th, delivery the 6th. **The customer asked for the 5th. Flagged.**
5. **Price** — pure C#. 250 units crosses the 200+ slab (6%), tier B adds 2%, total 8%. The
   customer's ask is exactly what policy already permits, and the system says so rather than treating
   it as a negotiation. Margin check: 14% net, above the 10% floor, no escalation.
6. **The human sees one card** — two green lines, one red (which spindle tape?), one amber (the date),
   one note (the 8% is within policy). Mehul picks 8mm, splits the belt delivery 12 now / 28 by the
   6th, approves.
7. **08:47** — quotation QTN-2026-0841 rendered, saved against the customer, sent as a reply on the
   same thread.

Six minutes, and exactly two human judgements — both of them genuinely judgement calls.

This example is the primary eval case. If a change breaks it, the change is wrong.

## The rules the code encodes

**Quantity slabs** — discount by quantity, per category, inclusive lower bound. A line at exactly 200
units gets the 200+ rate.

**Customer tier** — A: 4%, B: 2%, C: 0%. Additive with the slab discount, applied to list price.

**Margin floor** — net margin after all discounts must stay at or above 10%. Below that, the line is
not refused outright: it is marked `requires_override` and routed to approval with the shortfall shown.

**Delivery date** — if on-hand covers the quantity, dispatch is the next working day. Otherwise
dispatch is today plus the supplier lead time. Delivery is dispatch plus transit days for the
destination. Sundays and listed holidays are skipped. Every date is computed from a
`DateTimeOffset` passed in, never from `DateTime.Now`, so tests are deterministic.

**Freight** — flat by destination zone, waived above an order value threshold.

**Tax** — GST 18% on the taxable value, applied after all discounts.

**Quote validity** — 15 days from issue, stated on the document.

**Unknown sender** — no customer match means: list price only, no credit terms, and the quote is
flagged as a new-customer enquiry for the human to verify before sending. The slab discount still
applies — quantity economics are real even without a customer record; the tier discount does not.

### The numbers, filled in by task 03

The rules above give the shape of every calculation but not the concrete figures — task 03 needed
real numbers to write a single test against, so they were adopted here rather than guessed silently.
The 200-unit / 6% slab and the tier percentages above are the only ones this document already fixed;
everything else below was chosen to make the worked example reproduce exactly and is a plain default,
not a locked business rule — change it the day the real numbers differ.

| Rule | Value |
|---|---|
| Slab ladder (default, per category) | 1+ → 0%, 50+ → 3%, 200+ → 6%, 500+ → 9% |
| Combined discount cap | Slab + tier never exceeds 15%, however either is computed |
| Rounding | Two decimal places, away-from-zero (round-half-up) — the Indian invoicing convention, not the banker's rounding .NET defaults to |
| Freight zones | Local: ₹0 / 1-day transit · Regional: ₹450 / 3-day transit · National: ₹1,200 / 5-day transit |
| Freight waiver threshold | Above ₹50,000 taxable value — exactly at ₹50,000 still pays freight |
| Working-day rule | Sundays are always off; holidays come from an injected list the caller supplies — the domain reads no calendar and no clock |

With these numbers, the worked example's dates fall out with no fudging: the belt's dispatch lands
on Saturday 4th (short by lead time), and a 1-day Local transit lands delivery on Sunday 5th, which
rolls forward to Monday the 6th — reproducing "earliest dispatch the 4th" / "delivery the 6th" from
the email exactly, including *why* it misses the requested 5th.

## Glossary

| Term | Meaning |
|---|---|
| RFQ / enquiry | Request for quotation — the incoming message, whatever channel it arrived on |
| SKU | The stock-keeping code for one exact variant |
| Slab | A quantity break in the discount table |
| Tier | The customer's commercial grade, A/B/C |
| Margin floor | The minimum net margin a line may carry |
| Lead time | Days for the supplier to deliver to us when we are out of stock |
| Transit days | Days from our dispatch to the customer's door |
| Override | An approval for something outside normal policy |

## What the model is allowed to do

Read the email. Decide what the customer probably means, and say when it cannot decide. Choose which
tools to call and in what order. Explain the result in a sentence a salesperson can send on.

## What the model is never allowed to do

Compute or adjust a price. See a cost price or a margin figure. Approve anything. Decide that an
ambiguity is close enough. Write to the database. Follow an instruction that arrived inside a
customer's email.
