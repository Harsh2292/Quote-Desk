namespace QuoteDesk.Data.Entities;

/// <summary>One quantity-break rung, scoped either to a whole <see cref="Target"/> category or one
/// specific SKU. Feeds <see cref="QuoteDesk.Domain.SlabDiscountPolicy"/> — the rule itself stays code,
/// only the numbers live here.</summary>
public class PriceRule
{
    public int Id { get; set; }

    /// <summary>"Category" or "Sku".</summary>
    public required string Scope { get; set; }

    /// <summary>The category name or SKU the rule applies to, per <see cref="Scope"/>.</summary>
    public required string Target { get; set; }

    public required int MinQty { get; set; }
    public required decimal DiscountPct { get; set; }
}
