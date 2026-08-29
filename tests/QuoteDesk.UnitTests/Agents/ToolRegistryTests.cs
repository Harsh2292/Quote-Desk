using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.UnitTests.Agents.Fakes;
using QuoteDesk.UnitTests.Intake;

namespace QuoteDesk.UnitTests.Agents;

/// <summary>
/// "ReadToolRegistry contains zero write tools" (tasks/task-05-tools.md) — the entire enforcement
/// of CLAUDE.md rule 3, "nothing leaves without a human". Checked by constructing the real registry
/// (not mocking it away) and inspecting the function names it actually exposes.
/// </summary>
public class ToolRegistryTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

    [Fact]
    public void ReadToolRegistry_ContainsExactlyTheFiveReadTools()
    {
        var registry = BuildReadRegistry();

        registry.Tools.Select(t => t.Name).Should().BeEquivalentTo(
        [
            "resolve_customer", "get_customer_history", "search_catalog", "check_stock", "price_quote",
        ]);
    }

    [Fact]
    public void ReadToolRegistry_ContainsNoWriteTools()
    {
        var registry = BuildReadRegistry();

        registry.Tools.Select(t => t.Name).Should().NotContain(["create_quote_draft", "send_quote"]);
    }

    [Fact]
    public void WriteToolRegistry_ContainsExactlyTheTwoWriteTools()
    {
        var registry = new WriteToolRegistry(new QuoteWriteTools(new FakeQuoteRepository(), new FakeEnquiryRepository(), new FixedTimeProvider(Now)));

        registry.Tools.Select(t => t.Name).Should().BeEquivalentTo(["create_quote_draft", "send_quote"]);
    }

    private static ReadToolRegistry BuildReadRegistry()
    {
        var customerTools = new CustomerTools(new FakeCustomerRepository(), new FakeOrderHistoryRepository());
        var catalogTools = new CatalogTools(new FakeCatalogRepository());
        var stockTools = new StockTools(new FakeStockRepository(), new FixedTimeProvider(Now));
        var pricingTools = new PricingTools(new FakeCustomerRepository(), new FakeCatalogRepository(), new FakeStockRepository(), new FakePriceRuleRepository(), new FixedTimeProvider(Now));

        return new ReadToolRegistry(customerTools, catalogTools, stockTools, pricingTools);
    }
}
