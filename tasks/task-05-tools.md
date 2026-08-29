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

- [x] All seven tools implemented and registered via `AIFunctionFactory.Create`
- [x] `get_customer_history` resolves "same as last time" against the seeded 2RS purchases
- [x] `search_catalog` returns ambiguous for "the thicker one" with both variants listed
- [x] Reflection test proves no cost or margin field is reachable from any tool result
- [x] Test proves `ReadToolRegistry` contains zero write tools
- [x] Unit tests on every tool's validation and miss paths
- [x] No EF entity type escapes `QuoteDesk.Data`

## Out of scope

Agents, workflow, prompts, the API.

## Notes on completion

Built as planned, plus a small migration: `AddQuoteDetails` adds `Quotes.Freight/ValidUntil/ShipTo/
RequiredBy` and `QuoteLines.RequiresOverride/DispatchDate/DeliveryDate` — both tables were still
empty, so the change was free. `IQuoteRepository`/`QuoteRepository` joined the other five read
repositories in `QuoteDesk.Data`. `docs/SPEC.md` §6 and §7 were corrected in the same commit:
`search_catalog` returns `CatalogSearchResult` (an array cannot say "ambiguous"), `price_quote` takes
`int? customerId` (docs/DOMAIN.md's "Unknown sender" rule needs somewhere to hang), and
`create_quote_draft` returns `QuoteDraftResult` (a typed miss, matching every other tool).

**`MarginShortfallPct` is deliberately not carried through to any tool result or stored column** —
only `RequiresOverride` (a bool) survives from `QuoteDesk.Domain.PricedLine` into
`Agents.Tools.Results.PricedQuoteLine` and the `QuoteLines` table. It is a margin figure, and
docs/DOMAIN.md says the model may never see one; the approval card only needs to know a line needs
an override, not by how much. `docs/SPEC.md` §6 records this as a deliberate omission, not an
oversight, so a future session doesn't "fix" it by threading margin data toward the model.

**`[AIFunctionName]` was not used** — despite being the natural way to give a tool its snake_case
name, `Microsoft.Extensions.AI.Abstractions` 10.9.0 marks that attribute `[Experimental("MEAI001")]`,
which `-warnaserror` turns into a build failure. `ReadToolRegistry`/`WriteToolRegistry` instead pass
`AIFunctionFactoryOptions { Name = "..." }` to `AIFunctionFactory.Create`, which is not experimental
and produces the identical result. Descriptions still come from `[System.ComponentModel.Description]`
on each method and parameter — confirmed against the installed package's XML docs (not read by
anything at runtime, contrary to the task file's original text, since `GenerateDocumentationFile` is
`false` in this repo; `AIFunctionFactory` reads `DescriptionAttribute` via reflection instead, which
needs no XML doc file at all).

**`search_catalog`'s scoring** unions the repository's single-substring `SearchAsync` across every
token of `query` and every hint (not just `query` verbatim, which routinely matches nothing — e.g.
"6203 bearings" is not a contiguous substring of "6203 Series Ball Bearing (2RS)"), then scores each
candidate by the fraction of tokens it matches. This is what makes "ring frame spindle tape" +
["thicker"] come back ambiguous across all eight seeded thickness variants (docs/DOMAIN.md's case)
without hardcoding anything about spindle tapes specifically — the same token-overlap math also
resolves "25mm PU timing belt" to one SKU and "6203 bearings" to an ambiguous set of four suffix
variants (2RS/ZZ/RS/2Z), which is correct: only order history (task 06) can break that tie, matching
how the worked example actually resolves it.

**Found and fixed while writing the integration tests:** the same fixture race from task 04 recurred
— `RepositoryTests` and the new `ToolsIntegrationTests` each declared their own
`IClassFixture<RepositoryFixture>` against the fixed `QuoteDeskTests_Repository` database name. Fixed
the same way: both now share one `[Collection("Repository")]`. Any future test class using
`RepositoryFixture` must join this collection too.

120/120 tests passing (91 unit + 29 integration, combined with task 04's totals), 0 warnings under
`-warnaserror`. `npm run build` still passes (untouched by this task).
