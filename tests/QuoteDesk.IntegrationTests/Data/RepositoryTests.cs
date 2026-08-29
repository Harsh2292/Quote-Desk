using FluentAssertions;
using QuoteDesk.Domain;

namespace QuoteDesk.IntegrationTests.Data;

/// <summary>
/// Runs against the real containerised SQL Server, not an in-memory provider — an in-memory
/// provider would not catch a decimal truncation, which is exactly the bug worth catching here.
/// Also proves every deliberate seed case from tasks/task-02-data-efcore.md is individually
/// queryable.
/// </summary>
public class RepositoryTests(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
{
    [Fact]
    public async Task Catalog_NearIdenticalBearingSkus_AreBothIndividuallyQueryable()
    {
        var twoRs = await fixture.Catalog.GetBySkuAsync("BRG-6203-2RS", CancellationToken.None);
        var zz = await fixture.Catalog.GetBySkuAsync("BRG-6203-ZZ", CancellationToken.None);

        twoRs.Should().NotBeNull();
        zz.Should().NotBeNull();
        twoRs!.Sku.Should().NotBe(zz!.Sku);
    }

    [Fact]
    public async Task Catalog_BearingPricing_MatchesTheWorkedExampleExactly()
    {
        var item = await fixture.Catalog.GetBySkuAsync("BRG-6203-2RS", CancellationToken.None);

        item.Should().NotBeNull();
        item!.ListPrice.Should().Be(250.00m);
        item.CostPrice.Should().Be(197.80m);
    }

    [Fact]
    public async Task Catalog_SpindleTapeVariants_DifferOnlyByAttribute()
    {
        var sixMm = await fixture.Catalog.GetBySkuAsync("SPT-RF-6MM", CancellationToken.None);
        var eightMm = await fixture.Catalog.GetBySkuAsync("SPT-RF-8MM", CancellationToken.None);

        sixMm.Should().NotBeNull();
        eightMm.Should().NotBeNull();
        sixMm!.Name.Should().Be(eightMm!.Name);
        sixMm.Attributes.Should().Be("6mm");
        eightMm.Attributes.Should().Be("8mm");
    }

    [Fact]
    public async Task Stock_ShortSupplyBelt_HasFewerUnitsThanATypicalAsk()
    {
        var stock = await fixture.Stock.GetBySkuAsync("BELT-PU-25MM", CancellationToken.None);

        stock.Should().NotBeNull();
        stock!.OnHand.Should().Be(12);
        stock.LeadTimeDays.Should().Be(9);
    }

    [Fact]
    public async Task Customers_ShreejiTextiles_ResolvesByEmailDomain()
    {
        var customer = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);

        customer.Should().NotBeNull();
        customer!.Tier.Should().Be(CustomerTier.B);
        customer.DefaultShipTo.Should().Be("Sachin");
    }

    [Fact]
    public async Task OrderHistory_ShreejiTextiles_HasThreePriorBearingPurchases()
    {
        var shreeji = await fixture.Customers.FindByEmailDomainAsync("shreejitextiles.com", CancellationToken.None);

        var history = await fixture.OrderHistory.GetByCustomerAsync(shreeji!.Id, "BRG-6203-2RS", CancellationToken.None);

        history.Should().HaveCount(3);
        history.Should().OnlyContain(o => o.UnitPrice == 230.00m);
    }

    [Fact]
    public async Task Catalog_MarginFloorCase_HasATenPercentListToCostSpread()
    {
        var item = await fixture.Catalog.GetBySkuAsync("GEAR-M2-40T", CancellationToken.None);

        item.Should().NotBeNull();
        item!.ListPrice.Should().Be(100.00m);
        item.CostPrice.Should().Be(90.00m);
    }

    [Fact]
    public async Task Enquiries_UnknownSender_HasNoCustomerMatch()
    {
        var enquiry = await fixture.Enquiries.GetByIdAsync(2, CancellationToken.None);

        enquiry.Should().NotBeNull();
        enquiry!.CustomerId.Should().BeNull();
        enquiry.Status.Should().Be("new_customer");
    }

    [Fact]
    public async Task PriceRules_BearingsCategory_MatchesTheDomainDefaultLadder()
    {
        var rules = await fixture.PriceRules.GetByCategoryAsync("Bearings", CancellationToken.None);

        rules.Should().HaveCount(SlabDiscountPolicy.DefaultLadder.Count);
        rules.Select(r => (r.MinQty, r.DiscountPct))
            .Should().BeEquivalentTo(SlabDiscountPolicy.DefaultLadder.Select(s => (s.MinQty, s.DiscountPct)));
    }

    [Fact]
    public async Task Catalog_SearchByPartialSku_FindsBothBearingVariants()
    {
        var results = await fixture.Catalog.SearchAsync("6203", CancellationToken.None);

        results.Should().Contain(r => r.Sku == "BRG-6203-2RS");
        results.Should().Contain(r => r.Sku == "BRG-6203-ZZ");
    }

    [Fact]
    public async Task Catalog_UnknownSku_ReturnsNullRatherThanThrowing()
    {
        var result = await fixture.Catalog.GetBySkuAsync("DOES-NOT-EXIST", CancellationToken.None);

        result.Should().BeNull();
    }
}
