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
    public string? Note { get; set; }

    public Quote? Quote { get; set; }
    public CatalogItem? CatalogItem { get; set; }
}
