using FluentAssertions;
using QuoteDesk.Domain;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

public class MarginFloorPolicyTests
{
    [Fact]
    public void IsBelowFloor_MarginExactlyAtFloor_ReturnsFalse()
    {
        // The rule is "at or above 10%" — exactly at the floor must not require an override.
        MarginFloorPolicy.IsBelowFloor(0.10m).Should().BeFalse();
    }

    [Fact]
    public void IsBelowFloor_MarginOneBasisPointBelowFloor_ReturnsTrue()
    {
        MarginFloorPolicy.IsBelowFloor(0.0999m).Should().BeTrue();
    }

    [Fact]
    public void Shortfall_MarginAtFloor_IsZero()
    {
        MarginFloorPolicy.Shortfall(0.10m).Should().Be(0m);
    }

    [Fact]
    public void Shortfall_MarginBelowFloor_IsTheDifference()
    {
        MarginFloorPolicy.Shortfall(0.07m).Should().Be(0.03m);
    }
}
