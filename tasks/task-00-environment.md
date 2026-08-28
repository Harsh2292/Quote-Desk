# Task 00 — Environment, repo, and provider spike

**Session 1 · depends on: nothing · the only thing Harsh runs to get started**

## Goal

Get from "a folder of files" to "a git repo on a machine that works, with a Gemini key proven to
support tool calling" — and surface any problem with the plan before a single line of real code
exists.

**Harsh runs `/task 00` and nothing else.** Do all five parts yourself, in order, and stop between
them only where it says to stop.

---

## Part 1 — Read the plan and challenge it

Read `CLAUDE.md`, `docs/SPEC.md`, `docs/DOMAIN.md`, `tasks/README.md`, and skim every other file in
`tasks/`. Then tell Harsh:

1. Anything ambiguous, contradictory, or that you would have to guess at.
2. **Anything you think is wrong.** A rule that will not survive contact with the code, a task whose
   acceptance criteria cannot be met as written, a design choice that will hurt at task 08, a missing
   column we will obviously need. Be direct. The task files are a best guess, not gospel — reworking
   the plan now is far cheaper than reworking the code in a week.
3. Which Microsoft Agent Framework APIs tasks 05 and 06 depend on, and which of them you can actually
   confirm exist. Use the `api-researcher` subagent. If the framework does not work the way the spec
   assumes, that needs to be known today.
4. Your read on the task ordering.

**Stop here and wait for a reply.** Do not continue to Part 2 until Harsh has answered.

---

## Part 2 — Check and fix the machine

Run these and report a pass/fail table:

```bash
dotnet --version     # need 10.x
node --version       # need 20+
docker --version     # and confirm the daemon is actually running
git --version
jq --version
```

Then:

- **`chmod +x .claude/hooks/*.sh`** — do this first, before anything else. The hooks are currently
  inert. They come alive on the *next* start of Claude Code, so mention that to Harsh at the end.
- **`dotnet tool install --global dotnet-ef`** if it is missing — needed at task 02.

**What you can fix yourself:** the chmod, the dotnet-ef tool.

**What only Harsh can do** — list these clearly rather than attempting them, since they need a
GUI or admin rights:

- installing the .NET 10 SDK, Node 20+, or Docker Desktop
- starting Docker Desktop if the daemon is down
- installing `jq` (`winget install jqlang.jq` on Windows, `brew install jq` on macOS,
  `sudo apt install jq` on Linux and WSL)

**`jq` matters more than it looks.** The guard hooks parse JSON with it; without `jq` they exit
silently and the secret protection is gone. If it is missing, say so plainly and do not treat it as
a minor note.

If anything essential is missing, stop and tell Harsh. Do not work around a missing SDK.

---

## Part 3 — Initialise the repository

```bash
git init
git add -A
git commit -m "chore: project setup"
```

Then ask Harsh whether to create the GitHub repo. If yes:

```bash
gh repo create quotedesk --public --source=. --push
```

Public from the first commit — a repo that appears fully formed in one giant commit reads worse than
one that visibly grew, and the commit history is part of what a reviewer looks at.

---

## Part 4 — Provider spike (throwaway)

Answer the one genuine unknown in this project: **does tool calling work on Harsh's Gemini key,
through the .NET OpenAI client, streaming and non-streaming?**

Create a scratch console project **outside the solution folder** that:

1. Builds an `OpenAIClient` pointed at `https://generativelanguage.googleapis.com/v1beta/openai/`
   with the key read from an environment variable or user-secrets — **never hardcoded, never pasted
   into a file.** Ask Harsh to set it; do not ask him to paste the key into the chat.
2. Registers one trivial tool, e.g. `get_stock(string sku) -> int` returning a fixed number
3. Asks *"How many units of BRG-6203-2RS do we have?"* and prints the result
4. Repeats it with **streaming enabled**
5. Reports, for each run: was the tool actually called, with what arguments, and what came back

### If streaming plus tools does not work

This is **not** a blocker. The live trace panel is driven by server-emitted `AgentEvent`s — `stage`,
`tool_start`, `tool_end` — not by model tokens; only the final narration uses `token` events. So the
fallback is: run the tool-calling loop non-streaming and stream only the narration. The UI is
identical. Record the decision and move on.

---

## Part 5 — Record and clean up

- **Delete the spike project.** Only the recorded decision survives.
- Invoke `/session-log` yourself and write down: the environment result, the provider verdict in one
  of these forms — *"streaming + tools works, use it everywhere"* or *"streaming + tools is broken,
  run the tool loop non-streaming"* — and anything from Part 1 that Harsh decided.
- Update the status row for task 00 in `tasks/README.md`.
- Tell Harsh to restart Claude Code so the hooks become active, then propose the commit.

## Acceptance criteria

- [ ] The plan was read and challenged, and Harsh answered
- [ ] Every prerequisite verified; anything missing is clearly Harsh's to install
- [ ] `.claude/hooks/*.sh` are executable
- [ ] `dotnet-ef` installed
- [ ] Git repository initialised with a first commit
- [ ] Tool calling verified on the Gemini key, non-streaming and streaming
- [ ] The streaming decision is recorded in `docs/SESSION-LOG.md`
- [ ] The spike project is deleted and not committed
- [ ] The API key exists only in user-secrets or an environment variable — never in a file

## Out of scope

The solution, any project, any table, any business logic. That is task 01.

## Notes on completion
