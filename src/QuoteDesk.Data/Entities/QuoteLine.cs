namespace QuoteDesk.Data.Entities;

public class QuoteLine
{
    public int Id { get; set; }
    public required int QuoteId { get; set; }
    public required string Sku { get; set; }
    public required int Qty { get; set; }
    public required decimal UnitPrice { get; set; }
    public required decimal DiscountPct { get; set; }
    public required decimal LineTotal { get; set; }

    /// <summary>Whether this line's net margin fell below the 10% floor and needs a human override,
    /// per docs/DOMAIN.md. The margin figure itself is never stored here — only the flag.</summary>
    public required bool RequiresOverride { get; set; }

    public DateOnly? DispatchDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? Note { get; set; }

    public Quote? Quote { get; set; }
    public CatalogItem? CatalogItem { get; set; }
}
