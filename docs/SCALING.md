# Scaling QuoteDesk beyond a demo

**Why none of this is built: QuoteDesk is a portfolio project, not a production system.** Its whole
design is scoped around what proves the interesting idea — an agentic pipeline with a deterministic
pricing core and a human approval gate — on infrastructure that costs ₹0 (`docs/SPEC.md` §10). Every
item below is real, reasoned engineering, and every one of them would also be the wrong thing to build
here: added complexity and Azure cost with no demo it would improve, for load this project will never
actually see. That is the whole reason this is a document instead of a task list.

This exists because the question came up while closing out the demo — "what would it take to scale
this to millions of users?" — and the honest answer was worth writing down rather than losing. If a
future version of this project (or a similar one) ever needed to grow past a portfolio demo, this is
the map of what that actually requires — in the order that would matter, not the order that's easiest
to talk about.

## The two things people usually get wrong on this question

**Load balancers and read replicas are not the hard part.** They're the *expected* answer, and
QuoteDesk already has a head start on both — see below. The two things that actually determine
whether this scales are further down this list: decoupling the slow work from the request/response
cycle, and the fact that the real bottleneck isn't your own infrastructure at all.

**The real, measured bottleneck tonight was an external provider's queue, not this app's code.**
Server logs from a real run showed every SQL query in the 1–23ms range, and a multi-minute wait with
zero database activity in between — the entire delay was Gemini's free tier holding the request
before answering. No amount of scaling *this app's* infrastructure fixes a ceiling imposed upstream.
That's the single most important fact in this whole document, and it's the one a generic "how to
scale a web app" answer would miss entirely.

## 1. Load balancing + stateless replicas — the easy part, and already halfway there

QuoteDesk's auth is a bearer JWT, not a session cookie (`docs/SPEC.md` §3) — deliberately, since a
cookie wouldn't survive the Static Web Apps / Container Apps host split. That same property is what
makes horizontal scaling straightforward: any replica can serve any request, with no shared session
state to synchronize. A lot of systems have to redesign auth just to get here; this one already did,
for an unrelated reason.

## 2. Decouple accepting the request from doing the work — the one people miss

Right now, one HTTP connection stays open for an entire agent run (`AgentEventStreamWriter`'s SSE
stream) — seconds to, as observed live, several minutes. At real volume you cannot hold a connection
open per active run; connection count alone would exhaust a server long before CPU or memory did.

The real shape: `POST /api/enquiries/{id}/process` returns a job id immediately. A message queue
(Azure Service Bus, Kafka, SQS) hands the job to a pool of **worker processes that are not web
servers** — they just run `EnquiryPipeline`, which already knows how to suspend and resume via
`Microsoft.Agents.AI.Workflows` checkpoints (`SqlCheckpointStore`). Results stream out through a thin,
separate real-time gateway (or the client polls). This turns "one server holds one slow connection"
into "any worker can pick up any job," which is the actual lever that lets you add capacity.

`AgentRuns`/`WorkflowCheckpoints` already being separate, checkpointable state (not held in one
process's memory — see `docs/SPEC.md` §6) is not an accident of this redesign; it already exists for
a different reason (resuming a failed run past Resolve, task 09a) and turns out to be exactly the
property this decoupling needs.

## 3. The LLM provider's own capacity — the actual ceiling

This project's free-tier key is capped at ~20 requests/day, 5/minute on the capable model
(`docs/SPEC.md` §4) — deliberately chosen to keep the demo at ₹0. At real scale, none of the other
items on this list matter until this one is solved: a paid contract with a genuine throughput
guarantee, multiple provider keys load-balanced across accounts, or — for a ceiling you actually
control rather than rent — self-hosted inference on dedicated GPU capacity (vLLM, TensorRT). Every
other component here can be added incrementally as load grows; this one is binary — without it,
nothing past it helps.

## 4. Caching in front of the database

`resolve_customer` and `search_catalog` run on *every* enquiry, against data that changes rarely (a
catalogue, a customer list). A cache (Redis or similar) in front of those specific reads removes the
majority of database load before it ever reaches SQL Server. Not needed at this project's actual
traffic — every query measured tonight was single-digit milliseconds — but it's the first thing to
add once traffic is real, because it's the cheapest win with the least architectural change.

## 5. Database read replicas, then partitioning

One SQL Server instance has a ceiling. Reads (catalogue, customer, order history — the bulk of
traffic) fan out to replicas first; writes (new enquiries, new quotes) stay on the primary until that
alone becomes the bottleneck. Past that point: sharding by customer, or a distributed SQL variant
(Azure SQL Hyperscale and similar exist specifically for this).

## 6. A separate store for trace data

`AgentRuns.TraceJson` (`docs/SPEC.md` §8) is a large, append-only JSON blob per run, sitting in a
relational `nvarchar(max)` column. That's the right shape for a 12-enquiry demo and the wrong shape
at volume — it belongs in object storage or a document store, with the SQL row holding a pointer to
it instead of the blob itself.

## 7. An API gateway at the edge, not in-process rate limiting

Rate limiting currently runs inside the ASP.NET process itself (`Program.cs`'s `AddRateLimiter`) — the
right choice for a single-instance demo, since it needs zero extra infrastructure. At real scale that
moves to a dedicated edge layer (Azure API Management or equivalent), so throttling decisions happen
before a request consumes any application compute at all.

## 8. Multi-region deployment with geo-routing

"Millions of users" implies a global user base. That means the stack deployed in multiple regions
behind something like Azure Front Door, each region with its own read replica, accepting eventual
consistency on reads in exchange for low latency everywhere — a real trade-off, not a free upgrade.

## 9. Idempotency as a first-class property

At real request volume, network retries are constant — every write path needs to be safe to receive
twice. This is the one place QuoteDesk already has a genuine head start for a reason unrelated to
scaling at all: the workflow checkpoints after every stage (`WorkflowCheckpoints`, `docs/SPEC.md` §6)
exist so a *failed* run can resume past Resolve without repeating expensive work — the exact
resumability property idempotent-at-scale design needs, arrived at from a completely different
motivation.

## What this document is not

Not a roadmap, not a task list, not a suggestion that any of this belongs in a portfolio demo sized
for one Azure free tier. `CLAUDE.md`'s own rule stands: multi-tenancy and anything past what the demo
needs is explicitly out of scope (`docs/SPEC.md` §9). This is here so the reasoning survives past the
conversation it came from — nothing more.
