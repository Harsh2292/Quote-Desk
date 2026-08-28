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

- [ ] `dotnet build QuoteDesk.sln -warnaserror` succeeds with zero warnings
- [ ] `dotnet test` runs and reports zero tests, not an error
- [ ] `docker compose up -d` brings up SQL Server and it accepts a connection
- [ ] `curl localhost:<port>/health/live` returns 200
- [ ] `npm run build` in `src/QuoteDesk.Web` succeeds
- [ ] The React app displays the API's health status
- [ ] One commit, conventional message

## Out of scope

Any table, any entity, any business logic, authentication, any agent, any styling beyond default.

## Notes on completion

<!-- Written when the task closes: what was built, what surprised you, what the next task should know. -->
