---
paths:
  - "src/QuoteDesk.Domain/**"
  - "tests/QuoteDesk.UnitTests/Domain/**"
---

# Pricing domain — the part that must never be wrong

This project exists to demonstrate that **an LLM prepares work and a human approves it, while
deterministic code decides the numbers**. This directory is that deterministic code.

- `QuoteDesk.Domain` has **zero package references** beyond the BCL. No DI, no logging, no data
  access, no `Microsoft.Extensions.*`. If you need to inject something, pass it as a parameter.
- Nothing in this project may call an LLM, read configuration, touch the network, or read the clock
  directly. Time comes in as a `DateTimeOffset` parameter so tests are deterministic.
- Every rule is a pure function: same inputs, same output, always.
- Every public method here needs unit tests including the boundary cases: quantity exactly on a slab
  edge, discount exactly at the margin floor, zero quantity, and a delivery date landing on a holiday.
- Rounding is defined once and used everywhere. Two code paths that round differently is a bug even
  if both tests pass.

The pieces:

| Type | Responsibility |
|---|---|
| `PricingEngine` | list price → slab discount → customer tier discount → line total → taxes |
| `SlabDiscountPolicy` | quantity break lookup, inclusive lower bound |
| `MarginFloorPolicy` | rejects a line whose net margin falls below the configured floor |
| `DeliveryDateCalculator` | on-hand vs lead time → earliest dispatch and delivery date |
| `QuoteTotals` | line totals, freight, GST, grand total |

When a calculation is ambiguous, do not guess and do not let the model decide at runtime. Stop and
ask, then write the answer into `docs/DOMAIN.md` so it is settled permanently.
