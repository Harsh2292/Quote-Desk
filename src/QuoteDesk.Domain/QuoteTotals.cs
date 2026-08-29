namespace QuoteDesk.Domain;

/// <summary>Line totals, freight, GST and the grand total for one quote.</summary>
public sealed record QuoteTotals
{
    public required decimal Subtotal { get; init; }
    public required decimal Freight { get; init; }
    public required decimal Tax { get; init; }
    public required decimal GrandTotal { get; init; }
}

/// <summary>GST at 18% on the taxable value, applied after all discounts and after freight.</summary>
public static class QuoteTotalsCalculator
{
    public const decimal GstRatePct = 0.18m;

    public static QuoteTotals Calculate(IReadOnlyList<PricedLine> lines, FreightZone zone)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var subtotal = Money.Round(lines.Sum(l => l.LineTotal));
        var freight = FreightPolicy.ResolveFreight(zone, subtotal);
        var taxableValue = subtotal + freight;
        var tax = Money.Round(taxableValue * GstRatePct);
        var grandTotal = Money.Round(taxableValue + tax);

        return new QuoteTotals
        {
            Subtotal = subtotal,
            Freight = freight,
            Tax = tax,
            GrandTotal = grandTotal,
        };
    }
}
