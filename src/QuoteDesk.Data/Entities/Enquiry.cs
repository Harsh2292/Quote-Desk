namespace QuoteDesk.Data.Entities;

public class Enquiry
{
    public int Id { get; set; }

    /// <summary>"Paste" / "Email" / "WhatsApp" — a plain string, not the <c>EnquiryChannel</c> enum.
    /// That enum is owned by QuoteDesk.Intake and must never appear outside it (docs/SPEC.md §5);
    /// the adapter that persists an enquiry converts to and from this column.</summary>
    public required string Channel { get; set; }

    public required string SenderId { get; set; }
    public required string RawBody { get; set; }
    public required DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Null when the sender matched no known customer — see docs/DOMAIN.md, "Unknown sender".</summary>
    public int? CustomerId { get; set; }

    public required string Status { get; set; }

    /// <summary>The signed-in salesperson who created this enquiry — null for the seeded demo data
    /// and anything created before this column existed. A stranger who signs in to the public demo
    /// must only ever see and act on their own enquiries; a null-owned row belongs to nobody and is
    /// invisible everywhere ownership is checked, which is the deliberate clean slate for
    /// pre-existing rows rather than a migration hazard to work around.</summary>
    public int? OwnerUserId { get; set; }

    public Customer? Customer { get; set; }
    public AppUser? OwnerUser { get; set; }
}
