# Task 04 — Intake abstraction and paste adapter

**Session 2 · depends on: 02**

## Goal

One channel-agnostic shape for an incoming enquiry, and the one adapter that always works. Everything
downstream is written against this and never learns where an enquiry came from.

## Stack for this task

Plain C# records · ASP.NET Core minimal endpoint

## What to build

In `QuoteDesk.Intake`:

```csharp
public sealed record IncomingEnquiry
{
    public required EnquiryChannel Channel { get; init; }   // Paste | Email | WhatsApp
    public required string SenderId { get; init; }          // email address or phone number
    public required string Body { get; init; }              // may be empty when attachment-only
    public required DateTimeOffset ReceivedAt { get; init; }
    public IReadOnlyList<EnquiryAttachment> Attachments { get; init; } = [];
}
```

- `IEnquiryIntakeAdapter` with a single `PasteAdapter` implementation
- Persistence into `Enquiries`, returning an id
- An enquiry with no usable text and only attachments is stored with status
  `needs_manual_entry` — it does **not** fail, and it does **not** get sent to the model
- `POST /api/enquiries` accepting pasted text

## Acceptance criteria

- [x] `IncomingEnquiry` is the only shape any downstream code sees; `EnquiryChannel` never appears in
      `QuoteDesk.Agents`
- [x] Pasting the worked example from `docs/DOMAIN.md` stores an enquiry and returns its id
- [x] An attachment-only enquiry stores as `needs_manual_entry` rather than erroring
- [x] Unit tests on adapter parsing, including empty body, whitespace-only body, and a body of 50KB
- [x] Adding a new channel later requires no change outside `QuoteDesk.Intake`

## Out of scope

Email and WhatsApp adapters — those are task 09. Building this abstraction now is what makes task 09
half a task instead of a rewrite.

## Notes on completion

Built as planned: `IncomingEnquiry`, `EnquiryChannel`, `EnquiryAttachment` and `IEnquiryIntakeAdapter`
live in `QuoteDesk.Intake` with a single `PasteAdapter` implementation; `EnquiryStatusRule` is the
one place the "blank body → `needs_manual_entry`" decision is made, shared by every future adapter.
`IEnquiryRepository` gained `CreateAsync` (it was read-only since task 02). `POST /api/enquiries`
sits behind the fallback auth policy from task 04a with no extra wiring — that was the point of
building auth first. `CustomerId` is left null at intake deliberately: matching a sender to a
customer is `resolve_customer`'s job in the Resolve stage (task 06), not intake's.

Attachments are a shape only, on purpose — no `EnquiryAttachments` table exists yet, since the paste
channel can never produce one. Task 10 adds storage when email and WhatsApp actually deliver files.

**Boundary enforced by a test, not just a comment:** `IntakeBoundaryTests` scans every `.cs` file
under `QuoteDesk.Agents` and `QuoteDesk.Api` for the literal string `EnquiryChannel` and fails if it
appears — the acceptance criterion is checked mechanically, in the style of `DomainPurityTests`.

**Found and fixed while writing the integration tests:** `AuthEndpointsTests` and the new
`EnquiryEndpointsTests` each declared their own `IClassFixture<QuoteDeskApiFactory>`, so xUnit ran
them in separate collections in parallel — two factories racing to `EnsureDeleted`/`Migrate` the same
`QuoteDeskTests_Api` database at once, intermittently failing with "Database already exists" or, once
serialized onto one shared instance, a real bug: my new tests reused `kiran@shreejitextiles.example`,
a literal `AuthEndpointsTests` already provisions with a different Google subject, colliding on
`Users.IX_Users_Email`. Fixed by sharing one `[Collection("QuoteDeskApi")]` fixture across every
Api-hitting test class, and giving every test a distinct email. Any future `QuoteDeskApiFactory` test
class must join this same collection, not declare its own `IClassFixture`.

78/78 tests passing (52 unit + 26 integration), 0 warnings under `-warnaserror`.
