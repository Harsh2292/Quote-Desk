namespace QuoteDesk.Agents.Tools;

/// <summary>The holiday list <see cref="QuoteDesk.Domain.DeliveryDateCalculator"/> rolls forward
/// over. Empty for the demo — Sundays alone reproduce the worked example's dates exactly
/// (docs/DOMAIN.md) — and QuoteDesk.Domain itself reads no calendar, so a real list can be added
/// here later with no change below this project.</summary>
public static class QuoteDeskCalendar
{
    public static readonly IReadOnlySet<DateOnly> Holidays = new HashSet<DateOnly>();
}
