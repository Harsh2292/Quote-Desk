# QuoteDesk

> An agentic RFQ-to-quotation service. .NET 10, Microsoft Agent Framework, EF Core, React — built on
> the rule that the language model never decides a number.

**Status: not built yet.** This README is written properly in task 11. Until then it stays honest
about that.

## What it will do

A customer sends a messy enquiry by paste, email or WhatsApp — nicknames instead of part numbers, a
delivery date, a remembered discount. An agent layer extracts the lines, resolves them against the
catalogue and the customer's own purchase history, checks stock and lead times, prices everything
with deterministic C#, and presents a draft quotation to a salesperson. Nothing is created or sent
until a human approves it.

## Architecture in one line

A fixed pipeline with one autonomous stage:

```
Extract → Resolve → Price → Approve → Create → Send
```

The sequence never reorders or skips. Inside `Resolve` the agent chooses its own tool calls. `Price`
is plain C#. `Approve` is a human. The model is used only where the input is genuinely ambiguous;
everything with consequences is code.

## The four rules

1. The model never decides money — pricing lives in a dependency-free domain project.
2. No raw SQL from the model. It calls typed tools; data access is EF Core with LINQ.
3. Nothing leaves without a human. Write tools are unreachable from the resolving agent.
4. Every stage and tool call is traced and streamed to the UI.

## To be filled in at task 11

- [ ] Architecture diagram
- [ ] 30-second demo GIF
- [ ] Live URL and the cold-start note
- [ ] How to run it locally
- [ ] Why it is built this way — the section that actually matters
- [ ] Future work: voice note transcription, Meta WhatsApp Cloud API

## Development

`CLAUDE.md` holds the rules this repo is built under. `docs/SPEC.md` is the contract.
`tasks/` is the work queue — open the folder in VS Code, run `claude`, and type `/task 00`.
