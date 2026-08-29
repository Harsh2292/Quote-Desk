using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data;
using QuoteDesk.UnitTests.Agents.Fakes;
using QuoteDesk.UnitTests.Intake;

namespace QuoteDesk.UnitTests.Agents;

public class QuoteWriteToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

    private static PricedQuote OneLinePricedQuote() => new()
    {
        CustomerId = 1,
        Lines =
        [
            new PricedQuoteLine
            {
                Sku = "BRG-6203-2RS", Quantity = 250, ListPrice = 250.00m, DiscountPct = 0.08m,
                NetUnitPrice = 230.00m, LineTotal = 57_500.00m, RequiresOverride = false,
            },
        ],
        Subtotal = 57_500.00m,
        Freight = 0m,
        Tax = 10_350.00m,
        GrandTotal = 67_850.00m,
        ValidUntil = Now.AddDays(15),
        Warnings = [],
    };

    [Fact]
    public async Task CreateQuoteDraftAsync_ExistingEnquiry_PersistsAndAssignsNumber()
    {
        var enquiries = new FakeEnquiryRepository();
        var enquiryId = await enquiries.CreateAsync(new NewEnquiry("Email", "kiran@shreejitextiles.com", "body", Now, 1, "pending"), CancellationToken.None);
        var quotes = new FakeQuoteRepository();
        var tools = new QuoteWriteTools(quotes, enquiries, new FixedTimeProvider(Now));

        var result = await tools.CreateQuoteDraftAsync(enquiryId, OneLinePricedQuote(), CancellationToken.None);

        result.Created.Should().BeTrue();
        result.QuoteId.Should().NotBeNull();
        result.Number.Should().StartWith("QTN-2026-");
    }

    [Fact]
    public async Task CreateQuoteDraftAsync_UnknownEnquiry_ReturnsTypedRefusalRatherThanThrowing()
    {
        var tools = new QuoteWriteTools(new FakeQuoteRepository(), new FakeEnquiryRepository(), new FixedTimeProvider(Now));

        var result = await tools.CreateQuoteDraftAsync(999, OneLinePricedQuote(), CancellationToken.None);

        result.Created.Should().BeFalse();
        result.QuoteId.Should().BeNull();
        result.Reason.Should().Contain("999");
    }

    [Fact]
    public async Task CreateQuoteDraftAsync_NoLines_ReturnsTypedRefusal()
    {
        var enquiries = new FakeEnquiryRepository();
        var enquiryId = await enquiries.CreateAsync(new NewEnquiry("Email", "sender@example.com", "body", Now, null, "pending"), CancellationToken.None);
        var tools = new QuoteWriteTools(new FakeQuoteRepository(), enquiries, new FixedTimeProvider(Now));

        var emptyQuote = OneLinePricedQuote() with { Lines = [] };
        var result = await tools.CreateQuoteDraftAsync(enquiryId, emptyQuote, CancellationToken.None);

        result.Created.Should().BeFalse();
        result.Reason.Should().Contain("at least one line");
    }

    [Fact]
    public async Task SendQuoteAsync_DraftQuote_MarksSentAndStampsTime()
    {
        var quotes = new FakeQuoteRepository();
        var created = await quotes.CreateDraftAsync(
            new NewQuote(1, QuoteStatus.Draft, 100m, 0m, 18m, 118m, Now, Now.AddDays(15), null, null, [new NewQuoteLine("SKU", 1, 100m, 0m, 100m, false, null, null, null)]),
            CancellationToken.None);
        var tools = new QuoteWriteTools(quotes, new FakeEnquiryRepository(), new FixedTimeProvider(Now));

        var result = await tools.SendQuoteAsync(created.Id, CancellationToken.None);

        result.Sent.Should().BeTrue();
        result.SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task SendQuoteAsync_UnknownQuote_ReturnsTypedRefusal()
    {
        var tools = new QuoteWriteTools(new FakeQuoteRepository(), new FakeEnquiryRepository(), new FixedTimeProvider(Now));

        var result = await tools.SendQuoteAsync(999, CancellationToken.None);

        result.Sent.Should().BeFalse();
        result.Reason.Should().Contain("999");
    }

    [Fact]
    public async Task SendQuoteAsync_AlreadySentQuote_RefusesToSendAgain()
    {
        var quotes = new FakeQuoteRepository();
        var created = await quotes.CreateDraftAsync(
            new NewQuote(1, QuoteStatus.Draft, 100m, 0m, 18m, 118m, Now, Now.AddDays(15), null, null, [new NewQuoteLine("SKU", 1, 100m, 0m, 100m, false, null, null, null)]),
            CancellationToken.None);
        var tools = new QuoteWriteTools(quotes, new FakeEnquiryRepository(), new FixedTimeProvider(Now));
        await tools.SendQuoteAsync(created.Id, CancellationToken.None);

        var result = await tools.SendQuoteAsync(created.Id, CancellationToken.None);

        result.Sent.Should().BeFalse();
        result.Reason.Should().Contain("cannot be sent");
    }
}
