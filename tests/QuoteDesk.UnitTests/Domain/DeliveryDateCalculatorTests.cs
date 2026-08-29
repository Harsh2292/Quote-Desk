using FluentAssertions;
using QuoteDesk.Domain;
using Xunit;

namespace QuoteDesk.UnitTests.Domain;

public class DeliveryDateCalculatorTests
{
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    [Fact]
    public void Calculate_InStock_DispatchesNextWorkingDay()
    {
        // 2026-03-26 is a Thursday; the next working day is Friday 2026-03-27.
        var receivedAt = new DateTimeOffset(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

        var dates = DeliveryDateCalculator.Calculate(
            receivedAt, onHand: 500, quantityRequested: 250, supplierLeadTimeDays: 9,
            FreightZone.Local, NoHolidays);

        dates.Dispatch.Should().Be(new DateOnly(2026, 3, 27));
    }

    [Fact]
    public void Calculate_ShortOnStock_DispatchesAfterSupplierLeadTime()
    {
        var receivedAt = new DateTimeOffset(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

        var dates = DeliveryDateCalculator.Calculate(
            receivedAt, onHand: 12, quantityRequested: 40, supplierLeadTimeDays: 9,
            FreightZone.Local, NoHolidays);

        // 2026-03-26 + 9 days = 2026-04-04 (Saturday) — not a Sunday, so it stands.
        dates.Dispatch.Should().Be(new DateOnly(2026, 4, 4));
    }

    [Fact]
    public void Calculate_DeliveryLandsOnSunday_RollsForwardToMonday()
    {
        var receivedAt = new DateTimeOffset(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));

        var dates = DeliveryDateCalculator.Calculate(
            receivedAt, onHand: 12, quantityRequested: 40, supplierLeadTimeDays: 9,
            FreightZone.Local, NoHolidays);

        // Dispatch Sat 2026-04-04 + 1 day transit = Sun 2026-04-05 -> rolled to Mon 2026-04-06.
        dates.Delivery.Should().Be(new DateOnly(2026, 4, 6));
    }

    [Fact]
    public void Calculate_DispatchLandsOnListedHoliday_RollsForwardPastIt()
    {
        var receivedAt = new DateTimeOffset(2026, 3, 26, 8, 41, 0, TimeSpan.FromHours(5.5));
        var holidays = new HashSet<DateOnly> { new(2026, 3, 27) };

        var dates = DeliveryDateCalculator.Calculate(
            receivedAt, onHand: 500, quantityRequested: 250, supplierLeadTimeDays: 9,
            FreightZone.Local, holidays);

        // Next working day would be Fri 2026-03-27, but it's a listed holiday, so roll to Sat 03-28.
        dates.Dispatch.Should().Be(new DateOnly(2026, 3, 28));
    }
}
