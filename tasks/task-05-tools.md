# Task 05 — Typed tools

**Session 2 · depends on: 02, 03**

## Goal

Seven tools the agent can call. Each takes a record and returns a record. No agent exists yet — these
are ordinary C# methods with tests.

## Stack for this task

`Microsoft.Extensions.AI` (`AIFunctionFactory`) · EF Core repositories from task 02

## What to build

| Tool | Signature | Write? |
|---|---|---|
| `resolve_customer` | `(string companyName, string senderId) -> CustomerMatch` | no |
| `search_catalog` | `(string query, string[] hints) -> CatalogMatch[]` | no |
| `get_customer_history` | `(int customerId, string? sku) -> PriorPurchase[]` | no |
| `check_stock` | `(string sku, int qty) -> StockResult` | no |
| `price_quote` | `(int customerId, QuoteLineRequest[] lines) -> PricedQuote` | no |
| `create_quote_draft` | `(int enquiryId, PricedQuote quote) -> QuoteId` | **gated** |
| `send_quote` | `(int quoteId) -> SendResult` | **gated** |

Rules:

- Every input and output is a `record` with `required` members. Never `string` in, never `object` out.
- Validation returns a typed "not found" or "ambiguous" result. Never throws for a normal miss — the
  model has to be able to reason about it.
- `search_catalog` returns candidates with a **confidence and a reason**, and an explicit ambiguous
  result when it cannot choose. It never picks arbitrarily.
- `price_quote` calls `QuoteDesk.Domain` and nothing else.
- **`CostPrice` and margin appear in no tool return type.** Enforce with a test that reflects over
  every tool result type and fails if either property name is present.
- Read queries use `AsNoTracking()`. No lazy loading anywhere.
- Two separate registries: `ReadToolRegistry` and `WriteToolRegistry`, as distinct objects.
- XML doc comments on each tool are written **for the model**, in business language: what it does,
  when to use it, what a miss looks like. Treat them as prompt engineering, not documentation.

## Acceptance criteria

- [ ] All seven tools implemented and registered via `AIFunctionFactory.Create`
- [ ] `get_customer_history` resolves "same as last time" against the seeded 2RS purchases
- [ ] `search_catalog` returns ambiguous for "the thicker one" with both variants listed
- [ ] Reflection test proves no cost or margin field is reachable from any tool result
- [ ] Test proves `ReadToolRegistry` contains zero write tools
- [ ] Unit tests on every tool's validation and miss paths
- [ ] No EF entity type escapes `QuoteDesk.Data`

## Out of scope

Agents, workflow, prompts, the API.

## Notes on completion
