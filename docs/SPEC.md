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

**Config, resolved in task 06** (`appsettings.json`'s `Llm` section — key names and non-secret
defaults only, per CLAUDE.md's Security rules): `Endpoint` (defaults to the Gemini endpoint above),
`ApiKey` (empty; set locally via `dotnet user-secrets set "Llm:ApiKey" "<key>" --project src/QuoteDesk.Api`),
`Model` (defaults to `gemini-3.6-flash`), `MaxToolCalls` (8), `TokenBudget` (20000, generous rather than
tuned against a live model — revisit once real runs give real numbers). Bound into
`QuoteDesk.Agents.Llm.LlmOptions` and passed to `AddQuoteDeskAgentPipeline`, the same pattern
`AddQuoteDeskData` uses for its connection string rather than `QuoteDesk.Agents` depending on
`Microsoft.Extensions.Configuration` itself. **Not yet wired into `QuoteDesk.Api`'s `Program.cs`** —
task 06 is deliberately out of scope for the HTTP surface (see task file); task 07 does that binding
and decides whether an empty `Llm:ApiKey` should fail fast the way `Auth:Google:ClientId` already does.

**Structured output — deliberately not used.** `AIAgent.RunAsync<T>()`'s built-in `json_schema`
response-format mode was confirmed to exist and work mechanically (decompiled), but whether Gemini's
OpenAI-compatibility endpoint actually honours `response_format: {type: "json_schema"}` for
`gemini-3.6-flash` specifically is **unverified** — official docs are silent, and the only evidence
found is an unconfirmed community forum report. Every model call in the pipeline instead asks for
plain text and parses JSON out of it tolerantly (`QuoteDesk.Agents.Pipeline.ModelJson`, stripping a
` ```json ` fence if present), uniformly rather than as a caught-failure fallback — cheap insurance,
and it sidesteps a second unverified provider behaviour after `thought_signature` already cost a day.

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

## 7. Tools

| Tool | Signature | Write? |
|---|---|---|
| `resolve_customer` | `(string companyName, string senderId) -> CustomerMatch` | no |
| `search_catalog` | `(string query, string[] hints) -> CatalogSearchResult` | no |
| `get_customer_history` | `(int customerId, string? sku) -> PriorPurchase[]` | no |
| `check_stock` | `(string sku, int qty) -> StockResult` | no |
| `price_quote` | `(int? customerId, QuoteLineRequest[] lines) -> PricedQuote` | no |
| `create_quote_draft` | `(int enquiryId, PricedQuote quote) -> QuoteDraftResult` | **gated** |
| `send_quote` | `(int quoteId) -> SendResult` | **gated** |

**Two signatures corrected during task 05, in the same commit as the code:**

- **`search_catalog` returns `CatalogSearchResult`, not a bare `CatalogMatch[]`.** An array has no
  way to say "I cannot tell which of these you mean" — `CatalogSearchResult { Outcome, ResolvedSku?,
  Candidates[], Reason }` carries that explicitly. `Outcome` is `resolved` / `ambiguous` / 
  `not_found`; candidates always carry a confidence and a reason, and the tool never picks one
  arbitrarily when several score within 0.2 of each other.
- **`price_quote` takes `int? customerId`, not `int`.** docs/DOMAIN.md's "Unknown sender" rule — list
  price and the quantity discount still apply even when nothing matched — has nowhere to be
  expressed if the tool cannot be called without a customer at all.
- **`create_quote_draft` returns `QuoteDraftResult`, not a bare `QuoteId`.** Consistent with every
  other tool's validation style (a typed miss, never an exception) — an unknown enquiry or an empty
  line list returns `Created = false` with a `Reason` rather than throwing.

`search_catalog` returns candidates with a confidence and a reason, and an explicit ambiguous result
when it cannot choose. `get_customer_history` is what resolves "same as last time" — the tool that
makes the demo feel intelligent. `price_quote` calls `QuoteDesk.Domain` and nothing else.

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

## 8. API

```
POST /api/auth/google                -> { token, expiresAt, user }         anonymous
GET  /api/auth/me                    -> { id, email, name, pictureUrl, role }
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
