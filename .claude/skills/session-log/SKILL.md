---
name: session-log
description: Append a handover entry to docs/SESSION-LOG.md so the next session knows what happened in this one. Run this yourself when a task closes and at the end of every working session. Harsh should never have to ask for it.
allowed-tools: Bash(git log:*) Bash(git status:*) Bash(git diff:*)
---

# Write the handover entry

Context does not survive between sessions. This file is the only thing that does. Write it for a
version of you that knows nothing about today.

Append to `docs/SESSION-LOG.md` — never rewrite what is already there:

```markdown
## <YYYY-MM-DD> — <task number and name>

**Done:** what now works, in terms of behaviour rather than files.

**Files that matter:** the two or three places the next session should look first.

**Decisions made:** anything settled that isn't written in SPEC.md or an ADR. Include the reasoning,
not just the conclusion.

**Known gaps:** what is stubbed, faked, hardcoded, or deliberately skipped. Be specific — "seed data
uses a fixed date of 2026-03-01" is useful, "some things are incomplete" is not.

**Blocked on Harsh:** decisions I could not make alone.

**Next:** the single next task, and why it comes next.
```

Keep it under 25 lines. Then tell me if there is anything in "Blocked on Harsh" I need to answer
before the next session starts.
