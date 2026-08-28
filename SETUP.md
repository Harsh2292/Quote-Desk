# How this repository is set up for Claude Code

The configuration is checked in deliberately. If you are reading this as a reviewer: this is how the
work was directed, and these constraints were chosen before any code was written.

## Five layers, separated by when they load

`CLAUDE.md` loads into every session, so it stays under 200 lines and holds only what must always be
true. Everything else loads conditionally.

| Location | Loads | Contains |
|---|---|---|
| `CLAUDE.md` | every session | the four project rules, layout, commands, `@imports` |
| `.claude/rules/*.md` | when a matching file is touched | per-area rules, scoped with `paths:` |
| `.claude/skills/*/SKILL.md` | when invoked as `/name` | repeatable procedures |
| `.claude/agents/*.md` | when delegated to | specialists with isolated context |
| `.claude/hooks/*.sh` | at lifecycle events | mechanical enforcement |

**CLAUDE.md is advice; hooks are law.** Markdown shapes behaviour but guarantees nothing. A
`PreToolUse` hook runs regardless of what the model decides. So anything genuinely intolerable — a
secret written to disk, a force push, a recursive delete — is a hook, not a paragraph.

## A rule earns its place by naming the failure it prevents

Every rule in `.claude/rules/` maps to a specific way this project could go wrong: money in a
`double`, a read query tracking entities it will never modify, a test quietly weakened to go green,
an invented Agent Framework API. Preferences that prevent nothing were deliberately left out —
`.editorconfig` handles style, and over-constraining a model makes it optimise for compliance instead
of for the problem.

For the same reason `CLAUDE.md` carries an explicit instruction to **disagree** and say why before
following a rule that is wrong for the case at hand.

## What each piece does

**Rules** are path-scoped, so they cost nothing until relevant. Editing a `.cs` file loads the C#
rules; editing under `src/QuoteDesk.Domain/` loads the pricing rules too, which are stricter than the
rest of the codebase. `security.md` has no `paths` and is always on.

**Skills.** `/task NN` implements exactly one item from `tasks/` and stops. `/verify-all` is the gate:
build, tests, TypeScript, lint, frontend build, then eight checks done by reading. `/session-log`
writes the handover note. `/adr` records a decision.

Only `/task` and `/adr` are locked to manual invocation — starting work and declaring a decision are
human calls. `/verify-all` and `/session-log` are deliberately left model-invocable so Claude runs
them itself at the end of every task, without being asked. The developer types `/task NN` and
`/clear`, and toggles plan mode. Nothing else.

**Subagents** work in their own context and return a summary. `api-researcher` is the important one:
Microsoft Agent Framework reached 1.0 in April 2026, recalled signatures are unreliable, and
confidently-wrong agent code is the fastest way to lose a day. It has no write tools and must cite a
source and a confidence level for every claim. `dotnet-reviewer` reviews a finished task.
`spec-auditor` hunts drift — including checkboxes ticked without being true. Run that one **once**, at
the end of the final session; on a solo three-session build, running it every session is noise.

**Hooks.** `session-start.sh` reinjects branch, commits, the in-progress and next task, and the last
handover note, because context does not survive between sessions. `guard-paths.sh` blocks writes to
secrets. `guard-bash.sh` blocks the short list of commands that are never right here. `format.sh`
formats frontend files asynchronously. There is deliberately **no `Stop` hook running the build** — it
would fire every turn, take fifteen seconds, and train you to ignore it. Verification is invoked, not
nagged.

**`.mcp.json`** wires in the Microsoft Learn MCP server so documentation is looked up, not recalled.

## Work queue

`tasks/README.md` is the only place status lives. Each of the eleven task files carries its own stack,
scope, acceptance criteria, out-of-scope list, and a "Notes on completion" section written when it
closes — so a finished task file is a record, not just an instruction.

| Session | Tasks | Outcome |
|---|---|---|
| 1 | 00–03 | Environment, repo, provider spike, then schema, migrations, deterministic seed and the whole pricing engine under test. Apart from the throwaway spike, **no LLM is called at all.** |
| 2 | 04–08 | Intake, tools, agents, workflow with approval gate, API with SSE, three React screens. |
| 3 | 09–11 | Email and WhatsApp channels, telemetry, evals, CI, Azure deploy, README and demo. |

Session 1 comes first on purpose: by the time an agent is involved, every number it will ever quote
is already proven correct in isolation.

## Getting started

Open this folder in VS Code, move the terminal panel to the right, then:

```bash
claude
```

Trust the folder, approve the Microsoft Learn MCP server, and type:

```
/task 00
```

Task 00 does the rest — it reads the plan and tells you what it thinks is wrong, checks the machine,
makes the hooks executable, installs `dotnet-ef`, initialises the repo, and proves tool calling works
on the Gemini key. Restart Claude Code afterwards so the hooks become active.
