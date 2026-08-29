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

/// <summary>
/// The result of <c>search_catalog</c>. SPEC originally described this tool as returning a bare
/// <c>CatalogMatch[]</c>, but an array has no way to express "I cannot tell which of these you
/// mean" — <see cref="Outcome"/> carries that explicitly, corrected in docs/SPEC.md §7 in the same
/// commit as this type.
/// </summary>
public sealed record CatalogSearchResult
{
    /// <summary>"resolved" | "ambiguous" | "not_found".</summary>
    public required string Outcome { get; init; }

    /// <summary>Set only when <see cref="Outcome"/> is "resolved".</summary>
    public string? ResolvedSku { get; init; }

    public required IReadOnlyList<CatalogCandidate> Candidates { get; init; }

    public required string Reason { get; init; }
}
