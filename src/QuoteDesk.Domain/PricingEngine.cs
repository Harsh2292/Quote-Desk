namespace QuoteDesk.Domain;

/// <summary>
/// list price → slab discount → tier discount → line total, per docs/DOMAIN.md. This is the only
/// place a price is computed anywhere in QuoteDesk — the model explains a <see cref="PricedLine"/>,
/// it never produces one.
/// </summary>
public static class PricingEngine
{
    /// <summary>Slab and tier discounts are additive but never exceed this, so a future slab cannot silently give the shop away.</summary>
    public const decimal MaxCombinedDiscountPct = 0.15m;

    public static PricedLine PriceLine(PricingLineRequest request, CustomerTier? tier)
    {
        ArgumentNullException.ThrowIfNull(request);

        var slabs = request.Slabs ?? SlabDiscountPolicy.DefaultLadder;
        var slabPct = SlabDiscountPolicy.ResolveDiscountPct(request.Quantity, slabs);
        var tierPct = TierDiscountPolicy.ResolveDiscountPct(tier);
        var combinedPct = Math.Min(slabPct + tierPct, MaxCombinedDiscountPct);

        var netUnitPrice = Money.Round(request.ListPrice * (1 - combinedPct));
        var lineTotal = Money.Round(netUnitPrice * request.Quantity);

        // A net price of zero (or below) is always a total loss regardless of cost — there is no
        // meaningful ratio to compute, and it is unambiguously below the floor.
        var marginPct = netUnitPrice <= 0m
            ? -1m
            : (netUnitPrice - request.CostPrice) / netUnitPrice;

        return new PricedLine
        {
            Sku = request.Sku,
            Quantity = request.Quantity,
            ListPrice = request.ListPrice,
            DiscountPct = combinedPct,
            NetUnitPrice = netUnitPrice,
            LineTotal = lineTotal,
            MarginPct = marginPct,
            RequiresOverride = MarginFloorPolicy.IsBelowFloor(marginPct),
            MarginShortfallPct = MarginFloorPolicy.Shortfall(marginPct),
        };
    }
}
