# Task 07 — API, streaming, auth, logging

**Session 2 · depends on: 06**

## Goal

The workflow reachable over HTTP, streaming its progress, behind auth, with proper logging and error
shapes.

## Stack for this task

ASP.NET Core Minimal APIs · Server-Sent Events · Google OpenID Connect · `AddRateLimiter` · Serilog

## What to build

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

`AgentEvent` — define once in C#, mirror exactly in TypeScript, change both in the same commit:

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

- ~~Google OpenID Connect on every `/api/*` route~~ — **done ahead of schedule, before task 04.**
  React gets a Google ID token and posts it to `POST /api/auth/google`; the Api verifies it against
  Google (`Google.Apis.Auth`) and mints its own short-lived JWT, checked by
  `Microsoft.AspNetCore.Authentication.JwtBearer` against a fallback authorization policy that
  requires an authenticated user on every route by default. A `Users` table (`QuoteDesk.Data`,
  migration `AddUsers`) is auto-provisioned on first sign-in — see docs/SPEC.md §3 and §6 for the
  final shape, and docs/SESSION-LOG.md for why this moved earlier: doing it before any endpoint
  existed meant every endpoint from task 04 onward is protected by construction, not by a retrofit
  across eight routes. This task still owns rate limiting the `/api/auth/google` route (see below)
  and, if the endpoints below need anything more than "authenticated", the `[Authorize(Roles: ...)]`
  checks for it.
- `POST /api/auth/google` is anonymous and calls Google's JWKS endpoint on every miss — include it
  in the per-IP rate limit when that limiter is added (see below).
- ~~Rate limiting per IP and per token, plus a hard daily cap for the public demo~~ — **deferred to
  task 09.** Decided with Harsh before implementation: this defends a public URL that does not exist
  until task 09, and his standing instruction after task 05's review was to build the MVP and defer
  hardening until the product works end to end. Task 09 owns the public demo and is where the daily
  cap gets a real number to be sized against.
- Global exception handler producing RFC 9457 `ProblemDetails` — no stack traces, no connection
  strings, no inner exception text
- Serilog with a correlation id per enquiry flowing through every log line, console sink in
  development and structured JSON in production
- On a provider 429, return `provider_rate_limited` cleanly

## Acceptance criteria

- [x] Every endpoint implemented; all `/api/*` require a valid token (auth itself is already done —
      see the note above; this criterion is now about the new endpoints actually sitting behind it)
- [x] SSE emits every `AgentEvent` variant the pipeline actually produces, verified by an integration
      test — `token` is the one declared variant no stage emits yet; see docs/SPEC.md §8 and the note
      below, not a task 07 gap
- [x] C# and TypeScript event types match, checked by eye and noted here — `src/QuoteDesk.Web/src/api/agentEvents.ts`
      mirrors `src/QuoteDesk.Agents/Pipeline/AgentEvent.cs` field-for-field, including the `token`
      variant that exists in the type but is not yet produced
- [x] ~~Rate limiter returns 429 with `ProblemDetails` under test~~ — deferred to task 09, see above
- [x] Every log line carries the correlation id
- [x] `provider_rate_limited` covered by a test with a stubbed 429
- [x] No `Console.WriteLine` anywhere in the solution

## Out of scope

Traces and metrics — task 10. The UI — task 08. Rate limiting — task 09 (see above). The `edit`
decision payload for `POST /api/approvals/{id}` — task 08, once the approval card exists to say what a
salesperson actually needs to change; `edit` returns 400 for now.

## Notes on completion

