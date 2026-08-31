# Session log

Append-only handover notes. Context does not survive between Claude Code sessions — this file is what
does. Newest at the bottom. Written with `/session-log`.

---

## 2026-08-26 — Project set up, no code yet

**Done:** Repository scaffolding only — CLAUDE.md, seven path-scoped rules, four skills, three
subagents, four hooks, the docs, and eleven task files. No solution, no projects, no code.

**Files that matter:** `tasks/README.md` is the work queue and the only place status lives.
`docs/SPEC.md` is the contract. `docs/DOMAIN.md` holds the business rules and the worked example
every test validates against.

**Decisions made:**
- RFQ-to-quotation chosen because the loop closes on itself — an enquiry arrives, a quotation leaves,
  nothing obviously missing around it.
- Architecture is a **fixed pipeline with one autonomous stage**. Extract → Resolve → Price → Approve
  never reorders. Autonomy lives only inside Resolve. Price is C#. This is deliberate and is the main
  thing to be able to explain.
- **EF Core, not Dapper** — more common in job descriptions, and migrations are a demonstrable thing.
  `AsNoTracking()` on all reads; entities never leave `QuoteDesk.Data`.
- Pricing lives in a dependency-free `QuoteDesk.Domain` so it can be shown to be untouched by the LLM.
- Intake is one `IncomingEnquiry` record with three adapters, shipped in risk order: paste, email
  (IMAP), WhatsApp (Twilio sandbox). Meta Cloud API is optional and off the critical path.
- **Audio is not transcribed.** Voice notes are stored and played; the enquiry is marked
  `needs_manual_entry`. Images may be read by a multimodal model on the gemini profile, optionally,
  in task 09.
- **Gemini is the default provider** (Harsh already has a key). ~1000+ requests/day, enough for the
  public demo and for repeated eval runs, and it reads images so one key covers every channel.
  GitHub Models stays configured as a fallback and a control. Azure is the deploy target only —
  free-tier subscription, no Azure OpenAI.
- Task 00 added: a throwaway twenty-minute spike proving tool calling works on the Gemini key before
  anything depends on it. If streaming plus tools is broken there, the fallback is to run the tool
  loop non-streaming and stream only the final narration — the trace panel is driven by server
  AgentEvents, not model tokens, so the UI is unaffected.

**Known gaps:** Everything. Start at task 01.

**Blocked on Harsh:** Nothing. The Gemini key and the Azure subscription are already in hand. A
mailbox with an app password is needed for the email adapter in task 09, and a Twilio account for
WhatsApp in the same task — neither blocks anything before then, and task 09 ships email first
deliberately.

**Next:** Task 00 — environment, repo and provider spike. This is the only command needed to start:
`/task 00`.

---

## 2026-08-29 — Task 00 (parts 1–3 done, 4–5 outstanding)

**Done:** The plan was read and challenged; 22 findings raised and four decisions taken (below).
Repo initialised, 44 files in one local commit `57b55e3 chore: project setup`. **Not pushed** — see
"Blocked". Hooks made executable and the exec bit forced into the git index with
`git update-index --chmod=+x`, since Windows leaves `core.filemode` unset and the plain `chmod`
recorded nothing. Added `.gitattributes` because `core.autocrlf=true` would hand the hook scripts
CRLF endings on re-checkout and break bash.

**Files that matter:** `docs/SPEC.md` §7 and `docs/DOMAIN.md` — both need the schema edits agreed
today and not yet written. `tasks/task-00-environment.md` still reads `todo`.

**Decisions made:**
- **Schema gaps get fixed before task 02**, not after. SPEC §7 cannot store the worked example's
  approved outcome: `Quotes` needs `ShipTo`/`RequiredBy`/`Freight`/`ValidUntil`; `QuoteLines` needs
  `RequiresOverride`/`MarginShortfallPct`/`DispatchDate`/`DeliveryDate`; `Customers` needs
  `ShipToZone`; and `EnquiryAttachments`, `Approvals` and `WorkflowCheckpoints` do not exist at all.
- **`PriceRules` holds quantity slabs only**, loaded by a repository and passed into
  `SlabDiscountPolicy` as a parameter — Domain has zero references and cannot read a database. Tier
  percentages, the 10% margin floor, 18% GST, 15-day validity, freight zones and the holiday calendar
  are constants in Domain. Cost accepted: changing a holiday is a code change, not a data edit.
- **The spike also probes workflow suspend/resume**, not just tool calling. That risk was otherwise
  untested until task 06.
- **Never run `git commit` or `git push`** — stage and propose the message; Harsh commits. Applies in
  auto mode too. The history is part of the portfolio.

