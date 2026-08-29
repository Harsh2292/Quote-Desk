using FluentAssertions;
using QuoteDesk.Domain;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

public class PricingEngineTests
{
    [Fact]
    public void PriceLine_UnknownCustomer_AppliesSlabButNotTierDiscount()
    {
        var request = new PricingLineRequest
        {
            Sku = "BRG-6203-2RS",
            Quantity = 250,
            ListPrice = 250.00m,
            CostPrice = 197.80m,
        };

        var line = PricingEngine.PriceLine(request, tier: null);

        // 250 units crosses the 200+ slab (6%); no tier discount because there is no customer match.
        line.DiscountPct.Should().Be(0.06m);
        line.NetUnitPrice.Should().Be(235.00m);
    }

    [Fact]
    public void PriceLine_QuantityZero_LineTotalIsZero()
    {
        var request = new PricingLineRequest
        {
            Sku = "BRG-6203-2RS",
            Quantity = 0,
            ListPrice = 250.00m,
            CostPrice = 197.80m,
        };

        var line = PricingEngine.PriceLine(request, CustomerTier.B);

        line.LineTotal.Should().Be(0m);
    }

    [Fact]
    public void PriceLine_DiscountWouldBreachMarginFloor_FlagsRequiresOverrideWithShortfall()
    {
        // Slab 6% (200+) + tier A 4% = 10% off a line with only a 10% list-to-cost spread —
        // the net margin lands at exactly 0%, ten points under the floor.
        var request = new PricingLineRequest
        {
            Sku = "GEAR-90",
            Quantity = 200,
            ListPrice = 100.00m,
            CostPrice = 90.00m,
        };

        var line = PricingEngine.PriceLine(request, CustomerTier.A);

        line.MarginPct.Should().Be(0.00m);
        line.RequiresOverride.Should().BeTrue();
        line.MarginShortfallPct.Should().Be(0.10m);
    }

    [Fact]
    public void PriceLine_CombinedDiscountWouldExceedCap_ClampsAtMaxCombinedDiscountPct()
    {
        var request = new PricingLineRequest
        {
            Sku = "SPINDLE-TAPE-8MM",
            Quantity = 1_000,
            ListPrice = 100.00m,
            CostPrice = 50.00m,
            Slabs = [new QuantitySlab(1, 0.12m)],
        };

        // 12% slab + 4% tier A would be 16%, but the combined cap is 15%.
        var line = PricingEngine.PriceLine(request, CustomerTier.A);

        line.DiscountPct.Should().Be(PricingEngine.MaxCombinedDiscountPct);
    }
}
