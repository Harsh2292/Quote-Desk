using FluentAssertions;
using QuoteDesk.Domain;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

/// <summary>
/// The Shreeji Textiles enquiry from docs/DOMAIN.md — the primary eval case. "If a change breaks
/// it, the change is wrong." Every figure here is asserted, not adjusted to make the test pass.
/// </summary>
public class WorkedExampleTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    [Fact]
    public void ShreejiTextilesEnquiry_Bearings_Gets8PercentAnd14PercentMargin()
    {
        var request = new PricingLineRequest
        {
            Sku = "BRG-6203-2RS",
            Quantity = 250,
            ListPrice = 250.00m,
            CostPrice = 197.80m,
        };

        var line = PricingEngine.PriceLine(request, CustomerTier.B);

        // 250 units crosses the 200+ slab (6%); tier B adds 2%. Exactly what Kiran asked for —
        // policy already permits it, so the system confirms rather than negotiates.
        line.DiscountPct.Should().Be(0.08m);
        line.NetUnitPrice.Should().Be(230.00m);
        line.LineTotal.Should().Be(57_500.00m);
        line.MarginPct.Should().Be(0.14m);
        line.RequiresOverride.Should().BeFalse("14% clears the 10% floor");
    }

    [Fact]
    public void ShreejiTextilesEnquiry_Belt_ShortOnStockMissesRequestedDate()
    {
        var dates = DeliveryDateCalculator.Calculate(
            ReceivedAt,
            onHand: 12,
            quantityRequested: 40,
            supplierLeadTimeDays: 9,
            FreightZone.Local,
            NoHolidays);

        // "earliest dispatch the 4th" / "delivery the 6th" — the customer asked for the 5th.
        dates.Dispatch.Should().Be(new DateOnly(2026, 4, 4));
        dates.Delivery.Should().Be(new DateOnly(2026, 4, 6));

        var requestedBy = new DateOnly(2026, 4, 5);
        dates.Delivery.Should().BeAfter(requestedBy, "the belt delivery must be flagged as missing the customer's requested date");
    }

    [Fact]
    public void ShreejiTextilesEnquiry_Bearings_InStockDispatchesTheNextWorkingDay()
    {
        var dates = DeliveryDateCalculator.Calculate(
            ReceivedAt,
            onHand: 500,
            quantityRequested: 250,
            supplierLeadTimeDays: 9,
            FreightZone.Local,
            NoHolidays);

        dates.Dispatch.Should().Be(new DateOnly(2026, 3, 27));
    }
}