**Agent Framework, confirmed by `api-researcher`:** packages are GA and target `net10.0`;
`Microsoft.Agents.AI` and `.OpenAI` are both **1.19.0**, so SPEC §4's "1.5.0" is stale. SPEC §5's
`AsAIAgent` snippet is correct as written. Tools attach per agent instance via
`ChatClientAgentOptions.ChatOptions.Tools`, so two disjoint registries genuinely work. Human-in-the-loop
is first-class: `WorkflowBuilder` + `RequestPort.Create<TReq,TResp>` + `RequestInfoEvent` +
`handle.SendResponseAsync`. Checkpointing ships `FileSystemJsonCheckpointStore` and `CosmosCheckpointStore`
but **no SQL store** — task 06 should implement `ICheckpointStore<JsonElement>` over the
`WorkflowCheckpoints` table. Rehydration needs a stable `ChatClientAgentOptions.Id` per agent.
`RunStreamingAsync` yields message-level `AgentRunResponseUpdate`s, **not token deltas**, so SPEC §9's
`token` event will be chunkier than the UI design assumes. No official `IChatClient` test double exists;
task 06 writes its own fake.

**Known gaps:** `jq` is **not installed**, so `guard-paths.sh` and `guard-bash.sh` both `exit 0` and
protect nothing — SETUP.md's "hooks are law" is false until it is. Docker daemon not running. The
Gemini key is set nowhere. Parts 4 and 5 of task 00 are untouched: no spike has run, so the streaming
verdict is still unknown, and the SPEC/DOMAIN edits above are still unwritten.

**Blocked on Harsh:** Install `jq` (`winget install jqlang.jq`) and restart Claude Code. Start Docker
Desktop. Decide whether local commit `57b55e3` stays or is undone with `git reset --soft HEAD~1`.
Set the Gemini key via `dotnet user-secrets` once the spike project exists.

**Next:** Finish task 00 — parts 4 and 5. The spike settles the one genuine unknown (tool calling on
Gemini, streaming and not) before anything depends on it.

## 2026-08-29 — Setup simplification (no task number)

**Done:** Cut the Claude Code configuration from 19 files to 4 and the instruction docs from ~1,600
lines to ~700. All hooks removed at Harsh's instruction — the two guard hooks were inert anyway
(no `jq`), and `settings.json`'s `deny` list enforces the same protections through the harness,
which cannot fail open. `.claude/rules/` deleted entirely; its content merged into `CLAUDE.md`,
which is now the single always-loaded instruction file. `dotnet-reviewer` and `spec-auditor`
removed in favour of the built-in `/code-review`. `/verify-all` and `/adr` removed — the three
verification commands are written into `CLAUDE.md` and the task skill directly. `SETUP.md` deleted
(it claimed "hooks are law", which was false).

**Deploy moved from task 11 to task 09.** Channels became 10; observability, evals and the README
became 11. `SPEC.md` trimmed to what sessions 1–2 actually build.

**Files that matter:** `CLAUDE.md` (everything always-loaded now lives here), `tasks/README.md`
(new order and the reasoning), `tasks/task-09-deploy.md` (new).

**Decisions made:** Ship the paste path to a public URL as soon as it works, before channels,
telemetry or evals. The previous project (WebLibrary) was never finished, so momentum risk outranks
completeness. Process and scaffolding count as scope and were cut on the same grounds.

**Known gaps:** No `.cs` file exists yet. Docker daemon not running. The Gemini key is set nowhere.
Task 00 parts 4 and 5 are untouched — no spike has run, so the streaming verdict is still unknown.
`jq` is no longer needed by anything.

**Blocked on Harsh:** Start Docker Desktop. Have the Gemini API key ready for `dotnet user-secrets`
once the spike project exists. Decide whether local commit `57b55e3` stays or is undone.

**Next:** Task 00, parts 4 and 5 — the provider spike. It settles the one genuine unknown (tool
calling on Gemini, streaming and not) before anything depends on it.

## 2026-08-29 — Task 00, parts 1–3 (still `in progress`)

**Done:** Machine verified green — .NET SDK 10.0.302, Node v24.16.0, Docker 29.6.1 with the daemon
**running** (it was down last session), git 2.50.1, `dotnet-ef` 10.0.8 already installed. Nothing is
missing; no installs are outstanding.

Gemini key validated against `https://generativelanguage.googleapis.com/v1beta/openai/` — HTTP 200
on `GET /models`. The account reaches far more than the spec assumed: `gemini-2.5-flash`/`-pro`,
`gemini-3.5-flash`, `gemini-3.6-flash`, `gemini-3.1-pro`. Recorded in SPEC.md §4, along with the rule
to pin an exact model id rather than the `gemini-flash-latest` alias, which would break eval
reproducibility.

Spike project scaffolded at `<scratchpad>/spike` — .NET 10 console, `OpenAI` **2.13.0**. Outside the
repo, so it cannot be committed by accident. `Program.cs` is not written yet.

**Files that matter:** `docs/SPEC.md` §4 (verified provider findings), `tasks/task-00-environment.md`
(parts 4–5 remain), `CLAUDE.md`.

