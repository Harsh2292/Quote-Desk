# Tasks

The work queue and **the only place status lives**. Each task is finishable in one sitting and
delivers behaviour through every layer it touches.

**Start here: `/task 00`.** It sets up the machine, the repo, and proves the Gemini key works.

You type `/task NN` and `/clear`, plus plan mode with `Shift + Tab`. Verification, the handover note
and the status update happen automatically as part of finishing a task.

| # | Task | Session | Status |
|---|---|---|---|
| 00 | [Environment, repo, provider spike](task-00-environment.md) | 1 | done |
| 01 | [Setup and skeleton](task-01-setup.md) | 1 | done |
| 02 | [Data, EF Core, migrations, seed](task-02-data-efcore.md) | 1 | done |
| 03 | [Pricing domain](task-03-pricing-domain.md) | 1 | done |
| 04a | [Google sign-in and a Users table](task-04a-auth.md) | 2 | done |
| 04 | [Intake abstraction and paste adapter](task-04-intake.md) | 2 | done |
| 05 | [Typed tools](task-05-tools.md) | 2 | done |
| 06 | [Agents and workflow](task-06-agents-workflow.md) | 2 | done |
| 07 | [API, streaming, auth, logging](task-07-api.md) | 2 | done |
| 08 | [React screens](task-08-web.md) | 2 | done |
| — | Agent-layer rework (retrieval, structured output, ceilings) — see docs/SESSION-LOG.md 2026-08-31 | 2 | done |
| 09a | [Deployable — model routing, rate limiting, container, CI](task-09a-deployable.md) | 2 | in progress |
| 09b | [**Deploy to Azure — live URL**](task-09b-azure.md) | 2 | todo |
| — | Code review + security review + codebase walkthrough (Harsh reads the whole deployed system) | 2 | todo |
| 11 | [Observability, evals, README, demo](task-11-observability-docs.md) | 3 | todo |
| 10 | [Email and WhatsApp channels](task-10-channels.md) | 3 | todo |

Status values: `todo` · `in progress` · `done` · `blocked`

**Execution order is 11 before 10, reversing the numbering** (Harsh's call, 2026-09-01): the eval
suite, telemetry and README are the actual differentiators for a portfolio repo and don't depend on
extra channels existing; email/WhatsApp are being deliberately saved for last. Task numbers/file
names are unchanged (10 still means channels, 11 still means observability/evals/README) — only the
row order above, reflecting when each is actually done, changed. Neither task depends on the other
(10 depends on 04, 11 depends on 09), so nothing about swapping them is unsafe.

**Tasks 09–11 were re-scoped on 2026-08-31** to absorb the agent-layer rework and the gaps a full
audit turned up — model routing and the sign-in-screen polish moved into task 09; the eval golden
set, prompt-injection test, per-stage token counts and OpenTelemetry are spelled out in task 11's
"Expanded" section. The small correctness bugs the audit found (a streaming-`401` hole, a
deep-link-`404` infinite load, a swallowed Google `onError`) are recorded in task 08's notes and
belong to the review pass between 09 and 10, not to a numbered task.

**Task 09 was split into 09a/09b on 2026-09-01** — ten distinct pieces of work is not one sitting.
09a is everything verifiable on this machine (the model-routing fix that was the actual blocker, rate
limiting, the container, CI); 09b is Azure and the live URL, needing an account and credentials only
Harsh has.

## Why deploy is task 09 and not last

The previous project was never finished, and an unfinished repo is worth nothing to a recruiter. So
the paste path ships to a public URL the moment it works end to end — before extra channels,
telemetry or evals exist.

From task 09 onward there is always something live to click. Tasks 10 and 11 improve a running
product instead of being prerequisites for one. If the project stops after any of them, what remains
is still a working demo.

## Sessions

**Session 1 (tasks 00–03)** — environment, repo, and a throwaway spike proving tool calling works on
the Gemini key, then schema and the entire pricing engine under test. Apart from the spike, **no LLM
is called at all.** By the end, every rupee QuoteDesk will ever quote is already provably correct.

**Session 2 (tasks 04–09b)** — the agent layer, the product, and the deploy. The big one.

**Session 3 (tasks 11 then 10)** — telemetry, evals, and the README first; extra channels last.

## Only after task 11

- [ ] Resume updated, the old unfinished project removed
- [ ] A short write-up posted publicly, linking the repo and the live demo
