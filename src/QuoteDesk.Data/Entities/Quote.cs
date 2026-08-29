namespace QuoteDesk.Data.Entities;

public class Quote
{
    public int Id { get; set; }
    public required int EnquiryId { get; set; }

    /// <summary>e.g. "QTN-2026-0841".</summary>
    public required string Number { get; set; }

    /// <summary>"draft" / "approved" / "sent" / "rejected".</summary>
    public required string Status { get; set; }

    public required decimal Subtotal { get; set; }
    public required decimal Tax { get; set; }
    public required decimal Total { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    public Enquiry? Enquiry { get; set; }
    public List<QuoteLine> Lines { get; set; } = [];
}
