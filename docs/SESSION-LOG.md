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
