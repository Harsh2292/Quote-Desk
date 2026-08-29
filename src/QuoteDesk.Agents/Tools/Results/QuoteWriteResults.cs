namespace QuoteDesk.Agents.Tools.Results;

/// <summary>The result of <c>create_quote_draft</c>. A typed miss (an unknown enquiry, an empty
/// line list) rather than an exception, matching every other tool's validation style — but this
/// tool is never reachable from the Resolve agent regardless (it lives only in
/// <see cref="WriteToolRegistry"/>), so a miss here is a workflow bug, not model confusion to
/// reason about.</summary>
public sealed record QuoteDraftResult
{
    public required bool Created { get; init; }
    public int? QuoteId { get; init; }
    public string? Number { get; init; }
    public required string Reason { get; init; }
}

/// <summary>The result of <c>send_quote</c>. QuoteDesk never actually emails or messages a customer
/// (docs/SPEC.md §9) — this stamps the quote sent and nothing more.</summary>
public sealed record SendResult
{
    public required bool Sent { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public required string Reason { get; init; }
}
