using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Data;
using QuoteDesk.UnitTests.Agents.Fakes;

namespace QuoteDesk.UnitTests.Agents;

public class StockToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

    [Fact]
    public async Task CheckStockAsync_EnoughOnHand_DispatchesNextWorkingDay()
    {
        var stock = new FakeStockRepository();
        stock.Stock.Add(new StockRecord("BRG-6203-2RS", 500, 5, 100));
        var tools = new StockTools(stock, new FixedTimeProvider(Now));

        var result = await tools.CheckStockAsync("BRG-6203-2RS", 250, CancellationToken.None);

        result.Found.Should().BeTrue();
        result.OnHand.Should().Be(500);
        result.DispatchDate.Should().NotBeNull();
        result.Reason.Should().Contain("covers");
    }

    [Fact]
    public async Task CheckStockAsync_ShortOnHand_DispatchWaitsForLeadTime()
    {
        var stock = new FakeStockRepository();
        stock.Stock.Add(new StockRecord("BELT-PU-25MM", 12, 9, 5));
        var tools = new StockTools(stock, new FixedTimeProvider(Now));

        var result = await tools.CheckStockAsync("BELT-PU-25MM", 40, CancellationToken.None);

        result.Found.Should().BeTrue();
        result.Reason.Should().Contain("lead time");
    }

    [Fact]
    public async Task CheckStockAsync_UnknownSku_ReturnsNotFound()
    {
        var tools = new StockTools(new FakeStockRepository(), new FixedTimeProvider(Now));

        var result = await tools.CheckStockAsync("NO-SUCH-SKU", 10, CancellationToken.None);

        result.Found.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CheckStockAsync_QuantityAtOrBelowZero_ReturnsNotFound(int qty)
    {
        var tools = new StockTools(new FakeStockRepository(), new FixedTimeProvider(Now));

        var result = await tools.CheckStockAsync("ANY-SKU", qty, CancellationToken.None);

        result.Found.Should().BeFalse();
        result.Reason.Should().Be("Quantity must be greater than zero.");
    }
}
