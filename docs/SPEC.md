# QuoteDesk — specification

The contract. When the code and this document disagree, one of them is wrong — fix it deliberately,
in the same commit. Task-level detail lives in `tasks/`; this file is the shape of the whole thing.

It covers what is being built now. Deployment mechanics live in task 09, channels in task 10,
telemetry and evals in task 11 — written when those tasks are reached, not three weeks early.

## 1. What it is

A customer sends a messy enquiry — by paste, email or WhatsApp — to a distributor of textile
machinery spares. QuoteDesk reads it, resolves what the customer actually means, checks stock and
lead time, prices the lines against the company's own rules, and presents a complete draft quotation
to a salesperson who approves, edits or rejects it. Only then is a quote created and sent.

Enquiry in, quotation out. That loop is the whole product.

## 2. Autonomous or predefined?

**A fixed pipeline with one autonomous stage.** This is the most interesting thing about the
architecture and you should be able to state it precisely.

```
Extract → Resolve → Price → Approve → Create → Send
```

The sequence never reorders, never skips, never stops early. That part is a state machine, written in
code. **Inside `Resolve`, the agent is genuinely autonomous**: it chooses which tools to call, in
what order, and how many times. Six calls for a messy enquiry, two for a clean one — nobody scripted
that. `Price` is pure C#. `Approve` is a human.

The principle: **the model is used only where the input is genuinely ambiguous. Everything with
consequences is code.**

Fully autonomous was rejected — unpredictable cost and latency, hard to eval, and it can silently
skip a step, which is fatal for a document that becomes a commercial commitment. Fully deterministic
was rejected because then it cannot read "the thicker one" or "same as last time", and that ambiguity
is the entire problem.

Microsoft Agent Framework Workflows exist for exactly this shape — a deterministic graph with
autonomous nodes — so the framework and the architecture fit each other.

Deliberately absent: **no vector database.** The data is structured; embeddings would be the wrong
tool. Say that out loud — it is a better answer than having used one.

## 3. Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 |
| API | ASP.NET Core Minimal APIs |
| Agent layer | `Microsoft.Agents.AI`, `.OpenAI`, `.Workflows` |
| Data | **EF Core** + SQL Server (Azure SQL in prod) |
| Intake | MailKit (IMAP) · Twilio WhatsApp sandbox (webhook) |
| Logging | Serilog, structured, correlation id per enquiry |
| Telemetry | OpenTelemetry → Application Insights |
| Frontend | React 19 + Vite + TypeScript + Tailwind, plain `fetch` |
| Streaming | Server-Sent Events |
| Auth | **Google identity, own JWT.** React gets a Google ID token from Google Identity Services (`@react-oauth/google`) and posts it to `POST /api/auth/google`; the Api verifies it against Google (`Google.Apis.Auth`) and mints a short-lived bearer JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`), checked on every route by a fallback authorization policy. Google is still the only identity provider and the app still stores no password — the one addition beyond task 01's plan is a `Users` table, auto-provisioned on first sign-in (§6), needed because a bearer token has to name *someone* server-side to check `role` against and to attribute `Quotes.ApprovedByUserId` to. Built ahead of schedule, before task 04, specifically so the fallback policy protects every endpoint from the moment it is written — see docs/SESSION-LOG.md. |
| Tests | xUnit + FluentAssertions, in `tests/` |
| CI/CD | GitHub Actions → Azure Container Apps + Static Web Apps |

As of Aug 2026 `Microsoft.Agents.AI` was at 1.19.0 and `Microsoft.Agents.AI.OpenAI` at 1.5.0. **Do
not hardcode versions from this document** — run `dotnet add package`, take what NuGet resolves, and
record the resolved versions back here.

**Resolved in task 01** (.NET 10.0.302 SDK, EF Core CLI 10.0.11):

| Package | Version |
|---|---|
| `Serilog.AspNetCore` | 10.0.0 |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.11 |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.11 |
| `AspNetCore.HealthChecks.SqlServer` | 9.0.0 |
| `FluentAssertions` | 7.2.0 (pinned — v8 moved to a paid Xceed licence; 7.2.0 is the last Apache-2.0 release) |
| `@tailwindcss/vite` | 4.3.3 |
| React | 19.2.8 |
| Vite | 8.2.2 |

**Resolved for the Google-identity/JWT auth work (done ahead of schedule, before task 04):**

| Package | Version |
|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.11 |
| `Google.Apis.Auth` | 1.76.0 |
| `Microsoft.AspNetCore.Mvc.Testing` (IntegrationTests only) | 10.0.11 |
| `@react-oauth/google` | see `src/QuoteDesk.Web/package.json` |

`Microsoft.AspNetCore.OpenApi` was deliberately **not** added to `QuoteDesk.Api`: the version NuGet
resolved for .NET 10 (2.0.0) pulls in a `Microsoft.OpenApi` with a known high-severity advisory,
which fails `-warnaserror`'s `NU1903` check. Task 01 needs no Swagger UI, so the package was dropped
rather than suppressed; revisit if a later task genuinely needs OpenAPI generation.

**Resolved for task 05's tools:** `Microsoft.Extensions.AI.Abstractions` **10.9.0**, added to
`QuoteDesk.Agents` only — `AIFunctionFactory` and `AIFunction` live there, and that is all task 05
needs. The full `Microsoft.Extensions.AI` package (an `IChatClient` and friends) is deferred to
task 06, when an actual chat client is wired up. Tool names are set via
`AIFunctionFactoryOptions.Name` rather than `[AIFunctionName]`, since that attribute is marked
`[Experimental("MEAI001")]` in this version and `-warnaserror` treats it as a build error; the
options-based name is not experimental and produces an identical result.

**Resolved for task 06's agent layer:** `Microsoft.Agents.AI` **1.19.0**, `Microsoft.Agents.AI.Workflows`
**1.19.0**, `Microsoft.Extensions.AI.OpenAI` **10.7.0** — all added to `QuoteDesk.Agents`.
**`Microsoft.Agents.AI.OpenAI` was deliberately not added**, despite being named in the original task
file: its only purpose is `OpenAI.Chat.ChatClient.AsAIAgent()`; building the agent from a plain
`IChatClient` instead (`chatClient.GetChatClient(model).AsIChatClient()`, then
`Microsoft.Extensions.AI.ChatClientExtensions.AsAIAgent(IChatClient, ...)`) is what makes the
stubbed-`IChatClient` integration-test requirement clean, and it also avoids pinning
`Microsoft.Extensions.AI.OpenAI` back to 10.6.0. Every API used — `ChatClientAgent`'s automatic
`FunctionInvokingChatClient` wrapping, `MaximumIterationsPerRequest`'s graceful (non-throwing)
termination, `AgentResponse.Usage`'s cumulative-per-call semantics, `RequestPort` wired with plain
`AddEdge` calls into an arbitrary downstream node, and a resumed run republishing its pending
`RequestInfoEvent` — was confirmed by decompiling the installed 1.19.0 assemblies (`ilspycmd`), not
recalled or guessed; see `tasks/task-06-agents-workflow.md`'s Notes on completion for the full list.

## 4. LLM provider — free, and swappable

**Resolved differently for each profile, since the `thought_signature` correction below — the
`gemini` profile no longer speaks the OpenAI wire protocol.** `github` still does:

```csharp
// "github" profile — unchanged, OpenAI-compatible.
var options = new OpenAIClientOptions { Endpoint = new Uri(cfg["Llm:Endpoint"]!) };
var chat = new OpenAIClient(new ApiKeyCredential(cfg["Llm:ApiKey"]!), options)
    .GetChatClient(cfg["Llm:Model"]!);

