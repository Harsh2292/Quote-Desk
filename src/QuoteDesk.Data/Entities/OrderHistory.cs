namespace QuoteDesk.Data.Entities;

public class OrderHistory
{
    public int Id { get; set; }
    public required int CustomerId { get; set; }
    public required string Sku { get; set; }
    public required int Qty { get; set; }
    public required decimal UnitPrice { get; set; }
    public required DateTimeOffset OrderedAt { get; set; }

    public Customer? Customer { get; set; }
    public CatalogItem? CatalogItem { get; set; }
}
