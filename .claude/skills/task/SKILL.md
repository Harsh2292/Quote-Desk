---
name: task
description: Implement one task from tasks/ end to end, with tests, then stop. Use to start any piece of work on QuoteDesk.
argument-hint: [task number, e.g. 03]
disable-model-invocation: true
---

# Implement one task

Target: task **$ARGUMENTS** (if empty, take the first `todo` row in `tasks/README.md`).

One task. Not two.

## 1. Read before writing

Read the task file in full, plus the parts of `docs/SPEC.md` and `docs/DOMAIN.md` it points at, plus
the existing code you are about to extend. Do not assume the code matches the spec — check.

**If anything in the task looks wrong** — a bad approach, a missing prerequisite, an acceptance
criterion that cannot be met as written — say so now, before writing code. The task files are my best
guess, not gospel.

## 2. Plan, and show me

State which files you will create or change, which types you will add, and which tests will prove it
works. If this touches `QuoteDesk.Domain` or `QuoteDesk.Agents`, wait for my go-ahead. If it uses a
Microsoft Agent Framework API, confirm it with `api-researcher` first and show me the source.

## 3. Build in dependency order

Domain → Data → Agents → Intake → Api → Web. Each layer compiles before the next. Tests for a layer
are written as you finish it, not saved up for the end.

## 4. Prove it — you run this, not me

**Invoke `/verify-all` yourself.** Do not ask me to run it and do not report a task done without it.
Everything must be green. Never weaken a test to get past a failure.

If it comes back red, tell me what failed before you start fixing.

## 5. Close the loop — also you, automatically

- Tick only the acceptance criteria that are genuinely true
- Write the **Notes on completion** section at the bottom of the task file: what was built, what you
  left out, what surprised you, what the next task should know
- Update the status row in `tasks/README.md`
- **Invoke `/session-log` yourself.** Every task, every session end. I should never have to ask.
- Hand the diff to `dotnet-reviewer` and summarise what it says
- Stage the change and propose a conventional-commit message. Do not commit until I say so.

The only things I type are `/task NN` and `/clear`, plus plan mode. Everything else in this procedure
is yours to run.

## Then stop

Report what you built, what you deliberately left out, and what the next task should be. Do not
begin it.
