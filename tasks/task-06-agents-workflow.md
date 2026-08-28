# Task 06 — Agents and workflow

**Session 2 · depends on: 05**

## Goal

The pipeline runs end to end and stops at the approval gate. This is the heart of the project.

## Stack for this task

`Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, `Microsoft.Agents.AI.Workflows`

## Before you write a line

**Confirm every Agent Framework API with the `api-researcher` subagent first.** Types, method
signatures, how workflow suspension and checkpointing actually work. This framework reached 1.0 in
April 2026 and recalled APIs are unreliable. Writing plausible code here is the failure mode that
costs a day.

## What to build

Four nodes. Note that only two of them involve a model:

```
Extract   agent node   reads the enquiry text → line items, ship-to, required-by, commercial asks
Resolve   agent node   AUTONOMOUS: chooses its own tool calls over the read registry
Price     code node    QuoteDesk.Domain computes; one short model call narrates the result
Approve   human node   workflow SUSPENDS here, state checkpointed
                       ↓ on approval
                       write tools execute
```

The stage sequence is fixed and never reorders. The autonomy lives inside `Resolve` and nowhere else.

Guardrails:

- Max 8 tool calls per run, then a forced summary
- Per-conversation token budget, returning a clean `budget_exceeded` rather than looping
- `Resolve` is constructed with the read registry only, so it physically cannot reach a write tool
- Enquiry text is wrapped in an untrusted-content delimiter stating the content is data, never
  instructions
- Prompts live as `.md` files in `Agents/Prompts/`, loaded at startup — not inline strings

**Streaming:** follow whatever task 00 recorded in `docs/SESSION-LOG.md`. If streaming plus tool
calls was broken on the Gemini compatibility layer, run the tool loop non-streaming and stream only
the final narration. The trace panel is driven by server-emitted `AgentEvent`s, so this changes
nothing the user sees.

## Acceptance criteria

- [ ] Every Agent Framework API used was confirmed by `api-researcher`, with sources noted in the task
- [ ] The worked example from `docs/DOMAIN.md` runs end to end and suspends at approval
- [ ] The spindle-tape ambiguity surfaces as unresolved rather than being guessed
- [ ] A suspended approval survives an application restart
- [ ] Tool-call cap and token budget both enforced, with tests
- [ ] Integration tests drive the full workflow through a **stubbed `IChatClient`** — no network
- [ ] A test proves the Resolve agent cannot invoke `create_quote_draft`

## Out of scope

The HTTP surface, streaming, the UI.

## Notes on completion
