# QuoteDesk

An agentic RFQ-to-quotation service. A customer sends a messy enquiry by paste, email or WhatsApp.
An agent layer built on Microsoft Agent Framework extracts the lines, resolves items and stock,
prices them with **deterministic C#**, and stops at a **human approval gate** before any quote is
created or sent.

Contract: @docs/SPEC.md · Business rules: @docs/DOMAIN.md · Work queue: @tasks/README.md

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

## Stack

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

Dependencies flow one way: `Api → Agents → Data → Domain`, with `Intake → Api`. `Domain` references
nothing. Never point a reference back up.

## Commands

```bash
dotnet build QuoteDesk.sln -warnaserror
dotnet test --filter "FullyQualifiedName!~Evals"
dotnet ef migrations add <Name> --project src/QuoteDesk.Data --startup-project src/QuoteDesk.Api
docker compose up -d
cd src/QuoteDesk.Web && npm run build
```

## Who runs what

Harsh types exactly four things: `/task NN` to start work, `Shift + Tab` for plan mode, `/context`,
and `/clear`. **Everything else is yours to run without being asked** — `/verify-all` at the end of
every task, `/session-log` when a task closes and at the end of every session, the `dotnet-reviewer`
subagent on a finished diff, and `api-researcher` before touching an unfamiliar API. If you find
yourself about to write "please run /verify-all", run it instead.

`/task` and `/adr` are the only skills Harsh invokes, because starting work and recording a decision
are his calls, not yours.

## Working agreement

- **One task at a time**, from `tasks/`. Finish it end to end, then stop.
- **Plan mode** before writing code in `QuoteDesk.Domain` or `QuoteDesk.Agents`.
- **Do not invent Microsoft Agent Framework APIs.** Confirm with the `api-researcher` subagent first.
  "I need to check this" is the correct answer when you are unsure.
- **Propose dependencies, don't add them silently.** Name the package and why; I decide.
- **Disagree with me.** If a rule here, a line in the spec, or a task's approach is wrong for the case
  in front of you, say so and explain why *before* following it. These rules exist to prevent known
  failures, not to stop you thinking. A rule you follow into a bad outcome helps nobody.
- Secrets via `dotnet user-secrets` locally, Container Apps secrets in prod. Never in the repo.
- One commit per task, conventional message.

## Definition of done

Build clean, tests pass, `npm run build` passes, new behaviour has tests, the task file's acceptance
criteria are genuinely true, and `/session-log` has been run. A red build is never done.