// "gemini" profile — Google's own native SDK, since Gemini's OpenAI-compatibility endpoint cannot
// carry the thought_signature a multi-turn tool call needs (see the correction below).
IChatClient chat = new Google.GenAI.Client(apiKey: cfg["Llm:ApiKey"]!).AsIChatClient(cfg["Llm:Model"]!);

AIAgent agent = chat.AsAIAgent(instructions: ..., name: ...);
```

`QuoteDesk.Agents.Llm.ChatClientFactory.Create` is the one place this branch lives, selected by
`LlmOptions.Provider` (`"gemini"` default, `"github"` fallback) — see that file's remarks for the full
reasoning.

| Profile | Client | Notes |
|---|---|---|
| **`gemini` (default)** | `Google.GenAI.Client` (native SDK, NuGet `Google.GenAI` 1.20.0) | Free tier, low daily cap per model (20 requests/day for `gemini-3.6-flash` on a fresh key — see below), and it reads images, so one key covers every channel. **Tool calls now work correctly, multi-turn — see the resolved correction below.** |
| `github` (fallback) | `OpenAIClient` pointed at `https://models.github.ai/inference` | Free with a GitHub PAT scoped `models:read`. Real OpenAI models, so tool calling behaves exactly as documented — useful as a control. But ~50 requests/day and an ~8K input cap, so it cannot carry the demo or the evals. |

**Pinned model: `gemini-3.6-flash`.** `gemini-2.5-flash` — the id this document originally assumed —
returns `404` for new keys ("no longer available to new users"); Google's own error names
`gemini-3.6-flash` as the replacement. Never use `gemini-flash-latest` or any other alias: it moves
under you and breaks eval reproducibility.

**Verified 2026-08-29 — task 00 spike, against `gemini-3.6-flash`:**

- **Non-streaming tool calls: works end to end** *for a single raw round trip* — `CompleteChatAsync`
  calls the tool, the follow-up request with the tool result gets a correct final answer. **This claim
  turned out to be too narrow for the real pipeline — see below.**
