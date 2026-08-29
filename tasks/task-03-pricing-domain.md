# Task 03 — Pricing domain

**Session 1 · depends on: 02**

## Goal

Every number QuoteDesk will ever quote, computed in plain C# and proven correct. No LLM is involved
in this task at all — that is the point of doing it first.

## Stack for this task

Plain C#, **zero package references**. xUnit + FluentAssertions for the tests.

## What to build

In `QuoteDesk.Domain`:

| Type | Responsibility |
|---|---|
| `PricingEngine` | list price → slab discount → tier discount → line total → totals |
| `SlabDiscountPolicy` | quantity break lookup, inclusive lower bound |
| `MarginFloorPolicy` | flags a line whose net margin falls below the floor |
| `DeliveryDateCalculator` | on-hand vs lead time → dispatch and delivery dates |
| `QuoteTotals` | line totals, freight, GST, grand total |
| `Money` | the single rounding helper. Everything rounds through this. |

The rules are written out in `docs/DOMAIN.md`. Read that file before starting; if anything in it is
ambiguous, ask rather than guess, and write the answer back into it.

Hard constraints:

- No `DateTime.Now` or `DateTimeOffset.UtcNow` anywhere. Time is a parameter.
- No configuration reading, no logging, no DI, no I/O.
- Every method is a pure function.

## Acceptance criteria

- [x] `QuoteDesk.Domain.csproj` has zero `PackageReference` entries and zero `ProjectReference` entries
- [x] Unit tests cover every rule in `docs/DOMAIN.md`
- [x] Boundary cases tested: quantity exactly on a slab edge, margin exactly at the floor, zero
      quantity, unknown customer, delivery landing on a Sunday, delivery landing on a holiday
- [x] **The worked example in `docs/DOMAIN.md` reproduces exactly, as a single test** — 250 bearings
      at 8%, the belt shortfall, the 14% margin
- [x] Grep confirms no `DateTime.Now` / `.UtcNow` in the project
- [x] All money is `decimal` and rounds through `Money`

## Out of scope

Anything that touches the database, the model, or the network.

## Notes on completion

Built together with tasks 01 and 02 — see `docs/SESSION-LOG.md` for why. Built **before** task 02,
not after, despite this file's own "depends on: 02": `QuoteDesk.Domain` has zero references and does
zero I/O, so it never actually needed the schema. Writing the pricing engine first meant the task 02
seed could be *asserted* against (the margin-floor breach case especially) instead of hand-waved.

**What was built:** `Money` (the one rounding call site, round-half-up) · `SlabDiscountPolicy` with a
`DefaultLadder` (1+/0%, 50+/3%, 200+/6%, 500+/9%) · `TierDiscountPolicy` (A 4%, B 2%, C 0%, no tier
for an unknown customer) · `PricingEngine` (combines both, capped at 15%, computes net margin and
`RequiresOverride`) · `MarginFloorPolicy` (10% floor, "at or above" passes) · `DeliveryDateCalculator`
(next working day if in stock, else `+lead time`, then `+transit days`, rolling past Sundays and an
injected holiday set) · `FreightPolicy` (Local/Regional/National, waived above ₹50,000) ·
`QuoteTotalsCalculator` (18% GST on lines + freight). 35 tests, all passing, including a
`DomainPurityTests` class that greps the actual `.cs` files for `DateTime.Now`/`.UtcNow` rather than
trusting a one-time manual check, plus a `WorkedExampleTests` class reproducing the Shreeji Textiles
enquiry — 8% discount, 14% margin, and the belt missing its requested date, exactly.

**What surprised me:** `docs/DOMAIN.md` states the *shape* of every rule but almost none of the
concrete numbers — the slab ladder past the one fixed 200-unit rung, the freight amounts, the transit
days, the discount cap. All of that had to be invented to write a single test, so it's now written
back into `docs/DOMAIN.md` under "The numbers, filled in by task 03", explicitly marked as defaults
to change rather than a locked business rule. The one number that mattered most: **Local-zone transit
had to be exactly 1 day**, not 2, for the belt's delivery to land on a Sunday and roll to the Monday
the email describes — that specific boundary case only exists because the numbers were chosen to
make the worked example's dates fall out with no fudging, not the other way around.

**What the next task should know:** task 05's tools will call this code directly —
`price_quote` is `PricingEngine.PriceLine` plus `QuoteTotalsCalculator.Calculate`, and
`check_stock`/date logic is `DeliveryDateCalculator.Calculate`. Nothing here reads config or a
catalogue; the slab ladder for a real category comes from task 02's `PriceRules` table via
`IPriceRuleRepository`, not from `SlabDiscountPolicy.DefaultLadder` — that constant is only the
fallback for a category with no rules of its own.
