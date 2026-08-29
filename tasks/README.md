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
| 04 | [Intake abstraction and paste adapter](task-04-intake.md) | 2 | todo |
| 05 | [Typed tools](task-05-tools.md) | 2 | todo |
| 06 | [Agents and workflow](task-06-agents-workflow.md) | 2 | todo |
| 07 | [API, streaming, auth, logging](task-07-api.md) | 2 | todo |
| 08 | [React screens](task-08-web.md) | 2 | todo |
| 09 | [**Deploy — Docker, CI, live URL**](task-09-deploy.md) | 2 | todo |
| 10 | [Email and WhatsApp channels](task-10-channels.md) | 3 | todo |
| 11 | [Observability, evals, README, demo](task-11-observability-docs.md) | 3 | todo |

Status values: `todo` · `in progress` · `done` · `blocked`

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

**Session 2 (tasks 04–09)** — the agent layer, the product, and the deploy. The big one.

**Session 3 (tasks 10–11)** — extra channels, telemetry, evals, and the README.

## Only after task 11

- [ ] Resume updated, the old unfinished project removed
- [ ] A short write-up posted publicly, linking the repo and the live demo
