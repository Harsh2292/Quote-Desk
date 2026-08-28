# Task 08 — React screens

**Session 2 · depends on: 07**

## Goal

Three screens. The Agent Trace panel is the product — give it real attention, because it is what an
interviewer will actually look at.

## Stack for this task

React 19 · Vite · TypeScript strict · Tailwind · plain `fetch` in small typed hooks

No data-fetching library, no state library, no component library. Six endpoints do not need TanStack
Query, and every dependency here is one you would have to justify.

## What to build

**Desk** — paste an enquiry on the left, live Agent Trace on the right. Each trace row: stage badge,
tool name, arguments, duration, ok/fail, collapsible.

**Approvals** — cards for pending actions. The ambiguous line shown in red with a dropdown, the date
conflict in amber, the within-policy discount as a note. Approve / edit / reject.

**Quotes** — list and detail, each linked to the trace that produced it.

Also:

- `useAgentStream` — one typed hook wrapping `EventSource`, parsing each event into the union and
  handling reconnect. Do not scatter `EventSource` across components.
- Loading, empty and error states on **every** async surface
- The `provider_rate_limited` state offers "replay a saved run", backed by three recorded runs stored
  as JSON. A recruiter must never see a blank error page.

## Acceptance criteria

- [ ] All three screens work against the real API
- [ ] The trace panel renders every `AgentEvent` variant correctly
- [ ] The worked example is demonstrable start to finish in the browser
- [ ] Every async surface has all three states
- [ ] Rate-limited replay works with the API stopped
- [ ] `npx tsc --noEmit`, `npx eslint .` and `npm run build` all clean
- [ ] No `any` in the codebase

## Out of scope

Mobile layouts beyond "does not break". No landing page, no settings, no theme toggle.

## Notes on completion
