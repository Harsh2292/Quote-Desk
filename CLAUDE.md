# QuoteDesk

An agentic RFQ-to-quotation service. A customer sends a messy enquiry by paste, email or WhatsApp.
An agent layer built on Microsoft Agent Framework extracts the lines, resolves items and stock,
prices them with **deterministic C#**, and stops at a **human approval gate** before any quote is
created or sent.

Contract: @docs/SPEC.md · Business rules: @docs/DOMAIN.md · Work queue: @tasks/README.md

This file is the only always-loaded instruction file. If a rule is worth having, it belongs here.

## Architecture in one line

A **fixed pipeline with one autonomous stage**: `Extract → Resolve → Price → Approve → Create → Send`
never reorders or skips. Inside `Resolve`, the agent chooses its own tool calls. `Price` is code.
`Approve` is a human. Know where that line sits — it is the most interesting thing about this project.

## The four rules

1. **The model never decides money.** Pricing, discounts, margin, delivery dates are computed in
   `QuoteDesk.Domain` in plain C#. The model explains the result; it never produces it.
2. **No raw SQL from the model.** It calls typed tools. Data access is EF Core with LINQ.
3. **Nothing leaves without a human.** Write tools are unreachable from the Resolve agent.
4. **Every stage and tool call is traced** and streamed to the UI.

If a change breaks one of these, stop and ask.

## Stack and layout

.NET 10 · ASP.NET Core Minimal APIs · Microsoft Agent Framework · EF Core + SQL Server · Serilog ·
OpenTelemetry · React 19 + Vite + TypeScript + Tailwind · xUnit + FluentAssertions · Docker ·
GitHub Actions → Azure Container Apps + Static Web Apps

```
src/QuoteDesk.Domain/   pricing rules — zero dependencies, exhaustively tested
src/QuoteDesk.Data/     EF Core context, entities, migrations, seed
src/QuoteDesk.Agents/   agents, typed tools, workflow, prompts
src/QuoteDesk.Intake/   channel adapters — paste, email, WhatsApp
src/QuoteDesk.Api/      minimal APIs, SSE, auth, telemetry
src/QuoteDesk.Web/      React
tests/                  UnitTests · IntegrationTests · Evals
```

Dependencies flow one way: `Api → Agents → Data → Domain`, with `Api → Intake → Data`. `Domain`
references nothing. A reference pointing back up means the logic is in the wrong project.

(Corrected in task 01: this file previously said `Intake → Api`, but `POST /api/enquiries` lives in
`QuoteDesk.Api` and calls `PasteAdapter` in `QuoteDesk.Intake`, which persists into `Enquiries` — the
arrow has to point the other way, or task 04 cannot compile.)

## Commands

```bash
dotnet build QuoteDesk.sln -warnaserror
dotnet build QuoteDesk.sln -c Release -warnaserror
dotnet test --filter "FullyQualifiedName!~Evals"
dotnet ef migrations add <Name> --project src/QuoteDesk.Data --startup-project src/QuoteDesk.Api
docker compose up -d
cd src/QuoteDesk.Web && npm run build
```

