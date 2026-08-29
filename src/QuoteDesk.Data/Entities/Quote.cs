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
    public required decimal Freight { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>15 days from issue, per docs/DOMAIN.md.</summary>
    public required DateTimeOffset ValidUntil { get; set; }

    /// <summary>The customer's stated delivery destination for this quote, once the Extract stage
    /// (task 06) supplies one. Null until then.</summary>
    public string? ShipTo { get; set; }

    /// <summary>The date the customer asked for, as written in the enquiry (e.g. "need by 5th"),
    /// once the Extract stage supplies one. Null until then.</summary>
    public DateOnly? RequiredBy { get; set; }

    /// <summary>The signed-in salesperson who approved this quote. Null until approval.</summary>
    public int? ApprovedByUserId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }

    public AppUser? ApprovedByUser { get; set; }
    public Enquiry? Enquiry { get; set; }
    public List<QuoteLine> Lines { get; set; } = [];
}
