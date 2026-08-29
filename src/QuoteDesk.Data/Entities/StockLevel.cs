namespace QuoteDesk.Data.Entities;

/// <summary>Keyed by <see cref="Sku"/> — one stock record per catalogue item, not a surrogate id.</summary>
public class StockLevel
{
    public required string Sku { get; set; }
    public required int OnHand { get; set; }
    public required int LeadTimeDays { get; set; }
    public required int ReorderLevel { get; set; }

    public CatalogItem? CatalogItem { get; set; }
}
