namespace QuoteDesk.Intake;

/// <summary>
/// One file attached to an enquiry — a photo of a written list, a voice note. Shape only for now:
/// no storage exists yet. Task 10's email and WhatsApp adapters are the first to actually produce
/// one, and add the table this record is persisted into at that point (docs/SPEC.md §5).
/// </summary>
public sealed record EnquiryAttachment
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
}