**Done:** `EnquiryPipeline` is reachable over HTTP behind the fallback auth policy. `POST
/api/enquiries/{id}/process` and `POST /api/approvals/{id}` both stream `AgentEvent`s as SSE through
one shared writer (`QuoteDesk.Api.Streaming.AgentEventStreamWriter`), which also persists the run's
full trace to a new `AgentRuns.TraceJson` column in a `finally` — proven end to end against the real
docs/DOMAIN.md worked example with a scripted `IChatClient`: process suspends at approval with the
spindle tape unresolved, `GET /api/approvals` lists it, `POST /api/approvals/{id}` with `approve`
resumes to a real `QTN-` number, `GET /api/quotes` lists it, and `GET /api/enquiries/{id}` replays the
persisted trace after the stream has closed. A stubbed 429 (`ClientResultException` with `Status =
429`, forced via a small test-only subclass since `Status`'s setter is `protected`) proves the clean
`provider_rate_limited` path. `Program.cs` now binds `LlmOptions` and fails fast on an empty
`Llm:ApiKey`, the same pattern `Auth:Google:ClientId` already used.

**Files that matter:** `src/QuoteDesk.Api/Streaming/AgentEventStreamWriter.cs`,
`src/QuoteDesk.Api/Enquiries/EnquiryEndpoints.cs`, `src/QuoteDesk.Api/Approvals/ApprovalEndpoints.cs`
(new), `src/QuoteDesk.Api/Quotes/QuoteEndpoints.cs` (new), `src/QuoteDesk.Api/Logging/CorrelationMiddleware.cs`
(new), `src/QuoteDesk.Api/Program.cs`, `src/QuoteDesk.Data/Repositories/AgentRunRepository.cs` +
`QuoteRepository.cs` (new `GetByIdAsync`/`AppendTraceAsync`/`ListAsync`),
`src/QuoteDesk.Data/Migrations/…AddAgentRunTrace`, `src/QuoteDesk.Web/src/api/agentEvents.ts` (new),
`tests/QuoteDesk.IntegrationTests/Api/AgentStreamEndpointTests.cs` (new),
`tests/QuoteDesk.IntegrationTests/Api/ScriptableChatClient.cs` (new),
`tests/QuoteDesk.IntegrationTests/Agents/WorkedExampleScript.cs` (new — the worked-example script
extracted out of `EnquiryPipelineTests` so both test classes share it instead of duplicating ~40 lines).

**Decisions made:** Three scope questions settled with Harsh before writing code — rate limiting to
task 09, the trace stored as one `TraceJson` column (not a separate table) appended to by
read-merge-rewrite across the suspend/resume boundary, and `POST /api/approvals/{id}` supporting only
`approve`/`reject` for now. `EnquiryPipeline.StoredApproval` was made `public` (was `private`) so the
Api layer can deserialize `AgentRuns.ApprovalRequestJson` without reimplementing that wire shape.
`CorrelationMiddleware` sits *before* `UseSerilogRequestLogging`, not after — that middleware logs its
"Request finished" summary only once every downstream middleware has returned, so the `LogContext`
scope has to still be open at that point, or the one log line that matters most would never carry the
id. `QuoteDeskApiFactory` now seeds its database (it only migrated before) and swaps `IChatClient` for
a new `ScriptableChatClient` — `StubChatClient` takes its script at construction, which doesn't fit a
DI singleton shared across many tests, so `ScriptableChatClient` wraps a replaceable one and adds
`ScriptThrow` for simulating a provider exception, which `StubChatClient` alone cannot do.

**Known gaps found, not fixed here:** `PriceExecutor`'s narration is non-streaming, so no pipeline
stage emits a `TokenEvent` — see docs/SPEC.md §8's "Resolved in task 07" note; a real fix means
extending `PriceExecutor` and the `StubChatClient` contract, which is bigger than this task's scope of
wiring the existing pipeline behind HTTP. Separately (found, not touched — pre-existing since task 05):
`QuoteWriteTools.CreateQuoteDraftAsync` hardcodes `ShipTo`/`RequiredBy` to `null` even though
`ApprovalRequest` has carried real values from the Extract stage since task 06; nothing in task 07
threads them through `ApproveExecutor` into the draft. Worth a fast follow.

**A live run against real `gemini-3.6-flash` (added `tests/QuoteDesk.Evals/GeminiWorkedExampleEval.cs`,
run once Harsh supplied a key mid-session) found the `thought_signature` protocol gap docs/SPEC.md §4
already documented for streaming also breaks the non-streaming path** the whole pipeline actually
runs: Extract succeeds, `resolve_customer` executes correctly, and the next turn — submitting that
tool result back to the model — fails with the same `400 INVALID_ARGUMENT thought_signature` error.
This is not a task 07 bug and not fixed here; see docs/SPEC.md §4's correction for full detail and
docs/SESSION-LOG.md for the "Blocked on Harsh" entry asking for a direction decision. A smaller,
already-fixed finding from the same run: the model didn't reliably format `requiredBy` as an ISO date
(`extract.md` and a new `LenientNullableDateOnlyConverter` fix this, both in this commit). The dev
database was also found two migrations behind (missing `AddAgentRuns`/`AddAgentRunTrace`) and has been
brought current.
