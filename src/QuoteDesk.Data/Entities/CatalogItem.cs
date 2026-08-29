namespace QuoteDesk.Data.Entities;

public class CatalogItem
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Uom { get; set; }
    public required decimal ListPrice { get; set; }

    /// <summary>Never leaves the server and never reaches the model — see docs/SPEC.md §6.</summary>
    public required decimal CostPrice { get; set; }

    /// <summary>The one distinguishing attribute for near-identical variants, e.g. "6mm" vs "8mm".</summary>
    public string? Attributes { get; set; }

    public StockLevel? StockLevel { get; set; }
}
