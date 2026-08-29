using QuoteDesk.Domain;

namespace QuoteDesk.Data.Entities;

public class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? EmailDomain { get; set; }
    public string? WhatsAppNumber { get; set; }
    public required CustomerTier Tier { get; set; }
    public required int CreditDays { get; set; }
    public string? GstIn { get; set; }
    public string? DefaultShipTo { get; set; }
}
