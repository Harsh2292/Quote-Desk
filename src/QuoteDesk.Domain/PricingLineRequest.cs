namespace QuoteDesk.Domain;

/// <summary>One requested line, as handed to <see cref="PricingEngine"/>. Nothing but numbers — no
/// catalogue lookup and no I/O happens inside the domain.</summary>
public sealed record PricingLineRequest
{
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required decimal ListPrice { get; init; }
    public required decimal CostPrice { get; init; }

    /// <summary>The category's quantity-break ladder. Defaults to <see cref="SlabDiscountPolicy.DefaultLadder"/> when omitted.</summary>
    public IReadOnlyList<QuantitySlab>? Slabs { get; init; }
}
