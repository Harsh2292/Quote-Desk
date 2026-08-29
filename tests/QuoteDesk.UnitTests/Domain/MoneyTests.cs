using FluentAssertions;
using QuoteDesk.Domain;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Round_MidpointValue_RoundsAwayFromZero()
    {
        Money.Round(1.005m).Should().Be(1.01m);
    }
}
