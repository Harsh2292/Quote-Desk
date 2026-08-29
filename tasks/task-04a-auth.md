# Task 04a — Google sign-in and a Users table

**Session 2 · depends on: 02 · unblocks: 04–08**

## Goal

Every `/api/*` route protected by default before task 04 adds the first real endpoint, so no later
task can silently ship one open. Not originally its own numbered task — `docs/SPEC.md` §3 and
`tasks/task-07-api.md` had planned this for task 07 — pulled forward once it became clear that doing
it before any endpoint existed meant a fallback authorization policy could cover every future route
by construction, rather than needing a retrofit pass across eight endpoints later.

## Stack for this task

`Microsoft.AspNetCore.Authentication.JwtBearer` · `Google.Apis.Auth` · `@react-oauth/google`

## What was built

React gets a Google ID token from Google Identity Services and posts it to `POST /api/auth/google`.
The Api verifies it against Google (`GoogleIdTokenValidator`, stubbed in integration tests exactly
the way `IChatClient` is stubbed elsewhere), auto-provisions a `Users` row keyed on the Google
`sub` claim (never the email — an account can change its email, never its subject), and mints its own
short-lived HS256 JWT (`JwtIssuer`) rather than a session cookie, since a cookie would not survive
the two-host split between Static Web Apps and Container Apps that task 09 is expected to use.

Every route is authenticated by default via `AddAuthorizationBuilder().SetFallbackPolicy(...)`; only
`POST /api/auth/google`, `/health/live` and `/health/ready` are `.AllowAnonymous()`. `GET /api/auth/me`
re-reads the database on every call rather than trusting the token's own claims, so a role change in
`Auth:AdminEmails` takes effect on the next request instead of waiting for the token to expire.

`Quote.ApprovedBy` (free-text string) became `ApprovedByUserId int?` (FK, restrict delete) in the same
migration that added `Users` — free to do while `Quotes` was still empty, expensive after task 07
starts writing rows into it.

This expands `docs/SPEC.md` §3 and §9 beyond what they originally scoped ("no user system to build").
Updated in the same commit, alongside `CLAUDE.md`'s stale "valid JWT" line and its `EventSource`
frontend rule, which the bearer-token choice makes inaccurate — `EventSource` cannot send an
`Authorization` header, so `useAgentStream` (task 07/08) will be built on `fetch` +
`ReadableStream`, not `EventSource`.

## Acceptance criteria

- [x] `Users` table exists, auto-provisioned on first Google sign-in
- [x] `POST /api/auth/google` validates a Google ID token and returns a bearer JWT
- [x] `GET /api/auth/me` requires a valid token and returns the current user
- [x] Every other route (present and future) requires auth via a fallback policy, not per-endpoint
      `[Authorize]`
- [x] `/health/live` and `/health/ready` remain reachable with no token
- [x] Integration tests run with no network and no real Google credentials (stubbed validator)
- [x] `docs/SPEC.md`, `CLAUDE.md`, and `tasks/task-07-api.md` updated in the same change

## Out of scope

Rate limiting `POST /api/auth/google` (task 07, alongside the rest of the rate limiter) · role-based
`[Authorize(Roles: ...)]` on any specific endpoint — none exist yet · sign-out on the server (the JWT
is stateless; there is nothing to revoke) · a refresh token (accepted trade-off — see the security
posture note in the implementation plan).

## Notes on completion

**One genuine bug found and fixed along the way, not anticipated in the plan:** Program.cs reads
`ConnectionStrings:QuoteDesk` and `Auth:*` synchronously, before `builder.Build()`. A
`WebApplicationFactory`'s `ConfigureWebHost(...).ConfigureAppConfiguration(...)` override only takes
effect as part of that same `Build()` call — i.e. *after* Program.cs has already read the values — so
an in-memory config override there is silently ignored. Worse: since `ConnectionStrings:QuoteDesk` is
already set locally via `dotnet user-secrets` for the real dev database, the first version of the
integration test factory would have migrated and wiped it, had a different bug (the missing
`Auth:Google:ClientId`) not thrown first and stopped it. Fixed by setting environment variables in
`QuoteDeskApiFactory`'s constructor instead — `WebApplicationBuilder.CreateBuilder(args)` reads
process environment variables synchronously, before any of Program.cs's own code runs, so there is no
ordering gap. Verified afterward: the real `QuoteDesk` database still has all 25 seeded customers.

**What the next task should know:** this is the repo's first `WebApplicationFactory`
(`tests/QuoteDesk.IntegrationTests/Api/QuoteDeskApiFactory.cs`) — task 07's own endpoint tests should
reuse it rather than building a second one. `IUserRepository`/`UserRepository` is also the repo's
first *write* repository; task 04's enquiry-write path can copy its shape (tracked read, explicit
`SaveChangesAsync`) rather than inventing a new one.

**Blocked on Harsh, resolved during the session:** the Google Cloud Console OAuth client (only he can
create it). He registered the JavaScript origin as `http://localhost:8080`, so `vite.config.ts` now
pins the dev server to that port rather than Vite's default 5173, to match.

**Still needs Harsh:** `.claude/settings.json`'s `Read(./.env.*)` deny rule is broader than intended —
it also blocks writing `.env.example`, even though `.gitignore` explicitly allows that file into the
repo (`!.env.example`). Blocked from creating `src/QuoteDesk.Web/.env.example` and
`src/QuoteDesk.Web/.env.local` for this reason; see the session log for the exact content each needs.
