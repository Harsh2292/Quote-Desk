# QuoteDesk — specification

The contract. When the code and this document disagree, one of them is wrong — fix it deliberately,
in the same commit. Task-level detail lives in `tasks/`; this file is the shape of the whole thing.

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

## 3. Why it is built this way

| Property | What it demonstrates |
|---|---|
| Typed tools over EF Core, never raw SQL from the model | Judgment about blast radius |
| Every rupee computed in C#, explained by the model | You know where LLMs must not be trusted |
| Workflow suspends for human approval before any write | Agents ≠ autonomy |
| One channel-agnostic intake shape, three adapters | You design for change without over-building |
| Streamed agent trace in the UI | You understand latency UX and debuggability |
| OpenTelemetry span per stage and per tool call | You have operated software, not just written it |
| Golden-set evals in CI, including prompt injection | Almost nobody's portfolio has this |
| Provider-swappable LLM behind one interface | Vendor risk, and it runs free |

Deliberately absent: **no vector database.** The data is structured; embeddings would be the wrong
tool. Say that out loud — it is a better answer than having used one.

## 4. Stack

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
| Auth | JWT bearer, one demo user. **Do not build a user system.** |
| Tests | xUnit + FluentAssertions, in `tests/` |
| CI/CD | GitHub Actions → Azure Container Apps + Static Web Apps |

As of Aug 2026 `Microsoft.Agents.AI` was at 1.19.0 and `Microsoft.Agents.AI.OpenAI` at 1.5.0. **Do
not hardcode versions from this document** — run `dotnet add package`, take what NuGet resolves, and
record the resolved versions back here.

## 5. LLM provider — free, and swappable

Both providers speak the OpenAI wire protocol, so the difference is one endpoint:

```csharp
var options = new OpenAIClientOptions { Endpoint = new Uri(cfg["Llm:Endpoint"]!) };
var chat = new OpenAIClient(new ApiKeyCredential(cfg["Llm:ApiKey"]!), options)
    .GetChatClient(cfg["Llm:Model"]!);
AIAgent agent = chat.AsAIAgent(instructions: ..., name: ...);
```

| Profile | Endpoint | Notes |
|---|---|---|
| **`gemini` (default)** | `https://generativelanguage.googleapis.com/v1beta/openai/` | ~1000+ requests/day free. Enough for a public demo and for repeated eval runs, and it reads images, so one key covers every channel. Its OpenAI-compatibility layer has known quirks with tool calls **combined with streaming** — task 00 settles this before anything depends on it. |
| `github` (fallback) | `https://models.github.ai/inference` | Free with a GitHub PAT scoped `models:read`. Real OpenAI models, so tool calling behaves exactly as documented — useful as a control if Gemini misbehaves. But ~50 requests/day and an ~8K input cap, so it cannot carry the demo or the evals, and a photo will not fit. |

**Why Gemini is the default:** the eval suite runs 15–20 cases per pass, which would consume a third
of GitHub Models' daily quota in a single run, and a public demo that dies after ten clicks is worse
than no demo.

**If streaming plus tool calls proves broken on the compatibility layer**, it is not a blocker. The
live trace is driven by server-emitted `AgentEvent`s — `stage`, `tool_start`, `tool_end` — not by
model tokens; only the final narration uses `token` events. Run the tool loop non-streaming and
stream just the narration. The UI is unchanged. Task 00 decides this in twenty minutes.

**Rate-limit behaviour is a feature.** On a 429 the API returns `provider_rate_limited` and the UI
offers to replay one of three recorded runs stored as JSON. A recruiter clicking the live demo must
never see a blank error.

## 6. Intake

One shape, three adapters, built in risk order (tasks 04 and 09):

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
| WhatsApp (Meta Cloud API) | optional, **not on the critical path** | high — business verification takes days and can fail |

Never use `whatsapp-web.js` or similar. Against WhatsApp's terms, and it gets numbers banned.

**Attachments.** Images may be read directly by a multimodal model on the default `gemini` profile —
no OCR service, no second provider. Audio is **not** processed: the voice note is stored, playable in the UI, and the enquiry is
marked `needs_manual_entry` for a human to type. Chat endpoints do not take audio on our code path,
free transcription is weak, and code-mixed Gujarati-Hindi-English is unreliable. This is graceful
degradation, and it is honest future work in the README.

## 7. Data model

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

## 8. Tools

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

## 9. API

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

## 10. Non-goals — refuse these

Multi-tenancy · user registration · vector DB · real email or WhatsApp *sending* (render the PDF, log
the send) · audio transcription · mobile layouts beyond "doesn't break" · admin panel · i18n ·
WebSockets · microservices · more than two agent nodes · any second business domain.

## 11. Deployment — Azure at effectively zero

| Piece | Service | Free basis |
|---|---|---|
| API | Container Apps, **min replicas 0** | 180k vCPU-s, 360k GiB-s, 2M requests free per subscription per month; nothing charged while scaled to zero |
| DB | Azure SQL **free offer** | 100k vCore-seconds + 32 GB, permanent, auto-pauses when exhausted — choose **auto-pause**, not pay-overage, which cannot be reversed |
| Frontend | Static Web Apps Free | free tier |
| Telemetry | Application Insights | free allowance; set a daily cap |
| Images | GitHub Container Registry | free for public |
| LLM | Google Gemini | free tier, no card |

Scale-to-zero means the first request after idle is slow. Put the measured number in the README —
"why is it slow the first time" is a question you want to be asked.

Set a ₹0 budget alert regardless. Free tiers change.

## 12. Honest scope

Three sessions. Task 00 is a twenty-minute spike; tasks 01–03 produce provably correct pricing with no LLM involved. Tasks 04–08
produce the working product. Tasks 09–11 are channels, telemetry, evals, deploy and the README — the
least enjoyable part, and precisely what separates this from every other portfolio repo.

Do not remove the old project from the resume until this one is deployed, green, and documented.
