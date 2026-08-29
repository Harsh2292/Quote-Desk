namespace QuoteDesk.Intake;

/// <summary>
/// One channel-agnostic shape for an incoming enquiry. Everything downstream of intake is written
/// against this record and never learns where an enquiry came from (docs/SPEC.md §5).
/// </summary>
public sealed record IncomingEnquiry
{
    public required EnquiryChannel Channel { get; init; }

    /// <summary>The email address or phone number the enquiry arrived from.</summary>
    public required string SenderId { get; init; }

    /// <summary>May be empty when the enquiry is attachment-only (e.g. a photo with no caption).</summary>
    public required string Body { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    public IReadOnlyList<EnquiryAttachment> Attachments { get; init; } = [];
}
