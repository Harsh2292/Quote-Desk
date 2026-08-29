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

- Google OpenID Connect on every `/api/*` route — sign in with Google, no seeded credential, no
  password, still **no user system to build** (changed from the original JWT-bearer plan; see
  docs/SPEC.md §3). Harsh creates the OAuth client in Google Cloud Console when this task starts;
  the client secret goes to `dotnet user-secrets`, never the repo.
- Rate limiting per IP and per token, plus a hard daily cap for the public demo
- Global exception handler producing RFC 9457 `ProblemDetails` — no stack traces, no connection
  strings, no inner exception text
- Serilog with a correlation id per enquiry flowing through every log line, console sink in
  development and structured JSON in production
- On a provider 429, return `provider_rate_limited` cleanly

## Acceptance criteria

- [ ] Every endpoint implemented; all `/api/*` require a valid token
- [ ] SSE emits every `AgentEvent` variant, verified by an integration test
- [ ] C# and TypeScript event types match, checked by eye and noted here
- [ ] Rate limiter returns 429 with `ProblemDetails` under test
- [ ] Every log line carries the correlation id
- [ ] `provider_rate_limited` covered by a test with a stubbed 429
- [ ] No `Console.WriteLine` anywhere in the solution

## Out of scope

Traces and metrics — task 10. The UI — task 08.

## Notes on completion