**Decisions made:** API signatures are verified by grepping the installed package's XML docs under
`~/.nuget/packages/`, not by spawning the `api-researcher` subagent — the docs are exact for the
resolved version and cost one command. The subagent is now reserved for behavioural questions
(can a workflow resume after a process restart, preview status, provider quirks), expected once in
the whole project. `CLAUDE.md` and the task skill were updated to say so.

**Known gaps:** **The provider spike has not run.** The streaming-plus-tool-calls verdict is still
unknown — the one genuine unknown in the project. No model id chosen yet. No `.cs` file in the repo.

**Blocked on Harsh:** Nothing. The key works and the machine is ready.

**Next:** Task 00 parts 4 and 5 — write the spike `Program.cs`, run it non-streaming and streaming,
record the verdict, delete the spike. Roughly twenty minutes, and task 01 should not start before it.

**Note:** the API key was pasted into the chat transcript. Rotate it in Google AI Studio once the
demo is finished.

## 2026-08-29 — Task 00 done

**Done:** Environment fully verified (.NET 10.0.302, Node 24.16, Docker 29.6.1 daemon up, git,
`dotnet-ef`). Provider spike ran and answered the one real unknown: on the Gemini OpenAI-compat
layer, non-streaming tool calls work end to end; streaming tool calls fail with `400` because
Gemini's 3.x thinking models require a `thought_signature` on replayed function-call parts that the
OpenAI wire schema has no field for — confirmed with a raw curl repro, so it's a real protocol gap,
not our bug. Task 00 is `done`.

**Files that matter:** `docs/SPEC.md` §4 (pinned model, full verdict and reasoning),
`tasks/task-00-environment.md` (Notes on completion).

**Decisions made:** Pin `gemini-3.6-flash` — `gemini-2.5-flash` (the id SPEC.md originally assumed)
is `404` for new keys, retired by Google. Tool-calling loop in `QuoteDesk.Agents` runs non-streaming
everywhere; only the closing narration streams. No architecture change — the trace panel already
runs on server-emitted `AgentEvent`s, not model tokens, so this was the fallback SPEC.md already
planned for.

**Known gaps:** No `.cs` file exists yet — task 01 is the first real code. Spike deleted, nothing
left in the scratchpad.

**Blocked on Harsh:** Nothing.

**Next:** Task 01 — setup and skeleton. Build the solution and the six projects per `CLAUDE.md`'s
layout, wired `Api → Agents → Data → Domain`, `Intake → Api`, before any real logic.

## 2026-08-29 — Tasks 01 + 03 + 02 done (built together, deliberately)

**Done:** Solution builds clean under `-warnaserror` (5 source + 3 test projects, `Api → Agents →
Data → Domain` and `Api → Intake → Data`). SQL Server runs via `docker-compose.yml` and is migrated.
Every pricing rule in `docs/DOMAIN.md` is implemented in `QuoteDesk.Domain` and proven — including a
single test reproducing the Shreeji Textiles worked example exactly (8% discount, 14% margin, belt
missing its date). The database is seeded deterministically (25 customers, 262 catalogue items, 1,200
order-history rows, 12 enquiries) with all six deliberate cases individually queryable. Vite/React
health-check page confirmed working end to end against the live Api through the dev-server proxy. 35
unit tests + 14 real-container integration tests, all passing.