- **Streaming tool calls: broken, provider-side, not fixable through the OpenAI-compatible endpoint.**
  The tool call itself surfaces correctly over `CompleteChatStreamingAsync`. Submitting the result on
  the *next* streaming turn fails with `400 INVALID_ARGUMENT`: *"Function call is missing a
  `thought_signature` in functionCall parts... required for tools to work correctly."* Gemini's 3.x
  "thinking" models attach a `thought_signature` to every function-call part and require it echoed
  back; that field has no home in the standard OpenAI wire schema, so the `OpenAI` .NET SDK's
  `ChatToolCall` cannot carry it. This is a real protocol gap between Gemini's thinking models and
  OpenAI-compatibility clients, not a bug in this codebase — confirmed by reproducing the same request
  with raw `curl`.

**Found 2026-08-30, task 07's first live run of the real pipeline: the gap above is not confined to
streaming.** The task 00 spike's non-streaming claim was verified against a single hand-rolled round
trip, not against the real `Microsoft.Agents.AI` agent (`ChatClientAgent` wrapping
`FunctionInvokingChatClient`) that `ResolveExecutor` actually runs. Reproduced live: Extract succeeds,
Resolve's first tool call (`resolve_customer`) executes and returns a real result, and the *next*
non-streaming turn — the one submitting that tool result back to the model — fails with the identical
`400 INVALID_ARGUMENT thought_signature` error the streaming path already had. The OpenAI wire schema
has no field for it regardless of streaming or not, so `FunctionInvokingChatClient`'s non-streaming
loop hits the same missing-field problem.

**Resolved 2026-08-30 — adopted Google's official `Google.GenAI` .NET SDK for the `gemini` profile.**
Researched two alternatives Harsh proposed first: OpenRouter (confirmed **no fix** — multiple
independent reports of the identical error through OpenRouter with Gemini 3 models; it is also an
OpenAI-compatible shim with the same structural gap) and Google's own native SDK (confirmed **fixes
it**). The mechanism, verified by reading `Google.GenAI`'s own source and decompiling this project's
exact installed `Microsoft.Extensions.AI` `FunctionInvokingChatClient`: the adapter appends the raw
`thought_signature` as a sibling `TextReasoningContent { ProtectedData = <base64> }` — a standard
`Microsoft.Extensions.AI` member — immediately after the `FunctionCallContent` it belongs to, and
reattaches it when building the next turn; `FunctionInvokingChatClient` never strips that sibling item,
so it survives untouched through the exact loop `ResolveExecutor` runs. Confirmed live, twice: a
minimal spike (`resolve_customer` called and its result submitted back, no exception) and the full
worked example through the real `EnquiryPipeline` (Extract → Resolve, `resolve_customer` **and**
`get_customer_history` both completing multi-turn, real order-history data returned) before a genuine
free-tier daily quota — 20 requests/day for `gemini-3.6-flash` on this key, exhausted by the day's
debugging — cut the run short. The multi-turn tool-calling mechanism is confirmed fixed; a single
completely clean end-to-end run (through to `ApprovalRequiredEvent`) is still pending a quota reset,
tracked in docs/SESSION-LOG.md.

**Trade-off accepted:** `Google.GenAI`'s `Client` takes an API key or GCP project/location, not an
arbitrary base URL, so the `gemini` profile lost the "any OpenAI-compatible endpoint, just change
`Llm:Endpoint`" swappability this section originally promised. `Endpoint` is now meaningful only for
the `github` fallback profile. `github` is unaffected by any of this — a real OpenAI endpoint, no
`thought_signature` involved.

**The tool-calling loop still runs non-streaming everywhere; only the closing narration is a candidate
for real streaming.** That decision predates this fix and stands regardless of it — the live trace
panel is driven by server-emitted `AgentEvent`s (`stage`, `tool_start`, `tool_end`), not model tokens,
so it was never affected either way. Whether `Google.GenAI`'s streaming path also round-trips
`thought_signature` correctly (its source suggests it should — the same adapter code handles both) is
unverified and out of scope here; docs/SPEC.md §8 already records that no pipeline stage emits a
`token` event yet regardless.

**A related finding from the same investigation:** the two profiles now throw different
exception types for the identical "rate limited" condition — `System.ClientModel.ClientResultException`
from the OpenAI-compatible client, `Google.GenAI.ClientError` from Google's native SDK — and the free-
tier daily quota above hit exactly this path live, proving it was a real gap: before
`EnquiryPipeline.ToErrorEvent` was extended to also match `ClientError { StatusCode: 429 }`, a genuine
Gemini rate limit fell through to a generic `internal` error instead of `provider_rate_limited`. Fixed
in the same commit as the SDK switch, with a stub-based regression test
(`AgentStreamEndpointTests.Process_WhenGoogleGenAiThrowsClientError429_EmitsProviderRateLimited`).

`tests/QuoteDesk.Evals/GeminiWorkedExampleEval.cs` is the regression test for all of the above — it
will read as a full pass once a quota reset allows one clean run through to completion.

