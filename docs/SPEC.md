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
| Auth | **Google OpenID Connect.** Still no user system to build — Google is the identity provider, sign-in is a redirect, and the app stores no password. Changed from the JWT-bearer/seeded-credential plan in task 01, on Harsh's instruction. |
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

`Microsoft.AspNetCore.OpenApi` was deliberately **not** added to `QuoteDesk.Api`: the version NuGet
resolved for .NET 10 (2.0.0) pulls in a `Microsoft.OpenApi` with a known high-severity advisory,
which fails `-warnaserror`'s `NU1903` check. Task 01 needs no Swagger UI, so the package was dropped
rather than suppressed; revisit if a later task genuinely needs OpenAPI generation.

## 4. LLM provider — free, and swappable

Both providers speak the OpenAI wire protocol, so the difference is one endpoint:

```csharp
var options = new OpenAIClientOptions { Endpoint = new Uri(cfg["Llm:Endpoint"]!) };
var chat = new OpenAIClient(new ApiKeyCredential(cfg["Llm:ApiKey"]!), options)
    .GetChatClient(cfg["Llm:Model"]!);
AIAgent agent = chat.AsAIAgent(instructions: ..., name: ...);
```

| Profile | Endpoint | Notes |
|---|---|---|
| **`gemini` (default)** | `https://generativelanguage.googleapis.com/v1beta/openai/` | ~1000+ requests/day free. Enough for a public demo and repeated eval runs, and it reads images, so one key covers every channel. **Tool calls work non-streaming; streaming is broken by a provider-side limitation — see below.** |
| `github` (fallback) | `https://models.github.ai/inference` | Free with a GitHub PAT scoped `models:read`. Real OpenAI models, so tool calling behaves exactly as documented — useful as a control. But ~50 requests/day and an ~8K input cap, so it cannot carry the demo or the evals. |

**Pinned model: `gemini-3.6-flash`.** `gemini-2.5-flash` — the id this document originally assumed —
returns `404` for new keys ("no longer available to new users"); Google's own error names
`gemini-3.6-flash` as the replacement. Never use `gemini-flash-latest` or any other alias: it moves
under you and breaks eval reproducibility.

**Verified 2026-08-29 — task 00 spike, against `gemini-3.6-flash`:**

- **Non-streaming tool calls: works end to end.** `CompleteChatAsync` calls the tool, the follow-up
  request with the tool result gets a correct final answer.
- **Streaming tool calls: broken, provider-side, not fixable in our code.** The tool call itself
  surfaces correctly over `CompleteChatStreamingAsync`. Submitting the result on the *next* streaming
  turn fails with `400 INVALID_ARGUMENT`: *"Function call is missing a `thought_signature` in
  functionCall parts... required for tools to work correctly."* Gemini's 3.x "thinking" models attach
  a `thought_signature` to every function-call part and require it echoed back; that field has no
  home in the standard OpenAI wire schema, so the `OpenAI` .NET SDK's `ChatToolCall` cannot carry it.
  This is a real protocol gap between Gemini's thinking models and OpenAI-compatibility clients, not
  a bug in this codebase — confirmed by reproducing the same request with raw `curl`.

**Decision: run the tool-calling loop non-streaming everywhere, stream only the closing narration.**
This was the fallback this document already planned for. The live trace panel is driven by
server-emitted `AgentEvent`s — `stage`, `tool_start`, `tool_end` — not by model tokens, so the UI is
unaffected. Only `token` events, used for the final human-readable sentence, need real streaming, and
that call carries no tool calls to replay, so it is unaffected by this issue.

**Rate-limit behaviour is a feature.** On a 429 the API returns `provider_rate_limited` and the UI
offers to replay one of three recorded runs stored as JSON. A recruiter clicking the live demo must
never see a blank error.

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
Quotes        (Id, EnquiryId, Number, Status, Subtotal, Tax, Total, CreatedAt,
               ApprovedBy, ApprovedAt, SentAt)                                                 -- empty
QuoteLines    (Id, QuoteId, Sku, Qty, UnitPrice, DiscountPct, LineTotal, Note)                 -- empty
```

`CostPrice` exists so margin can be checked. **It never leaves the server and never reaches the
model** — enforced by a reflection test over tool result types, not by convention.

Money columns are `decimal(18,2)`, configured explicitly. Read queries use `AsNoTracking()`. No entity
type appears in a signature outside `QuoteDesk.Data`.

## 7. Tools

| Tool | Signature | Write? |
|---|---|---|
| `resolve_customer` | `(string companyName, string senderId) -> CustomerMatch` | no |
| `search_catalog` | `(string query, string[] hints) -> CatalogMatch[]` | no |
| `get_customer_history` | `(int customerId, string? sku) -> PriorPurchase[]` | no |
| `check_stock` | `(string sku, int qty) -> StockResult` | no |
| `price_quote` | `(int customerId, QuoteLineRequest[] lines) -> PricedQuote` | no |
| `create_quote_draft` | `(int enquiryId, PricedQuote quote) -> QuoteId` | **gated** |
| `send_quote` | `(int quoteId) -> SendResult` | **gated** |

`search_catalog` returns candidates with a confidence and a reason, and an explicit ambiguous result
when it cannot choose. `get_customer_history` is what resolves "same as last time" — the tool that
makes the demo feel intelligent. `price_quote` calls `QuoteDesk.Domain` and nothing else.

Read and write registries are separate objects; the Resolve agent is constructed with the read
registry only.

## 8. API

```
POST /api/enquiries                  -> { enquiryId }
POST /api/enquiries/{id}/process     -> SSE stream of AgentEvent
GET  /api/enquiries/{id}             -> transcript + full trace
GET  /api/approvals                  -> pending approvals
POST /api/approvals/{id}             -> { decision: approve|edit|reject, payload? }
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

## 9. Non-goals — refuse these

Multi-tenancy · user registration · vector DB · real email or WhatsApp *sending* (render the PDF, log
the send) · audio transcription · mobile layouts beyond "doesn't break" · admin panel · i18n ·
WebSockets · microservices · more than two agent nodes · any second business domain.

## 10. Scope

Tasks 00–03 produce provably correct pricing with no LLM involved. Tasks 04–08 produce the working
product. **Task 09 deploys it** — from that point a live URL exists and every later task improves
something already running. Tasks 10–11 add channels, telemetry, evals and the README.

Do not remove the old project from the resume until this one is deployed, green, and documented.
