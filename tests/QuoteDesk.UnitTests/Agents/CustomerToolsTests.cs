using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Data;
using QuoteDesk.Domain;
using QuoteDesk.UnitTests.Agents.Fakes;

namespace QuoteDesk.UnitTests.Agents;

public class CustomerToolsTests
{
    private static readonly CustomerRecord Shreeji = new(1, "Shreeji Textiles", "shreejitextiles.com", "+91-98250-11223", CustomerTier.B, 45, "24AAAAA0001A1Z5", "Sachin");

    [Fact]
    public async Task ResolveCustomerAsync_NullSenderId_ThrowsRatherThanNullReferenceException()
    {
        var tools = new CustomerTools(new FakeCustomerRepository(), new FakeOrderHistoryRepository());

        var act = async () => await tools.ResolveCustomerAsync("Any Company", null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ResolveCustomerAsync_KnownEmailDomain_MatchesByDomain()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(Shreeji);
        var tools = new CustomerTools(customers, new FakeOrderHistoryRepository());

        var result = await tools.ResolveCustomerAsync("Shreeji Textiles", "kiran@shreejitextiles.com", CancellationToken.None);

        result.Found.Should().BeTrue();
        result.CustomerId.Should().Be(1);
        result.Tier.Should().Be("B");
        result.CreditDays.Should().Be(45);
        result.DefaultShipTo.Should().Be("Sachin");
        result.Reason.Should().Contain("email domain");
    }

    [Fact]
    public async Task ResolveCustomerAsync_KnownWhatsAppNumber_MatchesByNumber()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(Shreeji);
        var tools = new CustomerTools(customers, new FakeOrderHistoryRepository());

        var result = await tools.ResolveCustomerAsync("Shreeji Textiles", "+91-98250-11223", CancellationToken.None);

        result.Found.Should().BeTrue();
        result.Reason.Should().Contain("WhatsApp");
    }

    [Fact]
    public async Task ResolveCustomerAsync_KnownCompanyNameOnly_MatchesByName()
    {
        var customers = new FakeCustomerRepository();
        customers.Customers.Add(Shreeji);
        var tools = new CustomerTools(customers, new FakeOrderHistoryRepository());

        var result = await tools.ResolveCustomerAsync("Shreeji Textiles", "unknown-sender@example.com", CancellationToken.None);

        result.Found.Should().BeTrue();
        result.Reason.Should().Contain("company name");
    }

    [Fact]
    public async Task ResolveCustomerAsync_UnknownSender_ReturnsNotFoundWithReason()
    {
        var tools = new CustomerTools(new FakeCustomerRepository(), new FakeOrderHistoryRepository());

        var result = await tools.ResolveCustomerAsync("Nobody Textiles", "+91-90000-00000", CancellationToken.None);

        result.Found.Should().BeFalse();
        result.CustomerId.Should().BeNull();
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetCustomerHistoryAsync_SeededBearingPurchases_ResolvesSameAsLastTime()
    {
        var orderHistory = new FakeOrderHistoryRepository();
        orderHistory.Orders.AddRange(
        [
            new OrderHistoryRecord(1, 1, "BRG-6203-2RS", 200, 230.00m, new DateTimeOffset(2025, 11, 10, 10, 0, 0, TimeSpan.FromHours(5.5))),
            new OrderHistoryRecord(2, 1, "BRG-6203-2RS", 180, 230.00m, new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.FromHours(5.5))),
            new OrderHistoryRecord(3, 1, "BRG-6203-2RS", 220, 230.00m, new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.FromHours(5.5))),
        ]);
        var tools = new CustomerTools(new FakeCustomerRepository(), orderHistory);

        var history = await tools.GetCustomerHistoryAsync(1, "BRG-6203-2RS", CancellationToken.None);

        history.Should().HaveCount(3);
        history[0].OrderedAt.Should().Be(new DateTimeOffset(2026, 2, 20, 10, 0, 0, TimeSpan.FromHours(5.5)), "results are newest first");
    }

    [Fact]
    public async Task GetCustomerHistoryAsync_NoPriorPurchases_ReturnsEmptyList()
    {
        var tools = new CustomerTools(new FakeCustomerRepository(), new FakeOrderHistoryRepository());

        var history = await tools.GetCustomerHistoryAsync(999, null, CancellationToken.None);

        history.Should().BeEmpty();
    }
}
