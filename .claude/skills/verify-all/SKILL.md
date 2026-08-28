---
name: verify-all
description: Full verification gate for QuoteDesk — build, tests, frontend, lint, plus read-only checks. Run this yourself at the end of every task, before reporting it done. Harsh should never have to ask for it.
allowed-tools: Bash(dotnet build:*) Bash(dotnet test:*) Bash(dotnet format:*) Bash(npm run:*) Bash(npx tsc:*) Bash(npx eslint:*) Bash(git status:*) Bash(git diff:*)
---

# Verify everything

Run every check below. **Run them all before fixing anything** — I want the full picture, not a
running commentary. Report a pass/fail table.

```bash
dotnet build QuoteDesk.sln -warnaserror
dotnet test --filter "FullyQualifiedName!~Evals"
cd src/QuoteDesk.Web && npx tsc --noEmit && npx eslint . && npm run build
```

Then check these by reading, not running:

1. **No secrets staged.** `git diff --cached` and `git status` — nothing resembling a key, token or
   connection string.
2. **No raw SQL.** Grep `src/` for `FromSql`, string concatenation into a query, or any SQL text
   outside a migration.
3. **EF Core hygiene.** Every read-only query uses `AsNoTracking()`. No lazy loading. No entity type
   in a signature outside `QuoteDesk.Data`.
4. **Layer direction intact.** `QuoteDesk.Domain` still references nothing; no reference points back
   up the chain.
5. **The four rules hold.** Walk them from `CLAUDE.md` against this change.
6. **Write tools still gated.** No path from the Resolve agent to `create_quote_draft` or
   `send_quote` that skips the approval suspension.
7. **AgentEvent in sync.** The TypeScript union and the C# type describe the same events.
8. **No weakened tests.** In the diff, look for loosened assertions, skipped tests, or expected values
   changed to match actual output. Flag every one — this is the most damaging thing that can happen
   here.

Green everywhere: say so plainly and stop. Anything red: list failures in severity order, propose
fixes, and wait.

For a large task, hand the diff to `dotnet-reviewer` and summarise what it says.
