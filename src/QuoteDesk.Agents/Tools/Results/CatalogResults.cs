namespace QuoteDesk.Agents.Tools.Results;

/// <summary>One catalogue candidate <c>search_catalog</c> considered, with the confidence and
/// reasoning behind its score — never picked silently, always explainable.</summary>
public sealed record CatalogCandidate
{
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Uom { get; init; }
    public required decimal ListPrice { get; init; }
    public string? Attributes { get; init; }

    /// <summary>0.0 to 1.0 — how well this candidate matches the search terms.</summary>
    public required double Confidence { get; init; }

    public required string Reason { get; init; }
}

/// <summary>One line item's worth of search input — <c>search_catalog</c> takes an array of these
/// instead of one query at a time, so the model resolves every line in a single tool call rather than
/// one call per line (found live: the per-line version cost 3 real Gemini calls for a 3-line enquiry,
/// eating into the free-tier daily quota faster than necessary for no benefit — see
/// docs/SESSION-LOG.md).</summary>
public sealed record CatalogSearchQuery
{
    public required string Query { get; init; }
    public string[] Hints { get; init; } = [];
}

/// <summary>
/// One query's result within a <c>search_catalog</c> call. SPEC originally described this tool as
/// returning a bare <c>CatalogMatch[]</c>, but an array has no way to express "I cannot tell which of
/// these you mean" — <see cref="Outcome"/> carries that explicitly, corrected in docs/SPEC.md §7 in
/// the same commit as this type.
/// </summary>
public sealed record CatalogSearchResult
{
    /// <summary>Echoes the <see cref="CatalogSearchQuery.Query"/> this result answers, so a batched
    /// call's results can be matched back to the line item that produced each one.</summary>
    public required string Query { get; init; }

    /// <summary>"resolved" | "ambiguous" | "not_found".</summary>
    public required string Outcome { get; init; }

    /// <summary>Set only when <see cref="Outcome"/> is "resolved".</summary>
    public string? ResolvedSku { get; init; }

    public required IReadOnlyList<CatalogCandidate> Candidates { get; init; }

    public required string Reason { get; init; }
}