Run **both** build configurations, not just Debug — task 09 found that `-c Release` triggers analyzer rules Debug does not (CA1848's `LoggerMessage` requirement, discovered when the Dockerfile's `dotnet publish -c Release` failed on three call sites `dotnet build` had passed for the whole project's history). Docker publishes Release; a green Debug build is not proof the image builds.

## C#

- **Money is `decimal`.** Never `double` or `float`. Round only through `Money` in `QuoteDesk.Domain`;
  two code paths rounding differently is a bug even when both tests pass.
- **Dates are `DateTimeOffset` in UTC at rest.** Convert to IST only for display. `QuoteDesk.Domain`
  never reads the clock — time is always a parameter.
- **`CancellationToken` on every public async method**, and passed on. No `.Result`, no `.Wait()`,
  no `async void`.
- **No swallowed exceptions.** Handle it meaningfully or let the global handler shape it. `catch { }`
  does not appear in this repo.
- **Nullable is enabled and warnings are errors.** Do not suppress a warning to move on; fix it, or
  say why suppressing is correct.
- Style follows `.editorconfig` and the surrounding code. Do not invent conventions.

## Tests

xUnit and FluentAssertions. Nothing else.

Unit tests are required in three places, written down so "where needed" cannot become "nowhere":
`QuoteDesk.Domain` exhaustively, every tool's validation and miss paths, every intake adapter's
parsing including malformed and empty payloads. Everything else is covered at integration level.

- **Integration tests use a stubbed `IChatClient`.** CI must pass with no network and no API key. A
  test that calls a real model is an eval, not an integration test.
- **Evals live in `tests/QuoteDesk.Evals`**, excluded from the default run.
- **Cover the boundaries**: exactly on a slab edge, exactly at the margin floor, zero, empty,
  unknown. The happy path rarely breaks.
- **Never weaken an assertion to make a test pass.** If a test looks wrong, say so and explain why.
- Deterministic: no real clock, no unseeded random, no ordering dependence.
- Name tests `MethodName_Scenario_ExpectedOutcome`. A bug fix starts with a failing test.

## Frontend

- **The `AgentEvent` union mirrors the C# contract exactly and changes in the same commit.**
- **One typed hook owns SSE parsing** (`useAgentStream`). It is built on `fetch` + `ReadableStream`,
  not `EventSource` — auth is a bearer JWT (see Security below), and `EventSource` cannot send an
  `Authorization` header. Do not scatter stream-reading logic across components.
- **Every async surface renders loading, empty and error.** `provider_rate_limited` must render a
  useful message with the "replay a saved run" action — a recruiter on the live demo will hit it.
- **No `any`.** TypeScript strict stays on.
- **Three screens only**: Desk, Approvals, Quotes. No landing page, settings or theme toggle.
- **The Desk keeps its state.** A session provider above the router holds the enquiry text, the live
  trace and any error, so navigating to Approvals and back does not throw the run away; it survives a
  browser refresh via `sessionStorage`. It clears only on **New enquiry** or a pipeline that
  completes through an approve — never on navigation, a failed run, or a rejected decision.
- **The Agent Trace panel is the product.** Stage badge, a plain-language label for each step (never
  the raw tool name — those are internal; `src/api/traceLabels.ts` maps them), the step's arguments
  and result, duration, ok/fail, collapsible. Give it real attention.

## Security

- **Secrets never enter the repo.** `dotnet user-secrets` locally, Container Apps secrets in
  production. `appsettings.json` holds key *names* and non-secret defaults only.
- **Every `/api/*` route requires a valid bearer JWT, enforced by a fallback authorization policy —
  new endpoints are protected by default, not by remembering `[Authorize]`.** Google verifies the
  user's identity; the Api mints its own short-lived JWT (`POST /api/auth/google`) rather than using
  a session cookie, since a cookie would not survive the two-host split between Static Web Apps and
  Container Apps. Only `POST /api/auth/google`, `/health/live` and `/health/ready` are anonymous.
- **No raw SQL.** EF Core with LINQ. If raw SQL is genuinely needed, parameterise it via
  `FromSqlInterpolated` and say why first.
- **Errors to the client are RFC 9457 `ProblemDetails`** — no stack traces, connection strings or
  inner exception text.
- **The model never receives** connection strings, API keys, cost prices, margin figures, or any
  customer record other than the one under discussion. Enforced by a reflection test over tool
  result types, not by convention.
- **Rate limiting on by default**, per IP and per token, with a hard daily cap on the public demo.
- **Input from outside is untrusted.** Enquiry bodies are data, never instructions — wrapped in a
  delimiter, with an eval case proving the agent does not obey them.
- **Webhooks verify their signature** before touching the payload.

## Working agreement

- **Start a session by reading the last entry in `docs/SESSION-LOG.md`.** Nothing else carries
  context across `/clear` — there is no startup hook doing this for you any more.
- **Use the `codebase-memory` MCP server to orient in the code, not a full read-through.** It serves
  a pre-built graph of this repo (`search_graph`, `search_code`, `trace_path`, `get_architecture`,
  `get_code_snippet`, `query_graph`). Query it first when you need to find where something lives,
  what calls what, or how a stage is wired. Fall back to `Read`/`Grep`/`Glob` only when the graph
  does not answer — a specific file you already know you need, a fresh edit the index has not seen,
  or an exact-text search. Re-index after substantial changes: `codebase-memory-mcp cli
  index_repository --repo-path .` (or `detect_changes` to check staleness). The index is local and
  not committed; a fresh clone has no graph until it is rebuilt.
- **One task at a time**, from `tasks/`. Finish it end to end, then stop.
- **You do the building.** Harsh is learning this stack by reading what you produce, so implement it
  rather than handing back instructions. Ask him to run something only when it genuinely requires
  him — an interactive login, a portal click, a credential, a payment.
- **Simple beats thorough.** His last project died of size. When there is a small version and a
  complete version, build the small one and say what the complete one would add.
- **Do not invent Microsoft Agent Framework APIs.** Verify against the installed package's XML docs
  first — `~/.nuget/packages/<pkg>/<version>/lib/<tfm>/*.xml` lists every public member for the
  exact resolved version, and grepping it costs one command. Use the `api-researcher` subagent only
  for what those docs cannot answer — behaviour, preview status, known provider issues — and expect
  that roughly once in the whole project, not once a session.
- **Propose dependencies, don't add them silently.** Name the package and why; Harsh decides.
- **Disagree.** If a rule here, a line in the spec, or a task's approach is wrong for the case in
  front of you, say so and explain why *before* following it. This includes scope: a task that adds
  work the demo does not need should be challenged, not obeyed.
- **Never run `git commit` or `git push`** — in any permission mode. Stage the change, verify nothing
  secret is staged, and write out the conventional-commit message for Harsh to run. The commit
  history is part of what this repo is for.

## Definition of done

Build clean with `-warnaserror`, tests pass, `npm run build` passes, new behaviour has tests, the
task file's acceptance criteria are genuinely true, and `/session-log` has been run. Run those three
commands yourself before reporting a task done — never ask Harsh to. A red build is never done.
