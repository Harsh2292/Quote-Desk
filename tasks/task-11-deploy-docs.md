# Task 11 — Deploy, README, demo

**Session 3 · depends on: 10**

## Goal

A live URL, and the documentation that turns this repo into interview material. **Half the value of
the whole project lives in this task.** It is the least enjoyable one, which is exactly why almost
nobody does it and why doing it separates you.

## Stack for this task

Docker multi-stage · Azure Container Apps · Azure SQL free offer · Azure Static Web Apps

## What to build

**Deploy**

- Multi-stage Dockerfile, non-root user, trimmed
- Container Apps at **min replicas 0** — free grant covers 180k vCPU-seconds a month and nothing is
  charged while scaled to zero
- Azure SQL **free offer**: 100k vCore-seconds and 32GB, permanent. Choose the **auto-pause** option,
  not pay-overage — once selected, pay-overage cannot be reversed.
- Static Web Apps free tier for the frontend
- A ₹0 budget alert on the subscription. Free tiers change.
- Measure the cold start and write the number down

**README** — the part that gets read:

- what it is, in three sentences
- an architecture diagram showing the fixed pipeline and where the one autonomous stage sits
- a 30-second demo GIF covering the worked example: paste → trace streaming → ambiguity flagged →
  approval → quote
- **"Why I built it this way"** — deterministic pricing, the approval gate, no vector database,
  typed tools instead of text-to-SQL. This section is what an interviewer quotes back at you.
- how to run it locally
- the cold-start note, honestly explained
- future work: voice note transcription, Meta Cloud API, multi-tenant

**Close out**

- ADR-0002 on choosing no vector database (ADR-0001 already exists)
- `spec-auditor` run once, clean — no drift and no checkbox ticked that is not true
- `dotnet-reviewer` over the full history, findings fixed or consciously accepted

## Acceptance criteria

- [ ] Live URL opens in a browser that has never seen it, cold
- [ ] The worked example can be run end to end on the live site
- [ ] Budget alert configured
- [ ] README complete, including the diagram, the GIF and the "why" section
- [ ] Both ADRs written
- [ ] `spec-auditor` reports no drift and no false ticks
- [ ] Cold start measured and documented

## Out of scope

Custom domain, CDN tuning, autoscaling rules, load testing.

## Notes on completion
