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
criterion that cannot be met as written, or scope the demo does not need — say so now, before writing
code. The task files are a best guess, not gospel.

## 2. Say what you are about to do, briefly

Which files you will create or change, which types you will add, which tests will prove it works.
A short paragraph, not a document. If this touches `QuoteDesk.Domain` or `QuoteDesk.Agents`, wait for
a go-ahead. If it uses a Microsoft Agent Framework API, confirm the signature against the
installed package's XML docs first; the `api-researcher` subagent is a last resort.

## 3. Build in dependency order

Domain → Data → Agents → Intake → Api → Web. Each layer compiles before the next. Tests for a layer
are written as you finish it, not saved up for the end.

**You do the building.** Harsh is learning this stack by reading the result, so implement it rather
than handing back instructions. Ask him to run something only when it genuinely needs him — an
interactive login, a portal click, a credential, a payment.

## 4. Prove it — you run this, not Harsh

```bash
dotnet build QuoteDesk.sln -warnaserror
dotnet test --filter "FullyQualifiedName!~Evals"
cd src/QuoteDesk.Web && npm run build
```

All green, every time, before reporting anything done. Never weaken a test to get past a failure.
If something fails, say what failed before you start fixing it.

## 5. Close the loop — also yours, automatically

- Tick only the acceptance criteria that are genuinely true
- Write the **Notes on completion** section at the bottom of the task file: what was built, what you
  left out, what surprised you, what the next task should know
- Update the status row in `tasks/README.md`
- **Invoke `/session-log` yourself.** Every task, every session end.
- Stage the change with `git add`, confirm nothing secret is staged, and **write out the
  conventional-commit message for Harsh to run.** Never run `git commit` or `git push` yourself.

## 6. Explain what you built

A short walkthrough of the interesting parts — especially anything touching Microsoft Agent
Framework, React, Docker, CI/CD or hosting. This is the point of the project as much as the code is.

## Then stop

Report what you built, what you deliberately left out, and what the next task should be. Do not
begin it.
