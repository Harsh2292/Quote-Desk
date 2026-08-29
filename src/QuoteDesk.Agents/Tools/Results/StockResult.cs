namespace QuoteDesk.Agents.Tools.Results;

/// <summary>The result of <c>check_stock</c>. <see cref="DispatchDate"/> only — the delivery date to
/// the customer's door needs their freight zone, which <c>price_quote</c> adds.</summary>
public sealed record StockResult
{
    public required bool Found { get; init; }
    public int OnHand { get; init; }
    public int LeadTimeDays { get; init; }
    public DateOnly? DispatchDate { get; init; }
    public required string Reason { get; init; }
}
