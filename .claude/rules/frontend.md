---
paths:
  - "src/QuoteDesk.Web/**"
---

# Frontend

- **The `AgentEvent` union mirrors the C# contract exactly and changes in the same commit.** Drift
  between them is a bug, not a follow-up.
- **One typed hook wraps `EventSource`** (`useAgentStream`), parsing each event into the union and
  handling reconnect. Do not scatter `EventSource` across components.
- **Every async surface renders loading, empty and error.** The `provider_rate_limited` error must
  render as a useful message with the "replay a saved run" action — that is what a recruiter clicking
  the live demo will hit.
- **No `any`.** TypeScript strict stays on.
- **Three screens only**: Desk, Approvals, Quotes. No landing page, no settings, no theme toggle.
  Scope creep in the UI is the cheapest way to run out of time.
- **The Agent Trace panel is the product.** Stage badge, tool name, arguments, duration, ok/fail,
  collapsible. It is what an interviewer will look at — give it real attention.
- Dependencies here need justifying out loud. Six endpoints do not need a data-fetching library.
