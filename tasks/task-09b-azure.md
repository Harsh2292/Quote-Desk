# Task 09b — Azure: provisioning, CD, the live URL

**Session 2 (or 3) · depends on: 09a**

## Goal

Take the image and workflow task 09a produced and put them on a public URL — Container Apps for the
Api, Azure SQL free offer, Static Web Apps for the React build, a CD workflow, and the cold-start
number written down. From this point on there is always something live to click.

## Before starting

`az` CLI is **not installed** on this machine (checked at task 09a planning time; Docker and the
.NET SDK are). Install it, then `az login` — this is Harsh's to run; everything after that this
session can drive from the CLI.

## What to build

- Resource group; an image registry (ACR, or GHCR with Container Apps registry auth — decide which
  when this task starts, based on what's simplest to wire into the CD workflow).
- **Azure SQL free offer — choose auto-pause, not pay-overage. That choice is irreversible.** Create
  the empty database; the app migrates and seeds itself on first boot
  (`Database:MigrateOnStartup`/`SeedOnStartup`, task 09a).
- Container App, `min replicas 0`. Liveness probe on `/health/live` **only** — `/health/ready` runs
  the SQL check, and polling it would keep waking an auto-paused database and burn the free
  vCore-second grant.
- Container Apps secrets: `ConnectionStrings__QuoteDesk`, `Auth__Google__ClientId`,
  `Auth__Jwt__SigningKey` (≥32 bytes), `Auth__AdminEmails__0`, `Auth__AllowedOrigins__0` (the deployed
  Static Web Apps URL — CORS is already built, task 07; without this env var the browser is blocked),
  `Llm__ApiKey`, `Database__MigrateOnStartup=true`, `Database__SeedOnStartup=true` (once, then leave
  on — `DeterministicSeeder` is idempotent), `ASPNETCORE_ENVIRONMENT=Production`.
- Static Web Apps free tier for the React build, with `VITE_GOOGLE_CLIENT_ID` and
  `VITE_API_BASE_URL` (task 09a) set at build time.
- **Direct cross-origin calls to the Container App, not an SWA proxy route.** SWA's proxy buffers
  responses, which breaks SSE; `AgentEventStreamWriter` already sets `X-Accel-Buffering: no` for
  exactly this reason.
- Add the Static Web Apps origin to the Google OAuth client's authorized JavaScript origins,
  alongside `http://localhost:8080`. Sign-in fails outright in production without it.
- A CD workflow: build + push the image, `az containerapp update`, an SWA deploy action.
- A ₹0 budget alert on the subscription. Free tiers change.
- Measure cold start — the first request after idle — and write the number down in
  `docs/SESSION-LOG.md` and in the sign-in screen's own copy (task 09a already left a placeholder
  line there for it).

## Acceptance criteria

- [x] Live URL opens in a browser that has never seen it, cold
- [x] The worked example from `docs/DOMAIN.md` runs end to end on the live site: paste → trace →
      ambiguity flagged → approval → quote — enquiry #13 → QTN-2026-0001, 32.71s, no rate limit
- [x] A push to `main` deploys automatically and the pipeline is green
- [x] Budget alert configured (₹1 backstop)
- [x] Cold start measured and recorded — 26.4s typical, ~50s worst case with Azure SQL also resuming

## Out of scope

Custom domain, CDN tuning, autoscaling rules, load testing, blue/green. The README is task 11.

## Notes on completion

*(Reconstructed — this task's original "Notes on completion" was lost along with other uncommitted
work in an unrelated `git reset --hard`; this section carries the same facts docs/SESSION-LOG.md's
2026-09-02 entry and docs/SPEC.md §10 record, not a byte-for-byte restoration of the original prose.)*

**Shipped:** QuoteDesk live at https://nice-stone-04dc8f600.5.azurestaticapps.net (Static Web Apps,
Free SKU) → https://quotedesk-api.icyground-3aeb2921.centralindia.azurecontainerapps.io (Container
Apps, Consumption, `min-replicas 0`). Azure SQL migrates and seeds itself on first boot. CD
(`.github/workflows/cd.yml`) deploys on every push to `main` via GitHub OIDC — no stored Azure
secret — and pulls a public GHCR image (`ghcr.io/harsh2292/quotedesk-api`), so no Azure Container
Registry and no registry credentials either.

**Cost guards, all confirmed in place:** Azure SQL free-limit + AutoPause, Container Apps
`min-replicas 0` / `max-replicas 1`, Log Analytics capped at 0.1 GB/day ingestion, a ₹1 budget alert
as a backstop.

**A pre-existing gap surfaced by the real Google validator:** `POST /api/auth/google` returns 500,
not 401, on a malformed or absent token — `GoogleIdTokenValidator.ValidateAsync` lets a
`FormatException`/`ArgumentException` escape rather than mapping it to 401. Harmless in practice (a
real Google-issued token is always well-formed), but a real bug worth fixing eventually.

**Three deploy-time snags** (see docs/SESSION-LOG.md's fuller planning entries for context): the
device-code Azure login flow, Git Bash path mangling on Windows when invoking `az`, and GitHub's
newer immutable OIDC subject-claim format needing the federated credential's subject configured to
match exactly.
