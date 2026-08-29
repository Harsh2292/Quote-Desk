namespace QuoteDesk.Domain;

/// <summary>Computed dispatch and delivery dates for one line.</summary>
public sealed record DeliveryDates
{
    public required DateOnly Dispatch { get; init; }
    public required DateOnly Delivery { get; init; }
}

/// <summary>
/// On-hand vs supplier lead time → dispatch and delivery dates. Every date is computed from a
/// <see cref="DateTimeOffset"/> passed in, never from the clock, so results are deterministic.
/// </summary>
public static class DeliveryDateCalculator
{
    public static DeliveryDates Calculate(
        DateTimeOffset receivedAt,
        int onHand,
        int quantityRequested,
        int supplierLeadTimeDays,
        FreightZone zone,
        IReadOnlySet<DateOnly> holidays)
    {
        ArgumentNullException.ThrowIfNull(holidays);
        ArgumentOutOfRangeException.ThrowIfNegative(onHand);
        ArgumentOutOfRangeException.ThrowIfNegative(quantityRequested);
        ArgumentOutOfRangeException.ThrowIfNegative(supplierLeadTimeDays);

        var receivedDate = DateOnly.FromDateTime(receivedAt.Date);

        var dispatch = onHand >= quantityRequested
            ? RollForward(receivedDate.AddDays(1), holidays)
            : RollForward(receivedDate.AddDays(supplierLeadTimeDays), holidays);

        var delivery = RollForward(dispatch.AddDays(FreightPolicy.ResolveTransitDays(zone)), holidays);

        return new DeliveryDates { Dispatch = dispatch, Delivery = delivery };
    }

    /// <summary>Sundays and listed holidays are skipped — rolls forward to the next working day.</summary>
    private static DateOnly RollForward(DateOnly date, IReadOnlySet<DateOnly> holidays)
    {
        while (date.DayOfWeek == DayOfWeek.Sunday || holidays.Contains(date))
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
