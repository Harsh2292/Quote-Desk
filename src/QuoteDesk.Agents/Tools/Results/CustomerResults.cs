namespace QuoteDesk.Agents.Tools.Results;

/// <summary>
/// The outcome of <c>resolve_customer</c>. Never throws for a miss — <see cref="Found"/> is false
/// and every other field is null, which is the "unknown sender" case docs/DOMAIN.md describes: list
/// price only, no credit terms, flagged for a human to verify.
/// </summary>
public sealed record CustomerMatch
{
    public required bool Found { get; init; }
    public int? CustomerId { get; init; }
    public string? Name { get; init; }

    /// <summary>"A" / "B" / "C" — <see cref="QuoteDesk.Domain.CustomerTier"/> as a string, so the
    /// model never has to know a .NET enum's wire shape.</summary>
    public string? Tier { get; init; }

    public int? CreditDays { get; init; }
    public string? DefaultShipTo { get; init; }

    /// <summary>Which match rule fired (email domain, WhatsApp number, exact name) or, on a miss,
    /// why nothing matched. Written for the model to read back to a salesperson.</summary>
    public required string Reason { get; init; }
}

/// <summary>One prior order, newest first — what resolves phrases like "same as last time".</summary>
public sealed record PriorPurchase
{
    public required string Sku { get; init; }
    public required int Qty { get; init; }
    public required decimal UnitPrice { get; init; }
    public required DateTimeOffset OrderedAt { get; init; }
}
