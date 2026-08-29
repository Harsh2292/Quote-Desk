namespace QuoteDesk.Domain;

/// <summary>One priced line — the result of <see cref="PricingEngine.PriceLine"/>. Cost price never
/// appears here: it fed the margin check but must not leave the server or reach the model.</summary>
public sealed record PricedLine
{
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required decimal ListPrice { get; init; }

    /// <summary>Slab discount + tier discount, capped at <see cref="PricingEngine.MaxCombinedDiscountPct"/>.</summary>
    public required decimal DiscountPct { get; init; }
    public required decimal NetUnitPrice { get; init; }
    public required decimal LineTotal { get; init; }

    /// <summary>Net margin as a fraction (0.14 = 14%). Never surfaced to the model — see docs/DOMAIN.md.</summary>
    public required decimal MarginPct { get; init; }
    public required bool RequiresOverride { get; init; }
    public required decimal MarginShortfallPct { get; init; }
}
