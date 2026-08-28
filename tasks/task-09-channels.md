# Task 09 — Email and WhatsApp channels

**Session 3 · depends on: 04**

## Goal

Enquiries arrive the way this business actually receives them. Because task 04 built the abstraction,
each adapter is small.

## Stack for this task

MailKit (IMAP) · Twilio WhatsApp sandbox (webhook) · optionally a multimodal model call for images

## Ship in this order — the order is the risk ranking

**1. Email — MailKit IMAP poller.** A `BackgroundService` polling an inbox on an interval, mapping
each message to `IncomingEnquiry` with `Channel = Email`. Needs a mailbox and an app password,
nothing else. Mark messages read so they are not reprocessed.

**2. WhatsApp — Twilio sandbox.** A webhook endpoint receiving Twilio's form-encoded POST, mapping to
`IncomingEnquiry` with `Channel = WhatsApp`. You join the sandbox by texting a code from your phone —
no business verification, no waiting. Validate the Twilio signature on every request.

**3. Meta WhatsApp Business Cloud API — optional, and NOT on the critical path.** It needs a Meta
business account, a phone number not already on WhatsApp, and business verification that takes days
and sometimes fails. Build it only if verification comes through. The Twilio sandbox demos
identically. **Never use `whatsapp-web.js` or similar** — against WhatsApp's terms, and it gets
numbers banned.

## Attachments

A real WhatsApp enquiry is often a **photo of a written list** or a **voice note**.

**Images — implement if time allows.** Multimodal models read a photo directly as another content
part on the same chat request. No OCR service, no new dependency. Route image enquiries to the
**`gemini` profile**: GitHub Models caps input around 8K tokens and a phone photo will not fit.
Accuracy on messy handwriting is mediocre, which matters less than it sounds because the result lands
on the approval card for a human to correct.

**Audio — do not implement.** Chat endpoints do not take audio on the OpenAI-compatible path we use,
free transcription options are weak, and code-mixed Gujarati-Hindi-English is unreliable. Store the
file, play it in the UI, mark the enquiry `needs_manual_entry`, let the human type the lines. This is
graceful degradation, and it goes in the README as honest future work.

## Acceptance criteria

- [ ] Email adapter creates an enquiry from a real message in a test mailbox
- [ ] WhatsApp adapter creates an enquiry from a Twilio sandbox message
- [ ] Twilio signature validation rejects a forged request, covered by a test
- [ ] Nothing outside `QuoteDesk.Intake` changed to add either channel — confirm from the diff
- [ ] An attachment-only enquiry lands as `needs_manual_entry` and is visible in the UI
- [ ] Voice notes are stored and playable, never sent to a model
- [ ] If images are implemented: a photo of a typed list produces line items, on the gemini profile

## Out of scope

Replying over WhatsApp. Meta Cloud API unless verification lands. Any audio processing.

## Notes on completion
