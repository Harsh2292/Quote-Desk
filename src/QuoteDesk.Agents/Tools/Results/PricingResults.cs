namespace QuoteDesk.Agents.Tools.Results;

/// <summary>One line as requested to <c>price_quote</c> — a resolved SKU and a quantity, nothing
/// else. Everything else about the line is computed, never supplied.</summary>
public sealed record QuoteLineRequest
{
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
}

/// <summary>
/// One priced line, as returned by <c>price_quote</c>. Deliberately narrower than
/// <see cref="QuoteDesk.Domain.PricedLine"/>: <c>MarginPct</c> and <c>MarginShortfallPct</c> are
/// dropped here rather than carried through, because a margin figure must never reach the model
/// (docs/DOMAIN.md, "What the model is never allowed to do") — only <see cref="RequiresOverride"/>
/// survives, so the model can say a line needs an override without ever seeing why.
/// </summary>
public sealed record PricedQuoteLine
{
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required decimal ListPrice { get; init; }
    public required decimal DiscountPct { get; init; }
    public required decimal NetUnitPrice { get; init; }
    public required decimal LineTotal { get; init; }
    public required bool RequiresOverride { get; init; }
    public DateOnly? DispatchDate { get; init; }
    public DateOnly? DeliveryDate { get; init; }
}

/// <summary>The complete result of <c>price_quote</c> — the only tool that touches money, and the
/// only shape <c>create_quote_draft</c> accepts once a human has approved it.</summary>
public sealed record PricedQuote
{
    /// <summary>Null when the sender matched no known customer (docs/DOMAIN.md, "Unknown sender") —
    /// pricing still ran, using the quantity discount only.</summary>
    public int? CustomerId { get; init; }

    public required IReadOnlyList<PricedQuoteLine> Lines { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Freight { get; init; }
    public required decimal Tax { get; init; }
    public required decimal GrandTotal { get; init; }
    public required DateTimeOffset ValidUntil { get; init; }

    /// <summary>Human-readable notes — an unknown SKU that was skipped, a line needing a margin
    /// override, an unmatched sender. Never a number that shouldn't reach the model.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
