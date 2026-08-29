using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data;
using QuoteDesk.Domain;
using QuoteDesk.UnitTests.Agents.Fakes;

namespace QuoteDesk.UnitTests.Agents;

public class PricingToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

    [Fact]
    public async Task PriceQuoteAsync_WorkedExampleBearingLine_ReproducesEightPercentDiscount()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(new CustomerRecord(1, "Shreeji Textiles", "shreejitextiles.com", null, CustomerTier.B, 45, null, "Sachin"));
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(new CatalogItemRecord(1, "BRG-6203-2RS", "6203 Series Ball Bearing (2RS)", "Bearings", "Nos", 250.00m, 197.80m, null));
        var stock = new FakeStockRepository();
        stock.Stock.Add(new StockRecord("BRG-6203-2RS", 500, 5, 100));

        var tools = new PricingTools(customers, catalog, stock, new FakePriceRuleRepository(), new FixedTimeProvider(Now));

        var result = await tools.PriceQuoteAsync(1, [new QuoteLineRequest { Sku = "BRG-6203-2RS", Quantity = 250 }], CancellationToken.None);

        var line = result.Lines.Single();
        line.DiscountPct.Should().Be(0.08m, "200+ slab (6%) plus tier B (2%) per docs/DOMAIN.md");
        line.NetUnitPrice.Should().Be(230.00m);
        line.RequiresOverride.Should().BeFalse("14% margin clears the 10% floor");
    }

    [Fact]
    public async Task PriceQuoteAsync_UnmatchedCustomer_AppliesSlabButNotTierDiscount()
    {
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(new CatalogItemRecord(1, "BRG-6200-2RS", "6200 Series Ball Bearing (2RS)", "Bearings", "Nos", 100.00m, 79.12m, null));
        var stock = new FakeStockRepository();
        stock.Stock.Add(new StockRecord("BRG-6200-2RS", 500, 5, 100));

        var tools = new PricingTools(new FakeCustomerRepository(), catalog, stock, new FakePriceRuleRepository(), new FixedTimeProvider(Now));

        var result = await tools.PriceQuoteAsync(null, [new QuoteLineRequest { Sku = "BRG-6200-2RS", Quantity = 250 }], CancellationToken.None);

        result.Lines.Single().DiscountPct.Should().Be(0.06m, "quantity slab still applies without a customer match, but no tier discount");
        result.Warnings.Should().Contain(w => w.Contains("did not match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PriceQuoteAsync_MarginBelowFloor_FlagsRequiresOverride()
    {
        var catalog = new FakeCatalogRepository();
        catalog.Items.Add(new CatalogItemRecord(1, "GEAR-M2-40T", "Module 2 Spur Gear (40T)", "Gears", "Nos", 100.00m, 90.00m, null));
        var stock = new FakeStockRepository();
        stock.Stock.Add(new StockRecord("GEAR-M2-40T", 200, 6, 40));

        var tools = new PricingTools(new FakeCustomerRepository(), catalog, stock, new FakePriceRuleRepository(), new FixedTimeProvider(Now));

        var result = await tools.PriceQuoteAsync(null, [new QuoteLineRequest { Sku = "GEAR-M2-40T", Quantity = 60 }], CancellationToken.None);

        result.Lines.Single().RequiresOverride.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("margin override", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PriceQuoteAsync_UnknownSku_SkipsLineWithWarningRatherThanThrowing()
    {
        var tools = new PricingTools(new FakeCustomerRepository(), new FakeCatalogRepository(), new FakeStockRepository(), new FakePriceRuleRepository(), new FixedTimeProvider(Now));

        var result = await tools.PriceQuoteAsync(null, [new QuoteLineRequest { Sku = "NO-SUCH-SKU", Quantity = 10 }], CancellationToken.None);

        result.Lines.Should().BeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("NO-SUCH-SKU", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PriceQuoteAsync_EmptyLines_ReturnsZeroSubtotalAndNoWarnings()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(new CustomerRecord(1, "Shreeji Textiles", "shreejitextiles.com", null, CustomerTier.B, 45, null, "Sachin"));
        var tools = new PricingTools(customers, new FakeCatalogRepository(), new FakeStockRepository(), new FakePriceRuleRepository(), new FixedTimeProvider(Now));

        var result = await tools.PriceQuoteAsync(1, [], CancellationToken.None);

        result.Lines.Should().BeEmpty();
        result.Subtotal.Should().Be(0m);
        result.Warnings.Should().BeEmpty("a matched customer with no lines is not itself a problem worth flagging");
    }
}