**A second, smaller, real finding from the same live run, already fixed:** the Extract prompt asked
for `requiredBy` "interpreted as a plain date" without specifying a wire format, and real
`gemini-3.6-flash` did not reliably produce one — one run returned a correct `2024-05-05`, another
returned the literal word `"5th"` for the same enquiry, which threw `System.Text.Json`'s strict
`DateOnly` converter and failed the whole Extract stage over one optional field. Fixed two ways:
`extract.md` now states the `YYYY-MM-DD` format explicitly, and
`QuoteDesk.Agents.Pipeline.LenientNullableDateOnlyConverter` degrades an unparseable date to `null`
(the same as the field never having been stated) rather than throwing — defense in depth, since a
model does not follow a formatting instruction with 100% reliability and nothing downstream depends on
`RequiredBy` being present (docs/DOMAIN.md's actual delivery dates come from stock and lead time,
computed in code, never from the customer's stated date).

**Rate-limit behaviour is a feature.** On a 429 the API returns `provider_rate_limited` and the UI
offers to replay one of three recorded runs stored as JSON. A recruiter clicking the live demo must
never see a blank error.

**Config, resolved in task 06** (`appsettings.json`'s `Llm` section — key names and non-secret
defaults only, per CLAUDE.md's Security rules): `Endpoint` (defaults to the Gemini endpoint above),
`ApiKey` (empty; set locally via `dotnet user-secrets set "Llm:ApiKey" "<key>" --project src/QuoteDesk.Api`),
`Model` (defaults to `gemini-3.6-flash`), `MaxToolCalls` (8), `TokenBudget` (20000, generous rather than
tuned against a live model — revisit once real runs give real numbers), `UseStructuredOutput` (default
`true` — see below). Bound into
`QuoteDesk.Agents.Llm.LlmOptions` and passed to `AddQuoteDeskAgentPipeline`, the same pattern
`AddQuoteDeskData` uses for its connection string rather than `QuoteDesk.Agents` depending on
`Microsoft.Extensions.Configuration` itself. **Resolved in task 07:** wired into `Program.cs`, and an
empty `Llm:ApiKey` fails fast the same way `Auth:Google:ClientId` already did. **`Provider`** (defaults
to `"gemini"`) added alongside the `thought_signature` fix above — see `LlmOptions.cs`'s remarks for
why the two profiles could no longer share one client differing only by `Endpoint`.

**Per-stage model selection and provider fallback are task 09's**, not built. Today one `Model` is
used for all three model call types (Extract, Resolve, Narrate) and a provider 429 ends the run.
Task 09 splits the two easy stages onto a cheap high-quota model and adds a per-run fallback — see
`tasks/task-09-deploy.md`.

**Structured output — now used, corrected in the 2026-08-31 agent-layer rework.** This section
originally said schema-enforced output was deliberately avoided because Gemini's support for it was
unverified. It is now used, with a fallback rather than an avoidance:
`QuoteDesk.Agents.Pipeline.StructuredModelCall` asks the provider to enforce a JSON schema —
generated from the C# result type itself via `ChatResponseFormat.ForJsonSchema<T>`, so there is no
hand-written schema to drift — on the **Extract** and **Narrate** stages. If the provider rejects
schema mode, it falls back to the tolerant `ModelJson` parser for that call and logs a warning
(flip `Llm:UseStructuredOutput` to `false` to stop paying for the rejected attempt). Under both
paths, an unparseable reply is **retried once with the parse error fed back** — before this, one
reply of prose instead of JSON killed the whole run with no recovery. **Resolve deliberately stays
on plain-text parsing**: it is the one stage that calls tools, a strict response format would apply
to every turn of the tool loop, and whether a given provider handles that combination is unverified.
Whether `gemini-3.6-flash` honours schema mode at all is still unverified until the first live run —
the fallback exists precisely for that.

## 5. Intake

One shape, three adapters, built in risk order (tasks 04 and 10):

```csharp
IncomingEnquiry { Channel, SenderId, Body, ReceivedAt, Attachments[] }
```

Nothing downstream knows where an enquiry came from. `EnquiryChannel` never appears outside
`QuoteDesk.Intake`.

| Channel | How | Risk |
|---|---|---|
| **Paste** | UI textarea → `POST /api/enquiries` | none — this is what a recruiter uses, never break it |
| **Email** | MailKit IMAP poller in a `BackgroundService` | low — needs a mailbox and an app password |
| **WhatsApp** | Twilio sandbox webhook, signature verified | low — join by texting a code, no verification wait |

Never use `whatsapp-web.js` or similar. Against WhatsApp's terms, and it gets numbers banned. Meta's
Cloud API is explicitly **not** on the critical path — business verification takes days and can fail.

**Attachments.** Images may be read directly by a multimodal model on the default `gemini` profile —
no OCR service, no second provider. Audio is **not** processed: the voice note is stored, playable in
the UI, and the enquiry is marked `needs_manual_entry` for a human to type. This is graceful
degradation, and it is honest future work in the README.

## 6. Data model

EF Core, SQL Server, migrations generated by the CLI, seeded deterministically from a fixed random
seed so evals are reproducible.

```
Customers     (Id, Name, EmailDomain, WhatsAppNumber, Tier, CreditDays, GstIn, DefaultShipTo)  -- 25
CatalogItems  (Id, Sku, Name, Category, Uom, ListPrice, CostPrice, Attributes)                 -- 300
StockLevels   (Sku, OnHand, LeadTimeDays, ReorderLevel)                                        -- 300
PriceRules    (Id, Scope, Target, MinQty, DiscountPct)                                         -- ~40
OrderHistory  (Id, CustomerId, Sku, Qty, UnitPrice, OrderedAt)                                 -- ~1200
Enquiries     (Id, Channel, SenderId, RawBody, ReceivedAt, CustomerId, Status)                 -- 12 seeded
Quotes        (Id, EnquiryId, Number, Status, Subtotal, Freight, Tax, Total, CreatedAt,
               ValidUntil, ShipTo, RequiredBy, ApprovedByUserId, ApprovedAt, SentAt)            -- empty
QuoteLines    (Id, QuoteId, Sku, Qty, UnitPrice, DiscountPct, LineTotal,
               RequiresOverride, DispatchDate, DeliveryDate, Note)                              -- empty
Users         (Id, GoogleSubject, Email, Name, PictureUrl, Role, CreatedAt, LastLoginAt)       -- empty
AgentRuns     (Id, EnquiryId, SessionId, Status, ApprovalRequestJson, CreatedAt, UpdatedAt)     -- empty
WorkflowCheckpoints (Id, SessionId, CheckpointId, ParentCheckpointId, Payload, CreatedAt)       -- empty
```

`CostPrice` exists so margin can be checked. **It never leaves the server and never reaches the
model** — enforced by a reflection test over tool result types, not by convention.

`Users` is the one table this document did not originally plan for (§3, §9) — added ahead of
schedule, before task 04, alongside Google sign-in. `GoogleSubject` (the `sub` claim) is the unique
key a returning sign-in is matched on, never the email, which a Google account can change.
`Quotes.ApprovedBy` — originally a free-text name — became `ApprovedByUserId int?` (FK, restrict
delete) while the table was still empty, so the change was free; it names the actual signed-in
salesperson who approves a quote rather than a string someone typed.

`Quotes.Freight/ValidUntil` and `QuoteLines.RequiresOverride/DispatchDate/DeliveryDate` were added
in task 05's `AddQuoteDetails` migration — both tables were still empty, so the change was free.
`Quotes.ShipTo` and `Quotes.RequiredBy` exist for the Extract stage (task 06) to populate from the
enquiry text; `create_quote_draft` leaves them null until then. `RequiresOverride` is stored, but
`MarginShortfallPct` deliberately is not — it is a margin figure, which must never leave the server
(§7 below), and the approval card only needs to know a line needs an override, not by how much.

Money columns are `decimal(18,2)`, configured explicitly. Read queries use `AsNoTracking()`. No entity
type appears in a signature outside `QuoteDesk.Data`.

`AgentRuns` and `WorkflowCheckpoints` were added in task 06's `AddAgentRuns` migration, both tables
new so the change was free. `AgentRuns` is one row per pipeline run of one enquiry — what
`GET /api/approvals` (task 07) will list, and how `EnquiryPipeline.ResumeAsync` finds which
`SessionId` to resume from a bare enquiry id. `WorkflowCheckpoints` is the backing store behind
`Microsoft.Agents.AI.Workflows`' own `ICheckpointStore<JsonElement>` — `Payload` is the framework's
serialized state, opaque to `QuoteDesk.Data`, which only stores and retrieves it by session and
checkpoint id; the bridge onto the framework's interface lives in `QuoteDesk.Agents.Checkpointing
.SqlCheckpointStore`, keeping every `Microsoft.Agents.AI.Workflows` type out of `QuoteDesk.Data`
entirely, the same as every other framework-agnostic repository here.
`WorkflowCheckpointRepository` uses `IDbContextFactory<QuoteDeskDbContext>` rather than the shared
scoped context every other repository uses: the workflow engine writes a checkpoint from its own
background execution task, concurrently with whatever the caller driving the run's event stream does
on the same request's `DbContext` — sharing one instance between those two concurrent paths threw
EF Core's "a second operation was started on this context" error, found while writing task 06's own
integration tests.

`AgentRuns.TraceJson` was added in task 07's `AddAgentRunTrace` migration — the table already existed
and had no callers writing this column before task 07, so the change was free. See §8's "Resolved in
task 07" for what it stores and why.

## 7. Tools

| Tool | Signature | Write? |
|---|---|---|
| `resolve_customer` | `(string companyName, string senderId) -> CustomerMatch` | no |
| `search_catalog` | `(CatalogSearchQuery[] queries) -> CatalogSearchResult[]` | no |
| `get_customer_history` | `(int customerId, string? sku) -> PriorPurchase[]` | no |
| `check_stock` | `(string sku, int qty) -> StockResult` | no |
| `price_quote` | `(int? customerId, QuoteLineRequest[] lines) -> PricedQuote` | no |
| `create_quote_draft` | `(int enquiryId, PricedQuote quote) -> QuoteDraftResult` | **gated** |
| `send_quote` | `(int quoteId) -> SendResult` | **gated** |

**Two signatures corrected during task 05, in the same commit as the code:**

- **`search_catalog` returns `CatalogSearchResult[]`, not a bare `CatalogMatch[]`.** An array has no
  way to say "I cannot tell which of these you mean" — `CatalogSearchResult { Query, Outcome,
  ResolvedSku?, Candidates[], Reason }` carries that explicitly. `Outcome` is `resolved` / `ambiguous` / 
  `not_found`; candidates always carry a confidence and a reason, and the tool never picks one
  arbitrarily when several score within 0.2 of each other.
- **`price_quote` takes `int? customerId`, not `int`.** docs/DOMAIN.md's "Unknown sender" rule — list
  price and the quantity discount still apply even when nothing matched — has nowhere to be
  expressed if the tool cannot be called without a customer at all.
- **`create_quote_draft` returns `QuoteDraftResult`, not a bare `QuoteId`.** Consistent with every
  other tool's validation style (a typed miss, never an exception) — an unknown enquiry or an empty
  line list returns `Created = false` with a `Reason` rather than throwing.

`search_catalog` returns ranked candidates with a confidence and an explicit ambiguous result when it
cannot choose (see the retrieval rewrite below). `get_customer_history` is what resolves "same as
last time" — the tool that makes the demo feel intelligent. `price_quote` calls `QuoteDesk.Domain`
and nothing else.

Read and write registries are separate objects (`ReadToolRegistry`, `WriteToolRegistry` in
`QuoteDesk.Agents.Tools`); the Resolve agent is constructed with the read registry only — enforced by
a test that constructs `ReadToolRegistry` and asserts neither write tool's name appears in it, not
just by the two classes being separate.

**Resolved in task 06: the Resolve agent is handed four of `ReadToolRegistry`'s five tools, not all
five.** `price_quote` is excluded — the Price stage (pure code) calls `PricingTools.PriceQuoteAsync`
directly instead, so "the model never decides money" (CLAUDE.md rule 1) is structurally true rather
than a matter of the model choosing not to call a tool it could technically reach. `ReadToolRegistry`
itself is unchanged (still all five, still tested as such); the filtering happens where the Resolve
agent's tool list is built (`QuoteDesk.Agents.Pipeline.EnquiryPipeline`).

**`search_catalog` was changed from one query per call to a batch of queries per call, found and fixed
in the same session as the `thought_signature` correction above.** The original signature
(`(string query, string[] hints) -> CatalogSearchResult`) cost one real Gemini call per line item —
three lines meant three calls, purely because the tool couldn't accept more than one query at a time,
not because the model needed three separate turns to think about it. `resolve.md`'s prompt now
instructs the model to call `search_catalog` once per enquiry, passing every line as a
`CatalogSearchQuery { Query, Hints }` entry in one `queries` array, and reads back one
`CatalogSearchResult` per entry, same order. For docs/DOMAIN.md's worked example this cuts Resolve
from 6 real model calls to 4, and the whole pipeline from 8 to 6. No change was needed to
`ResolveExecutor`, `TracedAIFunction`, or `ToolCallBudget` — none of them ever assumed one
`search_catalog` call resolved exactly one line, so the batching is entirely internal to
`CatalogTools`, `CatalogSearchQuery`/`CatalogSearchResult`, and the prompt.

**`search_catalog` was rebuilt as a two-stage ranker in the 2026-08-31 agent-layer rework — the
change that fixes task 08's first live run.** That run failed because the old scorer matched on
letters, not words: a query for a *PU* belt also matched every *s**pu**r* gear, a query for a *ring*
frame tape matched every *bea**ring***, and nothing was ever capped, so `search_catalog` returned
**342 candidates from a 262-row catalogue** — one 56 KB tool result, re-sent on every turn of the
tool loop until the provider refused. What it does now:

- **Stage one, recall:** the existing cheap substring lookup per search word, unioned.
- **Stage two, precision:** re-rank that shortlist with *whole-word* matching, weighted by inverse
  document frequency, so a rare distinguishing word (`PU`, `6203`, `2RS`, `25mm`) counts far more
  than a common family word (`belt`, `bearing`). Scoring is an additive sum of matched-word weights,
  so an extra hint can only help a candidate — this fixes the bug where the junk word `as` (from
  "same as last time") dragged a perfect 6203 match below the resolve threshold.
- **An absolute and a relative confidence floor**, then a **hard cap of five candidates** in every
  outcome — a query that cannot answer in five rows returns `ambiguous` or `not_found`.
- `CatalogCandidate` slimmed to `Sku`, `Name`, `Category`, `Attributes`, `Confidence` — the per-row
  `Reason`, `Uom` and `ListPrice` are gone (the model never prices; one `Reason` on the
  `CatalogSearchResult` explains the outcome).
- `resolve.md` now states the catalogue's grid structure up front (four families, two axes each) so
  the model's queries carry the family word and the distinguishing spec.
- `TracedAIFunction` also caps what a tool result writes into the trace at ~8 KB — belt-and-braces
  for a future tool that forgets to cap itself, since a tool result is paid for three times over
  (model input, SSE stream, `AgentRuns.TraceJson`).

**`get_customer_history` is capped at the 20 most-recent rows** (`OrderHistoryRepository`). It
returned all ~48 of a customer's orders, re-sent every turn — the second-worst driver of runaway
token cost. Twenty most-recent still answers "same as last time".

## 8. API

```
POST /api/auth/google                -> { token, expiresAt, user }         anonymous
GET  /api/auth/me                    -> { id, email, name, pictureUrl, role }
POST /api/enquiries                  -> { enquiryId }
POST /api/enquiries/{id}/process     -> SSE stream of AgentEvent
GET  /api/enquiries/{id}             -> transcript + full trace
GET  /api/approvals                  -> pending approvals
POST /api/approvals/{id}             -> { decision: approve|reject, rejectionReason? }
GET  /api/quotes                     -> list
GET  /api/quotes/{id}                -> detail, with the trace that produced it
GET  /health/live  /health/ready
```

`AgentEvent` — defined once in C#, mirrored exactly in TypeScript, both changed in one commit:

```ts
type AgentEvent =
  | { type: 'stage';      stage: 'extract'|'resolve'|'price'; at: string }
  | { type: 'tool_start'; name: string; args: unknown; at: string }
  | { type: 'tool_end';   name: string; ms: number; ok: boolean; result: unknown }
  | { type: 'token';      text: string }
  | { type: 'approval_required'; approvalId: string; action: string; payload: unknown }
  | { type: 'done';       usage: { promptTokens: number; completionTokens: number } }
  | { type: 'error';      code: 'provider_rate_limited'|'budget_exceeded'|'internal'; message: string }
```

**Resolved in task 07:**

- **`POST /api/approvals/{id}` supports `approve` and `reject` only** — `edit` returns 400
  ProblemDetails. Editing a priced quote (choosing different lines, letting the server re-price) is a
  real future need but has nowhere sensible to live until task 08's approval card exists to say what a
  salesperson actually needs to change; deciding that shape now, with no UI to validate it against,
  risked designing the wrong payload. `{id}` is the `AgentRun.Id`, the same id
  `ApprovalRequiredEvent.ApprovalId` already carries.
- **Both streaming endpoints share one writer**, `QuoteDesk.Api.Streaming.AgentEventStreamWriter` —
  SSE framing (`data: {json}\n\n`, one flush per event) exists in exactly one place. It also buffers
  every event it streams and persists the run's full trace in a `finally`, so a dropped connection
  still leaves whatever ran on the record, and the persistence write deliberately uses
  `CancellationToken.None` rather than the request's own token — a token that just fired (the
  connection dropping) would otherwise cancel the very write meant to survive that drop.
- **The trace is stored as `AgentRuns.TraceJson`** (task 07's `AddAgentRunTrace` migration) — one
  `nvarchar(max)` column holding the run's complete `AgentEvent[]`, appended to (by reading, merging,
  and rewriting — not a SQL append) across a suspend/resume boundary, since a run streams twice: once
  to `/process` (Extract → Resolve → Price, then suspend), once to `/approvals/{id}` (Approve). This is
  what `GET /api/enquiries/{id}` and `GET /api/quotes/{id}` replay once the live SSE stream that
  produced it has closed — CLAUDE.md calls the Agent Trace panel "the product", so it must survive a
  page refresh, not only exist while a browser tab is watching.
- **Rate limiting is deferred to task 09.** The acceptance criterion task 07's own file originally
  carried (per-IP, per-token, a daily cap) was struck: it defends a public URL that does not exist
  until task 09, and Harsh's standing instruction after task 05's review is to build the MVP and defer
  hardening until the product works end to end. Task 09 owns the public demo, so the rate limiter lands
  there, where the daily cap has a real number to be sized against.
- **`token` is declared in the union above but no pipeline stage emits one yet.** §4 describes
  streaming Price's narration for real; task 06 built `PriceExecutor.NarrateAsync` as one plain,
  non-streaming `narrateAgent.RunAsync` call instead, and `StubChatClient.GetStreamingResponseAsync`
  was written to throw `NotSupportedException` on the assumption nothing would ever call it. The SSE
  transport itself (`AgentEventStreamWriter`) is variant-agnostic — it will carry a `token` event the
  moment some stage actually produces one — so this is a real gap in what task 06 implemented against
  what this document described, not a task 07 limitation. Left as documented future work rather than
  fixed here: narration streaming touches already-tested task 06 code and the `StubChatClient`
  contract every integration test relies on, which is bigger than task 07's own scope of wiring the
  existing pipeline behind HTTP.

**Resolved in task 08 (the React screens):**

- **The trace panel shows a plain-language label for each step, never the raw tool or stage name.**
  Harsh's call during the design review — `resolve_customer` etc. are internal identifiers, not
  something a salesperson should see. `src/api/traceLabels.ts` maps the name from the `AgentEvent`
  to a human label ("Matched customer", "Searched catalogue", …); an unmapped name degrades to a
  de-underscored, title-cased form. The step's argument payload and result are still shown on
  expand. This supersedes the "tool name" wording in the `AgentEvent` description above and in
  CLAUDE.md.
- **The approval UI is approve / reject only, and resolves no ambiguous lines.** `edit` was already
  400 from task 07; task 08 confirmed there is no sensible dropdown to build either, because
  `UnresolvedLine` carries only `{originalDescription, quantity, reason}` — no SKU candidates. The
  card shows unresolved lines in red with the agent's reason and blocks nothing else; the quote
  simply cannot be sent until a human deals with them. The deferred shape, when a later task wants
  real line resolution: `UnresolvedLine.Candidates[]` (Resolve already computes them and throws them
  away) plus `ApprovalDecisionRequest.LineSelections[{originalDescription, sku}]`, re-priced through
  `PricingTools` before the draft is created.
- **`useAgentStream` is the one SSE reader**, `fetch` + `ReadableStream` (POST + bearer header rule
  out `EventSource`). It checks `Content-Type` before parsing because `/api/approvals/{id}` answers a
  rejected decision with JSON ProblemDetails, not a stream. No client reconnect — the server writes
  no `id:` lines — so recovery is a fresh `GET /api/enquiries/{id}` for the stored trace.
- **`provider_rate_limited` offers three runs recorded by hand as typed `AgentEvent[]`**
  (`src/QuoteDesk.Web/src/fixtures/*.ts`), not captured from a live model — deterministic and free
  of the daily quota. Only a `429` on the stream fetch triggers the replay picker; a run that
  exhausts the token budget instead surfaces `budget_exceeded` and renders as a plain error with a
  retry. Widening the picker to any provider failure is a small follow-up.

**Resolved in the 2026-08-31 agent-layer rework:**

- **The token budget is a governor now, not a post-mortem.** `BudgetedChatClient` wraps the chat
  client and counts every model round-trip against the run's `TokenUsageTracker` as it happens,
  throwing the moment the budget is breached. Before, counting happened only after a whole agent run
  finished, so Resolve's tool loop could spend several times the budget before anything looked — one
  recorded run reached 56,463 tokens against a 20,000 budget and reported it only once it was over.
  This is also the single place tokens are counted; the three scattered `tokens.Add(...)` calls in
  the executors are gone.
- **`budget_exceeded` is also raised for provider context-limit failures.** `EnquiryPipeline
  .ToErrorEvent` now maps a provider `400`/`413` whose message mentions tokens or context length to
  `budget_exceeded`, not a bare `internal` — the failure mode that took down task 08's first live
  run. Every failure also logs the full exception (type, message, stack) at `Error` level with the
  correlation id, so the next one is diagnosable from the server log rather than by reading
  `AgentRuns.TraceJson` by hand. The client still only ever sees the shaped `ErrorEvent`.
- **The frontend still treats only `429` specially.** `budget_exceeded` and `internal` both render
  as a plain trace-panel error; the recorded-run replay picker — which exists for exactly this — is
  not offered for them yet. Tracked in `tasks/task-08-web.md`'s notes and picked up in the post-09
  review pass.

## 9. Non-goals — refuse these

Multi-tenancy · user registration · vector DB · real email or WhatsApp *sending* (render the PDF, log
the send) · audio transcription · mobile layouts beyond "doesn't break" · admin panel · i18n ·
WebSockets · microservices · more than two agent nodes · any second business domain.

**"User registration" stays refused precisely** despite the `Users` table added in §6: there is no
sign-up form, no password, no profile editing, no invite flow, and no admin UI to manage users. A row
is created automatically the first time a Google account signs in, and that is the entire surface —
one upsert, triggered by Google's own identity check, with nothing for a user to fill in. If a task
ever proposes any of the things this paragraph just ruled out, that is scope creep and should be
challenged the same way any other item on this list would be.

## 10. Scope

Tasks 00–03 produce provably correct pricing with no LLM involved. Tasks 04–08 produce the working
product. **Task 09 deploys it** — from that point a live URL exists and every later task improves
something already running. Tasks 10–11 add channels, telemetry, evals and the README.

Do not remove the old project from the resume until this one is deployed, green, and documented.
