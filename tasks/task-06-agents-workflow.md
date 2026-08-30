# Task 06 — Agents and workflow

**Session 2 · depends on: 05**

## Goal

The pipeline runs end to end and stops at the approval gate. This is the heart of the project.

## Stack for this task

`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, `Microsoft.Agents.AI.Workflows`

## Before you write a line

**Confirm every Agent Framework API with the `api-researcher` subagent first.** Types, method
signatures, how workflow suspension and checkpointing actually work. This framework reached 1.0 in
April 2026 and recalled APIs are unreliable. Writing plausible code here is the failure mode that
costs a day.

## What to build

Four nodes. Note that only two of them involve a model:

```
Extract   agent node   reads the enquiry text → line items, ship-to, required-by, commercial asks
Resolve   agent node   AUTONOMOUS: chooses its own tool calls over the read registry
Price     code node    QuoteDesk.Domain computes; one short model call narrates the result
Approve   human node   workflow SUSPENDS here, state checkpointed
                       ↓ on approval
                       write tools execute
```

The stage sequence is fixed and never reorders. The autonomy lives inside `Resolve` and nowhere else.

Guardrails:

- Max 8 tool calls per run, then a forced summary
- Per-conversation token budget, returning a clean `budget_exceeded` rather than looping
- `Resolve` is constructed with the read registry only, so it physically cannot reach a write tool
- Enquiry text is wrapped in an untrusted-content delimiter stating the content is data, never
  instructions
- Prompts live as `.md` files in `Agents/Prompts/`, loaded at startup — not inline strings

**Streaming:** follow whatever task 00 recorded in `docs/SESSION-LOG.md`. If streaming plus tool
calls was broken on the Gemini compatibility layer, run the tool loop non-streaming and stream only
the final narration. The trace panel is driven by server-emitted `AgentEvent`s, so this changes
nothing the user sees.

## Acceptance criteria

- [x] Every Agent Framework API used was confirmed by `api-researcher`, with sources noted in the task
- [x] The worked example from `docs/DOMAIN.md` runs end to end and suspends at approval
- [x] The spindle-tape ambiguity surfaces as unresolved rather than being guessed
- [x] A suspended approval survives an application restart
- [x] Tool-call cap and token budget both enforced, with tests
- [x] Integration tests drive the full workflow through a **stubbed `IChatClient`** — no network
- [x] A test proves the Resolve agent cannot invoke `create_quote_draft`

## Out of scope

The HTTP surface, streaming, the UI.

## Notes on completion

**Done:** The full pipeline exists and runs — `Extract → Resolve → Price → [approval RequestPort] →
Approve` — behind one façade, `QuoteDesk.Agents.Pipeline.EnquiryPipeline`, with `StartAsync(enquiryId)`
and `ResumeAsync(enquiryId, decision)`. Verified against a stubbed `IChatClient` scripting the exact
`docs/DOMAIN.md` worked example: the model resolves the bearings and belt via real tool calls against
the seeded database (`resolve_customer`, three `search_catalog` calls, one `get_customer_history`
call), leaves the spindle-tape line unresolved (no guess), Price computes the real 8%/14% numbers via
`PricingTools` directly (never a model call), the run suspends at approval with a `pending_approval`
`AgentRun` row, and — in a dedicated test that builds a second, independent `EnquiryPipeline` sharing
nothing but the same SQL rows — resuming produces a real `QTN-` quote and marks it sent. A fourth test
proves the token budget: an artificially tiny budget on the very first model call yields a clean
`budget_exceeded` `ErrorEvent` and a `failed` `AgentRun`, nothing partially written.

**Files that matter:** `src/QuoteDesk.Agents/Pipeline/` (the whole pipeline — `EnquiryPipeline`,
`QuoteDeskWorkflow`, the four executors, `AgentEvent`, `TracedAIFunction`, `TokenUsageTracker`,
`ModelJson`), `src/QuoteDesk.Agents/Checkpointing/SqlCheckpointStore.cs`,
`src/QuoteDesk.Agents/Llm/` (`LlmOptions`, `ChatClientFactory`), `src/QuoteDesk.Agents/Prompts/`
(three embedded `.md` files + `PromptLibrary`), `src/QuoteDesk.Data/Repositories/AgentRunRepository.cs`
and `WorkflowCheckpointRepository.cs`, `tests/QuoteDesk.IntegrationTests/Agents/EnquiryPipelineTests.cs`
and `StubChatClient.cs`.

**API verification (CLAUDE.md: confirm before writing agent code).** Signatures came from grepping
the installed XML docs at `~/.nuget/packages/microsoft.agents.ai{,.abstractions,.workflows}/1.19.0/
lib/net10.0/*.xml` and `~/.nuget/packages/microsoft.extensions.ai{,.abstractions}/10.9.0/lib/net10.0/
*.xml` — one command each, per CLAUDE.md's standing instruction. Three genuinely behavioural questions
the docs could not answer went to the `api-researcher` subagent — more than the "roughly once" this
project otherwise budgets for, justified because this task's own text explicitly calls out "how
workflow suspension and checkpointing actually work" as something to confirm first, and each answer
changed how the code was written:
1. Whether `ChatClientAgent`/`AsAIAgent` auto-wraps a plain `IChatClient` with function-invocation
   behaviour, and how `FunctionInvokingChatClient.MaximumIterationsPerRequest` actually terminates.
   Answer (decompiled): yes, automatic (`WithDefaultAgentMiddleware` detects and reuses an existing
   `FunctionInvokingChatClient` rather than double-wrapping); the iteration cap terminates
   *gracefully* — the model is forced into a text-only final turn, exactly the "forced summary" this
   task asked for — never throws.
2. How to wire a `RequestPort` into a graph so the response reaches a **different** downstream node
   than the one that sent the request (`Price → port → Approve`, not `Price → port → Price`).
   Answer (decompiled + the framework's own `HumanInTheLoopBasic` sample and Microsoft Learn docs):
   plain `AddEdge` calls, both directions — `WorkflowBuilderExtensions.AddExternalCall` was a red
   herring, its decompiled body always wires the response back to the same source.
3. Whether resuming a checkpoint after a real process restart re-surfaces the still-pending
   `RequestInfoEvent`, or requires manually reconstructing an `ExternalResponse`. Answer (decompiled):
   it re-publishes automatically (`RepublishUnservicedRequestsAsync`, called the moment
   `ResumeStreamingAsync` returns) — `info.Request.CreateResponse(decision)` on the freshly-surfaced,
   re-deserialized `ExternalRequest` is exactly correct; no manual `TypeId`/`PortableValue` needed.

**Decisions made:**
- **`Microsoft.Agents.AI.OpenAI` was not added**, contradicting this file's own package list — see
  `docs/SPEC.md` §3 for why (building the agent from `IChatClient` instead makes the stubbed-client
  tests clean, and avoids pinning `Microsoft.Extensions.AI.OpenAI` back to an older version).
- **`price_quote` is withheld from the Resolve agent** — see `docs/SPEC.md` §7. Price calls
  `PricingTools` directly.
- **Structured output (`RunAsync<T>`) is not used anywhere.** Every model call asks for plain text
  and parses JSON out of it tolerantly (`ModelJson`, handling a ` ```json ` fence) — see `docs/SPEC.md`
  §4. Gemini's OpenAI-compat support for `response_format: json_schema` is unverified for this exact
  model, and `RunAsync<T>`'s own deserializer is not fence-tolerant regardless.
- **Approval is a real workflow suspension**, not a payload-only shortcut (Harsh's explicit choice
  when this task was planned) — `RequestPort.Create<ApprovalRequest, ApprovalDecision>` wired with
  plain edges, backed by `SqlCheckpointStore` (`ICheckpointStore<JsonElement>`) over a plain
  `IWorkflowCheckpointRepository` in `QuoteDesk.Data` — no `Microsoft.Agents.AI.Workflows` type
  crosses into `QuoteDesk.Data`, matching every other repository's framework-agnostic pattern.
- **`AgentEvent` is carried over the workflow's own event stream**, not a separate channel: a small
  `AgentTraceEvent : WorkflowEvent` wraps one `AgentEvent`, raised via `IWorkflowContext.AddEventAsync`
  from every executor and unwrapped by `EnquiryPipeline`. Simpler than a hand-rolled `Channel<T>`, and
  it is the framework's own designed mechanism for exactly this.
- **`ApproveExecutor` makes no model call at all** — once a human has decided, nothing is left to
  interpret. It calls `QuoteWriteTools` and `IQuoteRepository.MarkApprovedAsync` as plain C#, traced
  the same way `TracedAIFunction` traces a model-driven call, so the trace panel shows both uniformly.
- **`IQuoteRepository.MarkApprovedAsync` is new** — the `ApprovedByUserId`/`ApprovedAt` columns existed
  since task 05 but nothing wrote them until this task.

**A real concurrency bug, found and fixed via task 06's own integration tests, not by inspection:**
the workflow engine writes a checkpoint from its own background execution task while
`EnquiryPipeline`'s foreground code reacts to the resulting event on the *same request's* shared,
scoped `DbContext` — EF Core does not support that, and threw "a second operation was started on this
context instance" the first time the worked-example test actually ran. Fixed by giving
`WorkflowCheckpointRepository` its own `IDbContextFactory<QuoteDeskDbContext>`-sourced, short-lived
context per call instead of the shared scoped one every other repository uses (`AddQuoteDeskData` now
registers `AddDbContextFactory` plus an `AddScoped<QuoteDeskDbContext>` sourced from it, since
`AddDbContext` and `AddDbContextFactory` for the same `TContext` cannot coexist — their default
lifetimes conflict). Documented in `docs/SPEC.md` §6.

**Known gaps:** `QuoteDesk.Api`'s `Program.cs` is untouched — `AddQuoteDeskAgentPipeline` (the new
extension method that wires the whole pipeline plus `LlmOptions`) exists and is exercised directly by
the integration tests, but nothing in the running Api calls it yet; task 07 does that, decides whether
a missing `Llm:ApiKey` should fail fast, and adds the SSE endpoint. `GET /api/approvals` (reading
`AgentRuns` where `Status = pending_approval`) has a repository method (`GetPendingApprovalsAsync`)
but no endpoint yet — also task 07. No live call against the real `gemini-3.6-flash` endpoint was made
in this session (Harsh has not yet run `dotnet user-secrets set "Llm:ApiKey"`); everything above is
proven against a stubbed `IChatClient` only, per CLAUDE.md's rule that integration tests must pass with
no network and no key. That live run is worth doing once the key is set, specifically to settle the
one thing decompilation cannot: whether `gemini-3.6-flash` accepts the exact tool-call argument shapes
`AIFunctionFactory`-generated tools expect.

**Blocked on Harsh:** One command, a credential, so it has to be him:
`dotnet user-secrets set "Llm:ApiKey" "<gemini key>" --project src/QuoteDesk.Api`. Not required for
anything in this task's own acceptance criteria — only for the live end-to-end run above, whenever
that's next convenient.

**Verified before calling this done:** `dotnet build QuoteDesk.sln -warnaserror` — 0 warnings, 0
errors. `dotnet test --filter "FullyQualifiedName!~Evals"` — 151/151 passing (117 unit, 34
integration; up from 124 at the end of task 05). `cd src/QuoteDesk.Web && npm run build` — passes
unchanged (task 06 touched no frontend code).

**Next:** Task 07 — API, streaming, auth, logging. Wires `EnquiryPipeline` behind
`POST /api/enquiries/{id}/process` (SSE) and `POST /api/approvals/{id}`, binds `LlmOptions` in
`Program.cs`, and decides the fail-fast question this task deliberately left open.
