# Task 09 — Deploy: Docker, CI, live URL

**Session 2 · depends on: 08**

## Goal

The paste path runs on a public URL that a recruiter can open, cold, on a phone. Everything after
this task improves something already live.

This used to be the last task. It was moved here on purpose: an unfinished repo is worth nothing to
a recruiter, and the previous project died before it ever shipped. Ship first, improve second.

## Stack for this task

Docker multi-stage · GitHub Actions · Azure Container Apps · Azure SQL free offer · Static Web Apps

## What to build

**Container**

- Multi-stage Dockerfile: SDK image builds, runtime image runs. Non-root user.
- `docker compose up -d` still works locally against SQL Server in a container.

**Azure**

- Container Apps for the API at **min replicas 0** — the free grant covers 180k vCPU-seconds a month
  and nothing is charged while scaled to zero.
- Azure SQL **free offer**: 100k vCore-seconds and 32 GB, permanent. Choose **auto-pause**, not
  pay-overage — once pay-overage is selected it cannot be reversed.
- Static Web Apps free tier for the React build.
- Secrets as Container Apps secrets. Never in the repo, never in the image.
- A ₹0 budget alert on the subscription. Free tiers change.

**CI** — GitHub Actions: build → test → container image → deploy. The build and test stages must pass
with **no API key and no network access to any model**; that is what the stubbed `IChatClient` is for.

**Cold start** — measure the first request after idle and write the number down. It goes in the
README as an honest note, and "why is it slow the first time" is a question worth being asked.

## Acceptance criteria

- [ ] `docker build` produces an image that runs the API with a non-root user
- [ ] Live URL opens in a browser that has never seen it, cold
- [ ] The worked example from `docs/DOMAIN.md` runs end to end on the live site: paste → trace →
      ambiguity flagged → approval → quote
- [ ] A push to `main` deploys automatically and the pipeline is green
- [ ] `dotnet test` passes in CI with no API key present
- [ ] Rate limiting and the daily cap are active on the public demo
- [ ] Budget alert configured
- [ ] Cold start measured and recorded in `docs/SESSION-LOG.md`

## Added 2026-08-31 — folded in from the agent-layer rework

**Model routing.** Today one `Llm:Model` serves all three model call types. The pipeline makes three
calls of very different difficulty: Extract (messy text → JSON, no tools), Resolve (autonomous, tool
loop, ambiguity), Narrate (one sentence from computed numbers). Add:

- Per-stage model config — Extract and Narrate on a cheap, high-quota model
  (`gemini-3.5-flash-lite`, ~500/day), Resolve on the capable one (`gemini-3.6-flash`, ~20/day).
  That moves two of three calls per run off the scarce bucket.
- A **per-run provider fallback**: if the first call of a run is rate-limited, run the whole run on
  the fallback model, and put which model answered into the trace so it is visible.
- **No user-facing model selector** — that is a settings control and CLAUDE.md forbids those. The
  routing is deterministic config.

Re-test first: `gemini-3.5-flash-lite` was rejected in an earlier session for "poor judgement", but
it was judged while being handed 342 candidates by the old retrieval code. Run the worked example
against it now that retrieval is fixed before committing to it as the Extract/Narrate model.

**Sign-in screen.** Before the URL is public it needs work — it is one sentence, predates the design
system, and swallows the Google widget's own failures (`onError={() => undefined}`) with no in-flight
state. Give it the dense operator-tool visual language the rest of the app uses and a short framing
line: what the demo is, that a personal Google account is fine, and the cold-start note. One card,
not a landing page.

**OAuth origins.** Add the production Static Web Apps URL to the Google OAuth client's authorized
JavaScript origins alongside `http://localhost:8080`, or sign-in fails outright in production.

**Rate limiting** (already an acceptance criterion above, deferred here from task 07) is still
unbuilt — no `AddRateLimiter` anywhere in `src/QuoteDesk.Api`. Per-IP and per-token limits plus a
hard daily cap on the public demo.

## Out of scope

Custom domain, CDN tuning, autoscaling rules, load testing, blue/green. The README is task 11.

## Notes on completion
