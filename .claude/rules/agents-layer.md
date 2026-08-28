---
paths:
  - "src/QuoteDesk.Agents/**"
---

# Agent layer

## Before writing any code here

**Confirm the API exists first**, using the `api-researcher` subagent or the `microsoft-learn` MCP
server. Microsoft Agent Framework reached 1.0 in April 2026 and recalled signatures are unreliable.
"I need to check this" is the correct answer when unsure. Plausible code that does not compile is the
failure mode that costs a day.

## The shape

Four nodes, and only two involve a model:

```
Extract  agent node   enquiry text → line items, ship-to, required-by, commercial asks
Resolve  agent node   AUTONOMOUS — chooses its own tool calls, read registry only
Price    code node    QuoteDesk.Domain computes; one short model call narrates
Approve  human node   workflow suspends, state checkpointed
```

The stage sequence is fixed and never reorders. **All the autonomy lives inside `Resolve`.** Know
that line and defend it.

## Tools

- Record in, record out. Never `string` in, never `object` out.
- A miss returns a typed "not found" or "ambiguous" result rather than throwing — the model must be
  able to reason about it.
- Read and write registries are separate objects. `Resolve` gets the read registry only, so it
  physically cannot call a write tool.
- Write tools suspend the workflow and emit `approval_required`. They never execute inline.
- Tool XML docs are written for the model, in business language. Treat them as prompt engineering.

## Prompt safety

Enquiry text is wrapped in a delimiter stating the content is data and never instructions. There is
an eval case that tries to make the agent obey text inside an enquiry; it must keep failing.

## Budgets

Max 8 tool calls per run, then a forced summary. A per-conversation token budget returning a clean
`budget_exceeded` rather than looping.

Prompts live as `.md` files in `Agents/Prompts/`, loaded at startup — not inline strings.
