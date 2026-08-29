using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Domain;

namespace QuoteDesk.UnitTests.Agents;

public class ShipToZoneResolverTests
{
    [Theory]
    [InlineData("Sachin")]
    [InlineData("Surat")]
    [InlineData("Palsana")]
    [InlineData("Kadodara")]
    [InlineData("Pandesara")]
    [InlineData("sachin")]
    public void Resolve_SeededSuratAreaCity_ReturnsLocal(string shipTo)
    {
        ShipToZoneResolver.Resolve(shipTo).Should().Be(FreightZone.Local);
    }

    [Fact]
    public void Resolve_UnknownCity_ReturnsRegional()
    {
        ShipToZoneResolver.Resolve("Mumbai").Should().Be(FreightZone.Regional);
    }

    [Fact]
    public void Resolve_NullShipTo_ReturnsRegional()
    {
        ShipToZoneResolver.Resolve(null).Should().Be(FreightZone.Regional);
    }
}
