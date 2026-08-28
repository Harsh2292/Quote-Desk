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

- [ ] `QuoteDesk.Domain.csproj` has zero `PackageReference` entries and zero `ProjectReference` entries
- [ ] Unit tests cover every rule in `docs/DOMAIN.md`
- [ ] Boundary cases tested: quantity exactly on a slab edge, margin exactly at the floor, zero
      quantity, unknown customer, delivery landing on a Sunday, delivery landing on a holiday
- [ ] **The worked example in `docs/DOMAIN.md` reproduces exactly, as a single test** — 250 bearings
      at 8%, the belt shortfall, the 14% margin
- [ ] Grep confirms no `DateTime.Now` / `.UtcNow` in the project
- [ ] All money is `decimal` and rounds through `Money`

## Out of scope

Anything that touches the database, the model, or the network.

## Notes on completion