**Files that matter:** `CLAUDE.md` (dependency-graph line, corrected), `docs/DOMAIN.md` ("The
numbers, filled in by task 03"), `src/QuoteDesk.Data/Seed/DeterministicSeeder.cs`.

**Decisions made:** Built 01+03+02 in one sitting, not separately — justified because all three are
foundation with zero external unknowns, and task 03 was done *before* 02 (its own "depends on 02" is
wrong: Domain has no references at all). Google OpenID Connect replaces the JWT-bearer plan for task
07 (Harsh's instruction; `docs/SPEC.md` §3 and task 07 updated). Docker Desktop's containerd
snapshotter was breaking every pull from `mcr.microsoft.com`; fixed by disabling it in Docker
Desktop's own settings — a machine-local toggle, not a repo change.

**Known gaps:** Seed data is 262 catalogue items / 16 price rules, not the spec's ~300 / ~40 —
documented as a deliberate simplification in task 02's Notes on completion. `IEnquiryRepository` is
read-only; task 04 adds the write path.

**Committed:** `142d0c3` on `development`, local only (not pushed). Re-verified build/tests
immediately before committing — still 0 warnings, 49/49 tests passing. `QuoteDesk.Web`'s
`node_modules` and `dist` are gitignored as usual; nothing else was left uncommitted.

**Machine state for next session:** SQL Server container (`quotedesk-sql`) is up and seeded — no
need to re-run `docker compose up -d` or the `--seed` flag unless the volume is removed. To browse
the data directly (SSMS / Azure Data Studio / VS Code's mssql extension): server `localhost,1433`,
SQL auth, login `sa`, password `QuoteDesk!Local1` (the `docker-compose.yml` local-dev default, not a
real secret), check "Trust server certificate".

**Blocked on Harsh:** Nothing.

**Next:** Task 04 — intake abstraction and paste adapter.

## 2026-08-29 — Task 04a: Google sign-in and a Users table

**Done:** React gets a Google ID token and posts it to `POST /api/auth/google`; the Api verifies it
against Google, auto-provisions a `Users` row keyed on the `sub` claim, and mints its own bearer JWT.
Every route requires that token by default via a fallback authorization policy — `/health/*` and
`/api/auth/google` are the only exceptions. Verified live: `/health/live` → 200, `/api/auth/me` with
no token → 401, `/api/auth/google` with a bad token → 401 with a real Google JWKS round-trip and no
exception text in the body. **Also verified through an actual browser**: Harsh signed in with a real
Google account at `localhost:8080`, and the `Users` row landed correctly — `pharshin29@gmail.com`,
role `admin` (matched from `Auth:AdminEmails`), `CreatedAt == LastLoginAt` on first sign-in.

**Files that matter:** `src/QuoteDesk.Api/Program.cs` (auth wiring, the fallback-policy line),
`src/QuoteDesk.Api/Auth/`, `tests/QuoteDesk.IntegrationTests/Api/QuoteDeskApiFactory.cs`.

**Decisions made:** Pulled forward from task 07 so every endpoint from task 04 on is protected by
construction. Bearer JWT, not a cookie — the prod split between Static Web Apps and Container Apps
would break cookie auth, and `EventSource` (needed for SSE) can't send an `Authorization` header
either way, so `useAgentStream` will use `fetch`+`ReadableStream` instead (CLAUDE.md updated).
`docs/SPEC.md` §3/§9 and `tasks/task-07-api.md` updated in the same commit — this expands "no user
system to build" by exactly one auto-provisioned table, nothing more.

**Known gaps:** `WebApplicationFactory`'s `ConfigureAppConfiguration` is ineffective here — Program.cs
reads config before `Build()` — so the test factory uses environment variables instead; found and
fixed before it could touch the real dev database. No rate limit on `/api/auth/google` yet (task 07).

**Blocked on Harsh:** Nothing. (`.claude/settings.json`'s `Read(./.env.*)` deny rule blocked writing
`.env.example` — narrowed to `Read(./.env)` + `Read(./.env.local)` on his explicit instruction;
`.env.example` now exists, `src/QuoteDesk.Web/.env.local` holds the real Client ID he created by hand.)

**Next:** Task 04 — intake abstraction and paste adapter.

## 2026-08-29 — Tasks 04 + 05: intake, paste adapter, and the seven typed tools

**Done:** `POST /api/enquiries` stores a pasted enquiry (behind the task 04a auth policy) via
`QuoteDesk.Intake`'s `PasteAdapter`, blank-body-with-attachments correctly landing on
`needs_manual_entry`. All seven tools from docs/SPEC.md §7 now exist in `QuoteDesk.Agents.Tools` as
plain, fully-tested C# — `search_catalog` resolves "25mm PU belt" cleanly and comes back ambiguous
for "ring frame spindle tape" + "thicker" across all eight seeded thicknesses;
`price_quote` reproduces the worked example's 8%/14% exactly; `create_quote_draft`/`send_quote`
round-trip a quote to `sent` with a `QTN-` number. No agent or LLM call exists yet — these are still
ordinary methods with tests, called directly.

**Files that matter:** `src/QuoteDesk.Agents/Tools/` (all seven tools + both registries),
`src/QuoteDesk.Intake/PasteAdapter.cs`, `docs/SPEC.md` §6/§7 (three signature corrections, explained
inline), `tasks/task-04-intake.md` and `task-05-tools.md` Notes on completion (full detail).

**Decisions made:** `search_catalog` returns `CatalogSearchResult` not `CatalogMatch[]`;
`price_quote` takes `int? customerId` for the unknown-sender case; `create_quote_draft` returns a
typed `QuoteDraftResult`. `MarginShortfallPct` is never carried past `QuoteDesk.Domain` — only the
`RequiresOverride` bool reaches any tool result or column. `[AIFunctionName]` avoided (marked
`[Experimental("MEAI001")]` in `Microsoft.Extensions.AI.Abstractions` 10.9.0); tool names set via
`AIFunctionFactoryOptions.Name` instead. Two xUnit test classes hitting the same fixed test-database
name in separate `IClassFixture`s race each other under parallel execution — fixed twice this
session (Api tests, then Repository tests) by sharing one `[Collection(...)]`; **any new test class
using `QuoteDeskApiFactory` or `RepositoryFixture` must join the existing collection, not declare its
own `IClassFixture`.**

**Known gaps:** `Quotes.ShipTo`/`RequiredBy` stay null — no Extract stage exists yet to populate them
(task 06). `EnquiryAttachment` is a shape only, no table — task 10 adds storage when a real channel
can attach a file. No agent, workflow, or prompt exists — task 06 is the first place an LLM is called
outside the task 00 spike.

**Blocked on Harsh:** Nothing.

**Next:** Task 06 — agents and workflow. Wires `ReadToolRegistry` to an actual `AIAgent` for the
Resolve stage; everything it will call already exists and is proven.

## 2026-08-29 — Post-task-05 review: closed a real error-handling gap

**Done:** Harsh asked for a review pass before task 06, specifically about exception handling. Found
`QuoteDesk.Api` had **no global exception handler at all** — any unhandled exception (a DB hiccup, a
malformed request) fell through to ASP.NET Core's raw error response instead of the RFC 9457
`ProblemDetails` CLAUDE.md's Security section requires, and in Development/test it leaked a full
stack trace. Reproduced concretely: two Google sign-ins with the same email but different subjects
throws `DbUpdateException` uncaught by `AuthEndpoints`, which used to 500 with exception text in the
body. Fixed with `builder.Services.AddProblemDetails()` + `app.UseExceptionHandler()` as the first
middleware in the pipeline, and locked in with a new regression test that reproduces exactly that
trigger and asserts the response body contains no exception type, stack frame, or index name. Also
added two missing `ArgumentNullException.ThrowIfNull` guards (`CatalogTools.SearchCatalogAsync`'s
`query`/`hints`, `CustomerTools.ResolveCustomerAsync`'s `senderId`) — the only two tool parameters
touched directly (string concatenation, `.IndexOf`) before any null check, unlike every other
array/object parameter this session, which all already guarded consistently.

**Files that matter:** `src/QuoteDesk.Api/Program.cs` (the two new lines),
`tests/QuoteDesk.IntegrationTests/Api/GlobalExceptionHandlingTests.cs`.

**Decisions made:** Harsh confirmed mid-review: build the MVP only for now, defer further
production-grade hardening (rate limiting, deeper input validation, etc.) until after it works end to
end — matching the standing `completion-over-sophistication` preference. This exception-handler fix
was treated as in-scope regardless, since it is an explicit CLAUDE.md rule already in force, not new
hardening; no further defensive-programming pass was done beyond the two guards above.

**Known gaps:** Same as the entry above — nothing new introduced by this review. 124/124 tests
passing (94 unit + 30 integration), 0 warnings under `-warnaserror`.

**Blocked on Harsh:** Nothing.

**Next:** Task 06 — agents and workflow, unchanged from above.

## 2026-08-29 — Task 06: agents and workflow

**Done:** The full pipeline runs — Extract → Resolve → Price → suspend at a real `RequestPort` →
Approve — behind `EnquiryPipeline.StartAsync`/`ResumeAsync`. Proven against a stubbed `IChatClient`
scripting the exact docs/DOMAIN.md worked example: real tool calls against the seeded DB resolve the
bearings and belt, the spindle tape stays unresolved (no guess), Price computes the real 8%/14% in
plain C# (never a model call), the run suspends with a `pending_approval` `AgentRun` row, and — via a
second, independent `EnquiryPipeline` sharing only the SQL rows — resuming produces a real `QTN-`
quote. A token-budget test proves a clean `budget_exceeded` with nothing partially written.

**Files that matter:** `src/QuoteDesk.Agents/Pipeline/` (`EnquiryPipeline`, `QuoteDeskWorkflow`, four
executors), `src/QuoteDesk.Agents/Checkpointing/SqlCheckpointStore.cs`,
`tests/QuoteDesk.IntegrationTests/Agents/EnquiryPipelineTests.cs`, `docs/SPEC.md` §3/§4/§6/§7,
`tasks/task-06-agents-workflow.md` Notes on completion (full API-verification detail).

**Decisions made:** `Microsoft.Agents.AI.OpenAI` deliberately not added (build the agent from
`IChatClient` instead — keeps stub-based tests clean). `price_quote` withheld from the Resolve agent;
Price calls `PricingTools` directly. No structured output (`RunAsync<T>`) anywhere — Gemini's
`json_schema` support is unverified, so every call parses fence-tolerant JSON from plain text
(`ModelJson`) uniformly. Real workflow suspension via `RequestPort`, wired with plain `AddEdge` calls
(not `AddExternalCall`, which always loops back to its own source — decompiled and confirmed wrong for
this shape). Three behavioural questions went to `api-researcher` (auto function-invocation and
iteration-cap semantics; routing a `RequestPort`'s response to a different node; resume republishing
the pending request after a restart) — more than the project's usual "once," justified because this
task's own text calls out checkpointing semantics as something to confirm first.

**Known gaps:** `Program.cs` untouched — `AddQuoteDeskAgentPipeline` exists and is proven by tests but
nothing in the running Api calls it; task 07 wires the SSE endpoint and decides whether a missing
`Llm:ApiKey` should fail fast. No live call was made against real `gemini-3.6-flash` — everything is
proven against a stub, per CLAUDE.md. Found and fixed a real concurrency bug along the way: the
workflow's background checkpoint-write and the caller's own DB reaction can't share one scoped
`DbContext` — `WorkflowCheckpointRepository` now uses its own `IDbContextFactory`-sourced context.

**Blocked on Harsh:** One command, whenever convenient (not required for this task's own criteria):
`dotnet user-secrets set "Llm:ApiKey" "<gemini key>" --project src/QuoteDesk.Api` — needed only for a
live end-to-end run against the real model, which would settle whether `gemini-3.6-flash` accepts the
tool-call argument shapes `AIFunctionFactory` expects.

**Next:** Task 07 — API, streaming, auth, logging. Wires `EnquiryPipeline` behind
`POST /api/enquiries/{id}/process` (SSE) and `POST /api/approvals/{id}`, binds `LlmOptions` in
`Program.cs`.

## 2026-08-30 — Task 07: API, streaming, auth, logging

**Done:** `EnquiryPipeline` is reachable over HTTP behind the existing auth policy. `POST
/api/enquiries/{id}/process` and `POST /api/approvals/{id}` (approve/reject) both stream `AgentEvent`s
as SSE through one shared writer that also persists the run's full trace. `GET /api/enquiries/{id}`,
`GET /api/approvals`, `GET /api/quotes`, `GET /api/quotes/{id}` all implemented. Proven end to end
against the real docs/DOMAIN.md worked example with a scripted `IChatClient`: process suspends at
approval with the spindle tape unresolved, approving resumes to a real `QTN-` quote, and the trace
replays after the stream closes. A stubbed 429 proves `provider_rate_limited`. 156/156 tests pass
(117 unit + 39 integration), 0 warnings under `-warnaserror`, `npm run build` clean.

**Files that matter:** `src/QuoteDesk.Api/Streaming/AgentEventStreamWriter.cs` (the one place SSE
framing exists), `src/QuoteDesk.Api/Approvals/` and `Quotes/` (new), `src/QuoteDesk.Data/Migrations/
…AddAgentRunTrace`, `tests/QuoteDesk.IntegrationTests/Api/AgentStreamEndpointTests.cs`.

**Decisions made:** Three scope questions settled with Harsh before coding — rate limiting deferred to
task 09 (defends a URL that doesn't exist yet), the trace stored as one `AgentRuns.TraceJson` column
appended by read-merge-rewrite, and approvals support only approve/reject (`edit` returns 400 until
task 08 defines that payload). Full detail and reasoning in tasks/task-07-api.md's Notes on completion.

**Known gaps:** No pipeline stage emits a `token` SSE event — Price's narration runs non-streaming
(docs/SPEC.md §8 records this as a real task-06 gap, not fixed here; bigger than task 07's scope).
Separately, `QuoteWriteTools.CreateQuoteDraftAsync` still hardcodes `ShipTo`/`RequiredBy` to null even
though `ApprovalRequest` has carried real values since task 06 — found, not touched.

**Live-Gemini finding, significant:** Harsh supplied a real key mid-session; the first-ever live run
of the real pipeline (`tests/QuoteDesk.Evals/GeminiWorkedExampleEval.cs`, dev DB migrated to catch up
first — it was 2 migrations behind) surfaced that the `thought_signature` protocol gap docs/SPEC.md
already documented for *streaming* also breaks the **non-streaming** path: Extract succeeds,
`resolve_customer` executes and returns a real result, and the very next turn — submitting that result
back to the model — fails with the same `400 INVALID_ARGUMENT thought_signature` error. Task 00's
spike verified non-streaming against one hand-rolled round trip, not the real `ChatClientAgent` /
`FunctionInvokingChatClient` loop `ResolveExecutor` runs. **The whole Resolve stage cannot currently
complete against real `gemini-3.6-flash`.** docs/SPEC.md §4 now records this correction in full; the
eval fails on purpose as its regression test. A smaller, already-fixed finding from the same run: the
model didn't reliably format `requiredBy` as ISO-8601 (`"5th"` on one run, valid on another) — fixed
via a lenient converter plus a tighter prompt instruction, both in this commit.

**Blocked on Harsh:** The `thought_signature` finding above needs a direction decision, not a guess —
options include checking whether a newer `Microsoft.Agents.AI`/`Microsoft.Extensions.AI.OpenAI`
release has addressed it, switching the default profile to `github` (SPEC.md §4: real OpenAI models,
correct tool-calling, but ~50 req/day and an ~8K input cap — cannot carry the demo), or something else
entirely. This blocks the Resolve stage working end to end against the pinned model, which blocks a
real browser demo — everything else in tasks 06–07 is proven correct against a stub and does not
depend on this being resolved.

**Next:** Resolve the `thought_signature` non-streaming finding above before task 08, or explicitly
decide to proceed with task 08 (React screens) anyway, since the UI can be built and demoed against
stub-driven or replayed runs regardless. `src/QuoteDesk.Web/src/api/agentEvents.ts` is ready for
`useAgentStream` to consume either way.

## 2026-08-30 — Gemini `thought_signature` fix: adopted Google.GenAI

**Done:** The `thought_signature` blocker above is resolved. Harsh asked whether OpenRouter or
Google's native SDK would fix it; researched both — OpenRouter confirmed no fix (same OpenAI-compat
shim, same error, per multiple independent GitHub issue reports), Google's official `Google.GenAI`
.NET SDK confirmed a real fix (`api-researcher`: read the SDK's own source and decompiled this
project's exact installed `FunctionInvokingChatClient` — the adapter round-trips the signature through
a standard `TextReasoningContent.ProtectedData` field, which the loop never strips). Spiked first
(isolated `resolve_customer` call, no pipeline involved) — passed. Adopted for real:
`ChatClientFactory.Create` now branches on a new `LlmOptions.Provider` (`"gemini"` → `Google.GenAI`,
`"github"` → unchanged `OpenAIClient`). Confirmed live through the actual `EnquiryPipeline`: Extract
succeeded, `resolve_customer` **and** `get_customer_history` both completed multi-turn with real tool
results — the exact call that failed before now works — before a genuine free-tier daily quota (20
requests/day for `gemini-3.6-flash` on this key) cut the run short. 157/157 non-eval tests still pass.

**Files that matter:** `src/QuoteDesk.Agents/Llm/ChatClientFactory.cs` (the branch + full reasoning),
`src/QuoteDesk.Agents/Llm/LlmOptions.cs` (`Provider`), `docs/SPEC.md` §4 (full resolution write-up).

**Decisions made:** Spike-then-adopt, not straight to production code — Harsh's call, to avoid
touching `ChatClientFactory` before knowing it would work. Trade-off accepted knowingly: `Google.GenAI`
takes an API key, not a base URL, so the `gemini` profile lost the "any OpenAI-compatible endpoint"
swappability; `github` is unaffected. Found and fixed in the same pass: the two profiles now throw
different exception types for a rate limit (`ClientResultException` vs `Google.GenAI.ClientError`) —
the live quota hit proved this was a real gap, not hypothetical, so `EnquiryPipeline.ToErrorEvent` now
matches both, with a new stub-based regression test.

**Known gaps:** No single completely clean live run (Extract → Resolve → Price → `ApprovalRequiredEvent`)
has completed yet — the free-tier quota (20 req/day for this model) was exhausted mid-verification.
The multi-turn tool-calling mechanism itself is confirmed fixed (two real tool calls completed that
previously failed on the very first one); what's unconfirmed is only the *rest* of the pipeline
(Price's narration call, the full worked example's ambiguity handling) under the new client, which
should behave identically since nothing else changed, but hasn't been watched happen end to end yet.

**Blocked on Harsh:** Nothing required, but worth knowing: 20 requests/day is tight for a live public
demo one Google account away from being useless — worth checking Google AI Studio for a way to raise
this free-tier quota, or confirming this is expected for `gemini-3.6-flash` specifically, before task 09.

**Next:** Once the daily quota resets, run `dotnet test tests/QuoteDesk.Evals --filter GeminiWorkedExampleEval`
once more to confirm a fully clean pass, then proceed to task 08 (React screens) — nothing about the
UI depends on this being re-verified first.

## 2026-08-30 — Phase 0 result: gemini-3.1-flash-lite tested, rejected for quality

**Done:** Ran the real worked example against `gemini-3.1-flash-lite` (a separate free-tier quota
bucket from `gemini-3.6-flash`'s exhausted 20/day). Quota was not the limiting factor this time — the
run consumed far more than 20 requests with no quota error at all, confirming per-model buckets are
real and separate. But it failed on quality: `search_catalog("6203 bearing")` came back with **112
weakly-scored candidates** (all confidence 0.2, matched only on the token "RING") instead of narrowing
to the actual bearing, and the model then tried to disambiguate by calling `get_customer_history`
one SKU at a time across many candidates — a brute-force exploration that burned 153,724 tokens against
a 20,000 budget before `EnquiryPipeline`'s safety cap correctly stopped the run with `budget_exceeded`.
Nothing wrong reached anywhere (the safety net worked exactly as designed), but the judgment quality
was measurably worse than `gemini-3.6-flash`'s clean run on the identical enquiry.

**Decision: stay on `gemini-3.6-flash`.** Per the plan agreed before this test: a demo that reasons
this poorly is worse than one that occasionally runs out of quota. `tests/QuoteDesk.Evals/GeminiFlashLiteWorkedExampleEval.cs`
kept in the repo as a real, dated record of this — not deleted — so a future session doesn't re-attempt
the same switch without re-testing (and re-test is worth doing again later: this was one run, on a
"Lite" model that may simply need a different, less open-ended prompt to search well, not necessarily
a permanent verdict on the model itself).

**Next:** Proceed to batching `search_catalog` (already approved, model-independent) — see the current
plan for the concrete change.

## 2026-08-30 — Batched search_catalog

**Done:** `search_catalog` now resolves every line item in one call instead of one call per line —
`(CatalogSearchQuery[] queries) -> CatalogSearchResult[]`, one result per query in the same order.
For docs/DOMAIN.md's worked example this cuts Resolve from 6 real model calls to 4, and the whole
pipeline from 8 to 6. No change was needed to `ResolveExecutor`, `TracedAIFunction`, or
`ToolCallBudget` — none of them ever assumed one call resolved one line. 158/158 non-eval tests pass
(118 unit + 40 integration — one new unit test added for the batch case), `npm run build` clean.

**Files that matter:** `src/QuoteDesk.Agents/Tools/CatalogTools.cs`,
`src/QuoteDesk.Agents/Tools/Results/CatalogResults.cs` (new `CatalogSearchQuery`, `CatalogSearchResult`
gained a `Query` echo field), `src/QuoteDesk.Agents/Prompts/resolve.md`, docs/SPEC.md §7.

**Decisions made:** Kept the tool name and read/write registry unchanged — only its signature changed,
so `ToolRegistryTests`'s fixed name list needed no update. `CatalogSearchResult` gained a `Query` field
(echoing which input it answers) so the model — and a human reading the trace panel — can map a
batched call's results back to specific line items without relying on array position alone.

**Known gaps:** None new. This closes out the batching work from the earlier assessment; the narration
LLM-call-removal option from that same assessment was not picked and remains undone, on purpose.

**Next:** Task 08 — React screens. Nothing about this session's work changes that scope.

## 2026-08-30 — gemini-3.5-flash-lite tested, also rejected for quality

**Done:** Harsh checked Google AI Studio's own rate-limit dashboard directly — real numbers, not blog
guesses: `gemini-3.6-flash`/`gemini-3.7-flash`/`gemini-3 Flash` all cap at 20/day (matches what we
already measured), but `gemini-3.5-flash-lite` shows 500/day. Worth testing on quota grounds alone.
Ran the same rigorous worked-example eval used for `gemini-3.1-flash-lite`. Result: same class of
failure, different mechanism — the batched `search_catalog` call itself (confirms batching works
correctly against a real model) came back with a wildly over-broad candidate list (342 SKU mentions
across the three line items, in a ~300-item catalogue), and the model burned 56,463 tokens against the
20,000 budget trying to work through it before the safety cap stopped the run.

**Now two different "Lite" models have failed this exact bar, for related but distinct reasons** —
`3.1-flash-lite` over-explored via many small `get_customer_history` calls, `3.5-flash-lite` got a
bloated result from one `search_catalog` call. Both point at the same underlying weakness: Lite-tier
models construct less specific/tighter search queries than `gemini-3.6-flash` does, which this
project's `CatalogTools` scoring is sensitive to. This is real, repeated signal, not a fluke — staying
on `gemini-3.6-flash` as primary is the right call until/unless a Lite model gets a properly tuned
prompt for this specific task (not attempted; out of scope right now).

**Files that matter:** `tests/QuoteDesk.Evals/Gemini35FlashLiteWorkedExampleEval.cs` (new, kept as a
dated record, same reasoning as the 3.1 variant).

**Next:** Harsh asked about a fallback chain (gemini-3.6-flash primary, drop to a Lite model only once
quota is truly exhausted) rather than switching the primary outright. Given both Lite candidates now
have confirmed quality problems, this reframes as "worse but available beats nothing," not "just as
good and free" — a real design worth doing carefully, not a quick win. Not started; needs its own scoping
(mid-conversation model switching risk, whether to fall back per-run vs mid-run, how the trace panel
shows which model actually answered).

## 2026-08-30 — Session close: root cause refined, task 08 is next

**Done:** Traced the `3.5-flash-lite` failure down to real data rather than guessing: the model's
`search_catalog` query was reasonable ("PU timing belt", hint "25mm"); the 150-candidate flood was
`CatalogTools.Score` matching every "Rubber Timing Belt" too, because it counts overlapping words
without weighting the one that actually distinguishes the item (PU vs Rubber). **Corrects the earlier
entry's framing** ("Lite models construct less specific queries") — the confirmed root cause is our
own scoring code being too permissive, not the model's query construction. Separately, real: once
handed a big/ambiguous list, `3.1-flash-lite` chose to brute-force it (many individual
`get_customer_history` calls) instead of accepting ambiguity like the prompt instructs — that part is
a genuine model-quality difference, not a code bug.

**Decision:** Task 08 (React screens) starts next session, before any of today's follow-ups
(`CatalogTools.Score` reweighting, a model-fallback chain). Both are real and worth doing, but
deliberately parked — UI comes first.

**Known gaps / parked for later:** `CatalogTools.Score` doesn't weight distinguishing words — worth
fixing regardless of which model is used, and might be enough on its own to make a Lite model viable.
A model-fallback chain (gemini-3.6-flash primary, Lite as last resort once quota's gone) discussed but
not designed — needs its own scoping session.

**Everything from today (task 07, the Google.GenAI/thought_signature fix, batched `search_catalog`,
three eval files) is staged but not committed** — nothing lost, ready whenever Harsh wants the commit
message.

**Next:** Task 08 — React screens (Desk, Approvals, Quotes), starting fresh next session.
