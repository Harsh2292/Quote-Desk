using FluentAssertions;
using QuoteDesk.Agents.Tools;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.IntegrationTests.Data;

namespace QuoteDesk.IntegrationTests.Agents;

/// <summary>
/// Runs the tools against the real, deterministically seeded database — proving the deliberate
/// cases from docs/DOMAIN.md's worked example resolve correctly through actual EF Core queries,
/// not just against in-memory fakes.
/// </summary>
[Collection("Repository")]
public class ToolsIntegrationTests(RepositoryFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

    [Fact]
    public async Task GetCustomerHistoryAsync_ShreejiTextiles_ReturnsTheThreeSeededBearingPurchases()
    {
        var shreeji = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);
        var tools = new CustomerTools(fixture.Customers, fixture.OrderHistory);

        var history = await tools.GetCustomerHistoryAsync(shreeji!.Id, "BRG-6203-2RS", CancellationToken.None);

        history.Should().HaveCount(3);
        history.Should().OnlyContain(o => o.UnitPrice == 230.00m);
    }

    [Fact]
    public async Task PriceQuoteAsync_WorkedExampleThreeLines_ReproducesTheEightPercentBearingDiscount()
    {
        var shreeji = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);
        var tools = new PricingTools(fixture.Customers, fixture.Catalog, fixture.Stock, fixture.PriceRules, new FixedTimeProvider(Now));

        var result = await tools.PriceQuoteAsync(
            shreeji!.Id,
            [
                new QuoteLineRequest { Sku = "BRG-6203-2RS", Quantity = 250 },
                new QuoteLineRequest { Sku = "BELT-PU-25MM", Quantity = 40 },
            ],
            CancellationToken.None);

        var bearingLine = result.Lines.Single(l => l.Sku == "BRG-6203-2RS");
        bearingLine.DiscountPct.Should().Be(0.08m);
        bearingLine.NetUnitPrice.Should().Be(230.00m);
        bearingLine.RequiresOverride.Should().BeFalse();

        var beltLine = result.Lines.Single(l => l.Sku == "BELT-PU-25MM");
        beltLine.DispatchDate.Should().NotBeNull("stock is short, so DeliveryDateCalculator still computes a lead-time-based dispatch");
    }

    [Fact]
    public async Task SearchCatalogAsync_WorkedExampleThreeLines_ResolvesTwoAndFlagsTheThickerOne_AgainstTheRealCatalogue()
    {
        var tools = new CatalogTools(fixture.Catalog);

        var results = await tools.SearchCatalogAsync(
            [
                new CatalogSearchQuery { Query = "6203 bearing", Hints = ["same as last time"] },
                new CatalogSearchQuery { Query = "25mm PU timing belt", Hints = ["25mm", "PU"] },
                new CatalogSearchQuery { Query = "ring frame spindle tape", Hints = ["thicker"] },
            ],
            CancellationToken.None);

        // The 6203 line names the series but not the suffix — genuinely ambiguous until history decides.
        results[0].Outcome.Should().Be("ambiguous");
        results[0].Candidates.Should().OnlyContain(c => c.Sku.StartsWith("BRG-6203-"));

        // Both belt axes given — resolves outright, and not to the rubber belt.
        results[1].Outcome.Should().Be("resolved");
        results[1].ResolvedSku.Should().Be("BELT-PU-25MM");

        // "the thicker one" is a feel word — must stay ambiguous across the thicknesses, no bearings.
        results[2].Outcome.Should().Be("ambiguous");
        results[2].Candidates.Should().OnlyContain(c => c.Category == "SpindleTapes");

        // The whole point of the rewrite: no result floods the model.
        results.Should().OnlyContain(r => r.Candidates.Count <= 5);
    }

    [Theory]
    [InlineData("6205 2RS bearing", "BRG-6205-2RS")]
    [InlineData("module 2 spur gear 40T", "GEAR-M2-40T")]
    [InlineData("doubling frame spindle tape 8mm", "SPT-DF-8MM")]
    [InlineData("6210 ZZ bearing 60 pcs ka rate bhejo", "BRG-6210-ZZ")]
    [InlineData("20mm PU belt", "BELT-PU-20MM")]
    [InlineData("cogged v belt 40mm", "BELT-CVB-40MM")]
    public async Task SearchCatalogAsync_FullySpecifiedSeededPhrasings_ResolveToTheExpectedSku(string query, string expectedSku)
    {
        var tools = new CatalogTools(fixture.Catalog);

        var results = await tools.SearchCatalogAsync([new CatalogSearchQuery { Query = query }], CancellationToken.None);

        results[0].Outcome.Should().Be("resolved");
        results[0].ResolvedSku.Should().Be(expectedSku);
    }

    [Fact]
    public async Task CreateThenSendQuoteDraft_RoundTripsToSentWithAQuoteNumber()
    {
        var quoteWriteTools = new QuoteWriteTools(fixture.Quotes, fixture.Enquiries, new FixedTimeProvider(Now));
        var pricedQuote = new PricedQuote
        {
            CustomerId = 1,
            Lines = [new PricedQuoteLine { Sku = "BRG-6203-2RS", Quantity = 250, ListPrice = 250.00m, DiscountPct = 0.08m, NetUnitPrice = 230.00m, LineTotal = 57_500.00m, RequiresOverride = false }],
            Subtotal = 57_500.00m,
            Freight = 0m,
            Tax = 10_350.00m,
            GrandTotal = 67_850.00m,
            ValidUntil = Now.AddDays(15),
            Warnings = [],
        };

        var draft = await quoteWriteTools.CreateQuoteDraftAsync(1, pricedQuote, CancellationToken.None);
        draft.Created.Should().BeTrue();
        draft.Number.Should().StartWith("QTN-2026-");

        var sent = await quoteWriteTools.SendQuoteAsync(draft.QuoteId!.Value, CancellationToken.None);

        sent.Sent.Should().BeTrue();
        var stored = await fixture.Quotes.GetByIdAsync(draft.QuoteId.Value, CancellationToken.None);
        stored!.Status.Should().Be(QuoteStatus.Sent);
        stored.Number.Should().Be(draft.Number);
    }
}
