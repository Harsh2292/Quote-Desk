using FluentAssertions;
using QuoteDesk.Domain;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

public class SlabDiscountPolicyTests
{
    [Fact]
    public void ResolveDiscountPct_QuantityOneBelowSlabEdge_GetsLowerSlab()
    {
        var pct = SlabDiscountPolicy.ResolveDiscountPct(199, SlabDiscountPolicy.DefaultLadder);

        pct.Should().Be(0.03m);
    }

    [Fact]
    public void ResolveDiscountPct_QuantityExactlyOnSlabEdge_GetsThatSlab()
    {
        // The rule is an inclusive lower bound: a line at exactly 200 units gets the 200+ rate.
        var pct = SlabDiscountPolicy.ResolveDiscountPct(200, SlabDiscountPolicy.DefaultLadder);

        pct.Should().Be(0.06m);
    }

    [Fact]
    public void ResolveDiscountPct_QuantityZero_GetsZeroDiscount()
    {
        var pct = SlabDiscountPolicy.ResolveDiscountPct(0, SlabDiscountPolicy.DefaultLadder);

        pct.Should().Be(0.00m);
    }

    [Fact]
    public void ResolveDiscountPct_NegativeQuantity_Throws()
    {
        var act = () => SlabDiscountPolicy.ResolveDiscountPct(-1, SlabDiscountPolicy.DefaultLadder);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ResolveDiscountPct_QuantityAboveHighestSlab_GetsHighestSlab()
    {
        var pct = SlabDiscountPolicy.ResolveDiscountPct(5_000, SlabDiscountPolicy.DefaultLadder);

        pct.Should().Be(0.09m);
    }
}
