using FluentAssertions;
using QuoteDesk.Domain;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

public class TierDiscountPolicyTests
{
    [Theory]
    [InlineData(CustomerTier.A, 0.04)]
    [InlineData(CustomerTier.B, 0.02)]
    [InlineData(CustomerTier.C, 0.00)]
    public void ResolveDiscountPct_KnownTier_ReturnsTierRate(CustomerTier tier, double expected)
    {
        TierDiscountPolicy.ResolveDiscountPct(tier).Should().Be((decimal)expected);
    }

    [Fact]
    public void ResolveDiscountPct_UnknownSender_ReturnsZero()
    {
        // No customer match means no tier discount — see docs/DOMAIN.md, "Unknown sender".
        TierDiscountPolicy.ResolveDiscountPct(tier: null).Should().Be(0.00m);
    }
}
