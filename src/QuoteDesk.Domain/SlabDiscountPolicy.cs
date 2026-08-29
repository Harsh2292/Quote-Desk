namespace QuoteDesk.Domain;

/// <summary>One quantity break: at <see cref="MinQty"/> units or more, <see cref="DiscountPct"/> applies.</summary>
public sealed record QuantitySlab(int MinQty, decimal DiscountPct);

/// <summary>
/// Quantity-break discount lookup, inclusive lower bound — a line at exactly the slab's quantity
/// gets that slab's rate. Slabs are supplied per category from <c>PriceRules</c> data; this policy
/// carries no knowledge of any particular catalogue.
/// </summary>
public static class SlabDiscountPolicy
{
    /// <summary>
    /// The default ladder used when a category has no <c>PriceRules</c> entries of its own.
    /// Matches the 200-unit / 6% rung fixed by the worked example in docs/DOMAIN.md.
    /// </summary>
    public static readonly IReadOnlyList<QuantitySlab> DefaultLadder =
    [
        new QuantitySlab(1, 0.00m),
        new QuantitySlab(50, 0.03m),
        new QuantitySlab(200, 0.06m),
        new QuantitySlab(500, 0.09m),
    ];

    /// <summary>
    /// Resolves the discount for a given quantity: the rate of the highest slab whose
    /// <see cref="QuantitySlab.MinQty"/> is at or below <paramref name="quantity"/>. A quantity
    /// below every slab's minimum (including zero) gets 0%.
    /// </summary>
    public static decimal ResolveDiscountPct(int quantity, IReadOnlyList<QuantitySlab> slabs)
    {
        ArgumentNullException.ThrowIfNull(slabs);
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);

        var discount = 0m;
        foreach (var slab in slabs.OrderBy(s => s.MinQty))
        {
            if (quantity >= slab.MinQty)
            {
                discount = slab.DiscountPct;
            }
        }

        return discount;
    }
}
