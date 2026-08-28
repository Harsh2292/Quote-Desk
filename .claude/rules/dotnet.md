---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# C# rules

Each of these prevents a specific failure. Anything that is only my taste is not in this file — for
style, follow the surrounding code and `.editorconfig`.

- **Money is `decimal`.** Never `double` or `float`. Round only through `Money` in `QuoteDesk.Domain`;
  two code paths rounding differently is a bug even when both tests pass.
- **Dates are `DateTimeOffset` in UTC at rest.** Convert to IST only for display. `QuoteDesk.Domain`
  never reads the clock — time is always a parameter.
- **`CancellationToken` on every public async method**, and passed on. No `.Result`, no `.Wait()`,
  no `async void`.
- **No swallowed exceptions.** Handle it meaningfully or let the global handler shape it. `catch { }`
  does not appear in this repo.
- **Dependency direction is one way**: `Api → Agents → Data → Domain`, `Intake → Api`. `Domain`
  references nothing. A reference pointing back up means the logic is in the wrong project.
- **Nullable is enabled and warnings are errors.** Do not suppress a warning to move on; fix it, or
  tell me why suppressing is correct.

If you think one of these is wrong for the case in front of you, say so before following it.
