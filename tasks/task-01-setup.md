# Task 01 — Setup and skeleton

**Session 1 · depends on: nothing**

## Goal

A solution that builds clean, a database container that starts, an API that answers, and a React app
that talks to it. Nothing else.

## Stack for this task

.NET 10 SDK · ASP.NET Core Minimal APIs · Docker Compose with SQL Server 2022 · Vite + React 19 +
TypeScript + Tailwind

## What to build

- `QuoteDesk.sln` with six source projects and three test projects:
  `Domain · Data · Agents · Intake · Api · Web` and `tests/UnitTests · IntegrationTests · Evals`
- Project references wired in one direction only: `Api → Agents → Data → Domain`, `Intake → Api`.
  `Domain` references nothing.
- `Directory.Build.props` applies to all projects; `dotnet build -warnaserror` is clean
- `docker-compose.yml` starting SQL Server with a named volume
- `GET /health/live` and `GET /health/ready` in the API
- Vite app that calls `/health/live` and shows the result
- Serilog wired with console output — the full logging setup lands in task 07, but nothing should
  ever be written with `Console.WriteLine`

## Acceptance criteria

- [x] `dotnet build QuoteDesk.sln -warnaserror` succeeds with zero warnings
- [x] `dotnet test` runs and reports zero tests, not an error — true at this task's own checkpoint;
      see Notes on completion for why real tests exist by the time this file was closed
- [x] `docker compose up -d` brings up SQL Server and it accepts a connection
- [x] `curl localhost:<port>/health/live` returns 200
- [x] `npm run build` in `src/QuoteDesk.Web` succeeds
- [x] The React app displays the API's health status
- [x] One commit, conventional message

## Out of scope

Any table, any entity, any business logic, authentication, any agent, any styling beyond default.

## Notes on completion

Built together with tasks 02 and 03 in one session — see those files' Notes on completion for why,
and `docs/SESSION-LOG.md` for the full session. This note covers task 01's own scope only.

**What was built:** `QuoteDesk.sln` with five .NET source projects (`Domain · Data · Agents · Intake
· Api`) and three test projects, wired `Api → Agents → Data → Domain` and `Api → Intake → Data`
(**not** `Intake → Api` as this file and `CLAUDE.md` originally said — fixed in the same commit; see
`CLAUDE.md`'s dependency-graph line for why). `Program.cs` is hand-written, not templated: Serilog to
console, `/health/live` (always answers, `Predicate = _ => false`) and `/health/ready` (runs every
registered check). `docker-compose.yml` runs SQL Server 2022 with a healthcheck and a named volume.
The Vite app is a plain `npm` folder, not a project in the `.sln` — Tailwind v4 via the
`@tailwindcss/vite` plugin, dev-server proxy on `/health` and `/api` so the browser never needs CORS.

**What surprised me:**
- `dotnet new sln` on this SDK now **defaults to the new `.slnx` XML format**, not `.sln`. Every task
  file and `CLAUDE.md`'s Commands section name `QuoteDesk.sln` explicitly, so I forced the classic
  format with `--format sln` rather than update every reference.
- The `webapi` template's own `Microsoft.AspNetCore.OpenApi` package reference failed restore outright
  under `-warnaserror`: NuGet resolved `Microsoft.OpenApi 2.0.0` for .NET 10, which carries a known
  high-severity advisory (`NU1903`). Task 01 needs no Swagger UI, so the package was removed rather
  than the warning suppressed. Recorded in `docs/SPEC.md` §3 in case a later task wants OpenAPI back.
- Docker Desktop's daemon was down at the start of the session; once restarted, `docker compose up`
  and even plain `docker pull` failed identically against `mcr.microsoft.com` with `Head ... EOF` —
  traced to the containerd image-store snapshotter (`UseContainerdSnapshotter: true` in Docker
  Desktop's own settings), a known bad interaction between that snapshotter's `HEAD`-based manifest
  resolution and `mcr.microsoft.com`'s CDN. Fixed by flipping that one setting off and restarting
  Docker Desktop — a local dev-machine toggle, not a code change, so nothing in the repo reflects it.
- Empty xUnit projects were the actual risk this task's second criterion called out. Confirmed
  directly: `dotnet test` against three empty test projects exits **0** with "No test matches..." —
  never an error. By the time this file closed, tasks 02 and 03 had already added 35 unit tests and
  14 integration tests, so the criterion is true in the stronger sense too.

**What the next task should know:** task 04 is genuinely next — the Intake abstraction and paste
adapter. `IEnquiryRepository` already exists (read-only, `GetByIdAsync`) from task 02; task 04 adds
the write path (`AddAsync` or equivalent) that `PasteAdapter` calls.
