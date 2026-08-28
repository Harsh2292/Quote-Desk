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

- [ ] `IncomingEnquiry` is the only shape any downstream code sees; `EnquiryChannel` never appears in
      `QuoteDesk.Agents`
- [ ] Pasting the worked example from `docs/DOMAIN.md` stores an enquiry and returns its id
- [ ] An attachment-only enquiry stores as `needs_manual_entry` rather than erroring
- [ ] Unit tests on adapter parsing, including empty body, whitespace-only body, and a body of 50KB
- [ ] Adding a new channel later requires no change outside `QuoteDesk.Intake`

## Out of scope

Email and WhatsApp adapters — those are task 09. Building this abstraction now is what makes task 09
half a task instead of a rewrite.

## Notes on completion
