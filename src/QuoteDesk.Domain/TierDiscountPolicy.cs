namespace QuoteDesk.Domain;

/// <summary>
/// Customer-tier discount, additive with the slab discount and applied to list price. A
/// <see langword="null"/> tier means the sender did not match a known customer record — no
/// customer means no tier discount, though the quantity economics of the slab discount still apply.
/// </summary>
public static class TierDiscountPolicy
{
    public static decimal ResolveDiscountPct(CustomerTier? tier) => tier switch
    {
        CustomerTier.A => 0.04m,
        CustomerTier.B => 0.02m,
        CustomerTier.C => 0.00m,
        null => 0.00m,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown customer tier."),
    };
}
