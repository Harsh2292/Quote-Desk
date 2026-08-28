---
name: adr
description: Record an architecture decision for QuoteDesk as a numbered ADR in docs/adr/.
argument-hint: [the decision, in a few words]
disable-model-invocation: true
---

# Write an ADR

Decision: **$ARGUMENTS**

Create the next numbered file in `docs/adr/` following `docs/adr/TEMPLATE.md`. Keep it under one
page. An ADR that nobody reads is worse than none.

Write it so a reader who has never seen this codebase understands:

- what forced the decision (the constraint, not the preference)
- what was actually considered — at least two real alternatives with their genuine appeal
- what was chosen and the honest reason
- what this costs us, stated plainly

That last section is the one that matters. An ADR with no downsides listed reads as marketing and an
interviewer will treat it that way. Write the trade-off you actually accepted.

Then add a one-line entry to the index at the top of `docs/adr/` if one exists, and mention the ADR
in `docs/SESSION-LOG.md`.
