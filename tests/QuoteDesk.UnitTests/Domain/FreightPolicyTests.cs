using FluentAssertions;
using QuoteDesk.Domain;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

public class FreightPolicyTests
{
    [Fact]
    public void ResolveFreight_TaxableValueExactlyAtThreshold_StillCharges()
    {
        // "Waived above" a threshold — exactly at it is not yet above it.
        var freight = FreightPolicy.ResolveFreight(FreightZone.Regional, FreightPolicy.WaiverThreshold);

        freight.Should().Be(450m);
    }

    [Fact]
    public void ResolveFreight_TaxableValueJustAboveThreshold_IsWaived()
    {
        var freight = FreightPolicy.ResolveFreight(FreightZone.Regional, FreightPolicy.WaiverThreshold + 0.01m);

        freight.Should().Be(0m);
    }

    [Theory]
    [InlineData(FreightZone.Local, 0)]
    [InlineData(FreightZone.Regional, 450)]
    [InlineData(FreightZone.National, 1_200)]
    public void ResolveFreight_BelowThreshold_ChargesZoneFlatFee(FreightZone zone, double expectedFee)
    {
        FreightPolicy.ResolveFreight(zone, 1_000m).Should().Be((decimal)expectedFee);
    }
}
