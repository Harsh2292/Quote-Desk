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
a plain-language label for the step (never the raw tool name — Harsh's call during the design
review, tool names are internal), what it looked at and returned, duration, ok/fail, collapsible.

**Approvals** — cards for pending actions. The ambiguous line shown in red with the agent's reason,
the date conflict in amber, the within-policy discount as a note. Approve / reject only — the Api
rejects `edit` 400 until a later task defines the payload, so the UI has no edit control and no
line-resolution dropdown (that needs `UnresolvedLine.Candidates[]` server-side first).

**Quotes** — list and detail, each linked to the trace that produced it.

Also:

- `useAgentStream` — one typed hook built on `fetch` + `ReadableStream` (NOT `EventSource`: both
  streaming endpoints are POST and need a bearer `Authorization` header, per task 04a and CLAUDE.md).
  It parses each SSE frame into the `AgentEvent` union. The server has no resume, so recovery is a
  fresh `GET /api/enquiries/{id}` for its stored trace, exposed as a `recover()` action. Do not read
  streams anywhere else.
- Loading, empty and error states on **every** async surface
- The `provider_rate_limited` state offers "replay a saved run", backed by three recorded runs stored
  as JSON. A recruiter must never see a blank error page.

## Acceptance criteria

- [x] All three screens work against the real API
- [x] The trace panel renders every `AgentEvent` variant correctly
- [x] The worked example is demonstrable start to finish in the browser
- [x] Every async surface has all three states (`useAsync` + `AsyncBoundary` make it structural)
- [x] Rate-limited replay works with the API stopped (three runs recorded as `AgentEvent[]`)
- [x] `tsc -b`, `npm run lint` (oxlint — the project has no eslint) and `npm run build` all clean,
      bar one pre-existing `only-export-components` warning on `AuthContext.tsx` from task 04a
- [x] No `any` in the codebase

## Out of scope

Mobile layouts beyond "does not break". No landing page, no settings, no theme toggle.

## Notes on completion

**Designed first in Claude Design.** Harsh wanted the three screens plus the states that carry the
demo (empty Desk, streaming Desk, approval reached, Approvals, Quotes list, Quote detail, the
`provider_rate_limited` replay picker) settled visually before any React was written — seven
artboards on one canvas, refined by hand, then transcribed. The dense operator-tool direction and
the slate/system-font/`tabular-nums` vocabulary come from that canvas; it extends the default
Tailwind palette the sign-in screen already used rather than adding tokens.

**No component library.** ~6 hand-rolled primitives in `src/components/ui.tsx` (`Button`, `Badge`,
`Card`, `Mono`, `Eyebrow`, `Field`, `StatusDot`, `Spinner`, `AsyncBoundary`). shadcn was considered
and declined — the two parts that matter (the trace panel, the approval card) are bespoke either
way, and five endpoints do not justify Radix + CVA + a setup step.

**Trace panel shows plain-language labels, never raw tool names.** `src/api/traceLabels.ts` maps
`resolve_customer` → "Matched customer" etc.; an unmapped name degrades to a de-underscored,
title-cased form. This overrides the "tool name" wording in docs/SPEC.md §8 and CLAUDE.md — recorded
there too. The argument payload and result of each step are still shown on expand.

**Approve / reject only.** `POST /api/approvals/{id}` rejects `edit` with a 400 and `UnresolvedLine`
carries no SKU candidates, so there is no edit control and no line-resolution dropdown. Unresolved
lines render in red with the agent's reason and the quote cannot be sent until a human deals with
them — the "agent refuses to guess" half of docs/DOMAIN.md's worked example is demonstrable; the
"Mehul picks 8mm" half is deferred with a named shape (`UnresolvedLine.Candidates[]` +
`ApprovalDecisionRequest.LineSelections[]`), noted in docs/SPEC.md §8.

**SSE hook.** `useAgentStream` is `fetch` + `ReadableStream` (POST + bearer header rule out
`EventSource`). It checks `content-type` before parsing, because `/api/approvals/{id}` answers a bad
decision with JSON ProblemDetails, not a stream. No reconnect — the server writes no `id:` lines;
`recover(enquiryId)` re-fetches the persisted trace instead.

**Routing** is a ~55-line hash router (`src/routing/useHashRoute.ts`) over `useSyncExternalStore` —
`#/desk`, `#/desk/:enquiryId`, `#/approvals`, `#/quotes`, `#/quotes/:id`. Deep links survive a
refresh; no router dependency.

**Rate-limited replay** uses three runs recorded by hand as typed `AgentEvent[]`
(`src/fixtures/*.ts`) rather than captured from a live model — deterministic, and it does not spend
the free-tier daily quota. Replayed approval cards have Approve/Reject disabled (no real `AgentRun`
to POST to).

**Not done here:** `provider_rate_limited` is detected from a `429` on the stream fetch; a live run
that never 429s but simply exhausts the token budget surfaces as `budget_exceeded` and renders as a
plain error with a retry, not the replay picker. Matching the spec's intent (replay offered on any
provider failure) is a small follow-up. Desk's post-refresh approval flow resolves the `AgentRun.Id`
by scanning `GET /api/approvals`; if that list is large this is wasteful, but it is bounded by the
number of pending approvals.

---

**Amended 2026-08-31 — agent-layer rework + audit.**

**Added:** the Desk keeps its state. A `DeskSessionProvider` mounted above the router holds the
enquiry text, the live `useAgentStream` instance, and the decision state, so navigating to Approvals
and back no longer destroys the run; it survives a browser refresh via `sessionStorage` (400 KB cap,
drops the trace before overflowing). New controls: **New enquiry**, **Retry** (re-process the same
enquiry), **Edit & re-run** (change the text, run it as a new enquiry). Nothing clears except New
enquiry or a completed approve. The Desk tab links back to the run in progress.

**Gaps found by the 2026-08-31 audit, recorded not fixed — work for the post-09 review pass:**

- The sign-in screen (`src/QuoteDesk.Web/src/auth/SignInScreen.tsx`) is one sentence, predates the
  design system, swallows the Google widget's own failures (`onError={() => undefined}`), and shows
  no in-flight state during the `POST /api/auth/google` call. Polish folded into task 09 (it is the
  first thing a recruiter sees on the live URL).
- `budget_exceeded` and `internal` errors render as a plain trace-panel error — only a transport
  `429` gets the recorded-run replay picker, which was built for exactly this case.
- A `401` on a **streaming** call (`process` / `decide`) does not clear the token or flip the app to
  signed-out the way a `401` on a normal `apiFetch` call does — it surfaces as `internal` and leaves
  a dead screen while the user is nominally still signed in. `openAgentStream` needs the same
  `setToken(null)` + `onUnauthorized()` path `apiFetch` has.
- A deep-linked enquiry that `404`s shows "Loading enquiry…" forever — the `AsyncBoundary` in
  `DeskScreen` only wraps the pending-approval block, not the enquiry pane.
- The final pipeline stage (`price`) never shows a duration: the browser derives stage duration from
  the next stage's start time and there is no stage after `price`. A real fix is per-stage timing
  emitted server-side — that is task 11's per-stage-token/duration work.
