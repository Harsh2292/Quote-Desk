---
paths:
  - "tests/**"
---

# Testing

xUnit and FluentAssertions. Nothing else.

**Where unit tests are required** — written down so "where needed" cannot quietly become "nowhere":

- `QuoteDesk.Domain` — exhaustively, every rule and every boundary
- every tool's validation and miss paths
- every intake adapter's parsing, including malformed and empty payloads

Everything else is covered at integration level.

- **Integration tests use a stubbed `IChatClient`.** CI must pass with no network and no API key. A
  test that calls a real model is an eval, not an integration test.
- **Evals live in `tests/QuoteDesk.Evals`**, a separate project excluded from the default run.
- **Test behaviour, not implementation.** Asserting a private method was called is not a test.
- **Cover the boundaries**: exactly on a slab edge, exactly at the margin floor, zero, empty, unknown.
  The happy path rarely breaks.
- **A bug fix starts with a failing test.**
- **Never weaken an assertion to make a test pass.** If a test looks wrong, say so and explain why.
  Quietly adjusting it destroys the only signal that anything works.
- **Tests are deterministic.** No real clock, no unseeded random, no ordering dependence.
- Name tests `MethodName_Scenario_ExpectedOutcome`.
