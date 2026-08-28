# Tasks

The work queue and **the only place status lives**. Each task is finishable in one sitting and
delivers behaviour through every layer it touches.

**Start here: `/task 00`.** It sets up the machine, the repo, and proves the Gemini key works.

You only ever type `/task NN` and `/clear`, plus plan mode with `Shift + Tab`. Verification, the
handover note, the status update and the code review all happen automatically as part of finishing a
task — you should never have to ask for them.

| # | Task | Session | Status |
|---|---|---|---|
| 00 | [Environment, repo, provider spike](task-00-environment.md) | 1 | todo |
| 01 | [Setup and skeleton](task-01-setup.md) | 1 | todo |
| 02 | [Data, EF Core, migrations, seed](task-02-data-efcore.md) | 1 | todo |
| 03 | [Pricing domain](task-03-pricing-domain.md) | 1 | todo |
| 04 | [Intake abstraction and paste adapter](task-04-intake.md) | 2 | todo |
| 05 | [Typed tools](task-05-tools.md) | 2 | todo |
| 06 | [Agents and workflow](task-06-agents-workflow.md) | 2 | todo |
| 07 | [API, streaming, auth, logging](task-07-api.md) | 2 | todo |
| 08 | [React screens](task-08-web.md) | 2 | todo |
| 09 | [Email and WhatsApp channels](task-09-channels.md) | 3 | todo |
| 10 | [Observability, evals, CI](task-10-observability-ci.md) | 3 | todo |
| 11 | [Deploy, README, demo](task-11-deploy-docs.md) | 3 | todo |

Status values: `todo` · `in progress` · `done` · `blocked`

## Sessions

**Session 1 (tasks 00–03)** — environment, repo, and a throwaway spike proving tool calling works on
the Gemini key, then schema and the entire pricing engine under test. Apart from the throwaway spike, **no LLM is
called at all**. By the end, every rupee QuoteDesk will ever quote is already provably correct.

**Session 2 (tasks 04–08)** — the agent layer and the product. The big one.

**Session 3 (tasks 09–11)** — extra channels, telemetry, evals, CI, deploy, and the README.

## Only after task 11

- [ ] Resume updated, the old unfinished project removed
- [ ] A short write-up posted publicly, linking the repo and the live demo
