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
