# Task 09a — Deployable: model routing, rate limiting, container, CI

**Session 2 · depends on: 08 · split from the original task 09 (docs/SESSION-LOG.md, 2026-09-01)**

## Goal

Everything task 09 needs that can be built and verified on this machine, with no Azure account —
the code fix that stops the demo failing on a rate limit, the container it ships in, and the CI that
proves it builds clean with no API key. Task 09b takes the resulting image and container to a live
URL.

Split out of the original task 09 because it was ten distinct pieces of work — not one sitting.

## Why this had to come first

The first live run of the reworked pipeline (docs/SESSION-LOG.md, 2026-08-31) got through Extract,
Resolve and Price, then died on the **last** call: `gemini-3.6-flash`'s free tier allows 5
requests/minute, and one run makes ~6 sequential calls in well under a minute. Deploying that
pipeline unchanged would mean a public demo that fails on a rate limit roughly every time it runs.

## What was built

- **Per-stage model routing** — `LlmOptions.ExtractModel`/`ResolveModel`/`NarrateModel` (falling
  back to `Model`), `QuoteDesk.Agents.Llm.ChatClientRegistry` (one cached client per distinct model
  name), `EnquiryPipeline` rewired onto it. Extract/Narrate on `gemini-3.5-flash-lite`, Resolve on
  `gemini-3.6-flash` — two models, two independent requests-per-minute buckets. The trace now records
  which model answered each stage (`StageEvent.Model`).
- **A "try a sample enquiry" picker** on the Desk (Harsh's own addition to this task) — five samples,
  each shipping a sender and body together, verified against the running seeded database.
- **The sign-in screen, rebuilt** on the shared design primitives — fixes a swallowed Google
  `onError`, no in-flight state, and a silently-dropped empty credential.
- **Rate limiting** — `Microsoft.AspNetCore.RateLimiting`, no new package. A `GlobalLimiter` protects
  every route by default; `auth` and `pipeline` are stricter policies stacked on top of exactly the
  two routes that need them (an anonymous Google-verification call, and the one route that spends the
  shared Gemini key). See docs/SPEC.md §8 for the full shape.
- **CORS** — already built in task 07; nothing to do here beyond the config in task 09b.
- **Deployment seams**: `VITE_API_BASE_URL` (the two-host split), `EnableRetryOnFailure()` (Azure SQL
  auto-pause), `Database:MigrateOnStartup`/`SeedOnStartup` (nothing else creates the schema),
  `global.json` (SDK feature-band pin).
- **Dockerfile + `.dockerignore` + a `docker-compose.yml` `api` service** — multi-stage, non-root,
  verified live: builds, runs as uid 1654, migrates + seeds + answers both health endpoints against
  the compose `sql` service.
- **`.github/workflows/ci.yml`** — build (Debug and Release) → test → image, no API key, no network.
- **A real build-time gap found and fixed**: `dotnet build` (Debug) never triggers `CA1848`
  (`LoggerMessage` delegates); only `dotnet publish -c Release` does, and nothing in this project had
  published Release before this task's Dockerfile. Three call sites fixed properly; CLAUDE.md's
  Commands section now lists the Release build so this cannot silently recur.

## Acceptance criteria

- [x] `docker build` produces an image that runs the API with a non-root user
- [x] `docker compose up -d` still works locally, now including the `api` service
- [x] `dotnet build QuoteDesk.sln -warnaserror` and `-c Release -warnaserror` both clean
- [x] `dotnet test --filter "FullyQualifiedName!~Evals"` passes with no API key present
- [x] `npm run build` and `npm run lint` pass
- [x] Rate limiting is active, with a hard cap on the pipeline route sized against the shared model key
- [x] One full worked-example enquiry run live, through the containerised API, confirming Extract and
      Narrate answer on the Lite model, Resolve on the capable one, and no 429 occurs — done by Harsh
      (enquiry #3003, 2026-09-01): 52.68s total, Extract 1.69s/Lite, Resolve 49.59s/capable, Narrate
      ~1.4s/Lite, approved and sent as QTN-2026-0001. Four real bugs found from that one run, all
      fixed same day (see docs/SESSION-LOG.md).
- [ ] CI is green on a pushed branch — nothing has been pushed yet

## Out of scope

Everything in task 09b (Azure, the live URL, cold start). Custom domain, CDN tuning, autoscaling
rules, load testing, blue/green. The README is task 11.

## Notes on completion

Docs updated in the same pass: docs/SPEC.md §4 (model routing) and §8 (rate limiting, the
`demo_rate_limited` frontend distinction), CLAUDE.md's Commands section (the Release-build gap).

`docker compose up -d --build` verified for real once Harsh created `.env` — `sql` healthy, `api`
built, started, migrated, seeded, uid 1654, both health checks 200. The remaining two acceptance
criteria both need a real Google sign-in (browser) and are staying his to drive — see
docs/SESSION-LOG.md.

The two open acceptance criteria are deliberately last: they need Docker up, a database seeded, and
one real (quota-respecting) Gemini call, done once as a final end-to-end pass rather than repeated
through the session.
