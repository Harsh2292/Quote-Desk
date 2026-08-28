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
