using QuoteDesk.Agents.Tools.Results;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>What starts a pipeline run — the plain fields off <c>Enquiry</c>, not the entity itself
/// (QuoteDesk.Data entities never leave that project).</summary>
public sealed record EnquiryInput(int EnquiryId, string SenderId, string RawBody, DateTimeOffset ReceivedAt);

/// <summary>One line as the customer wrote it, before anything has been resolved.</summary>
public sealed record ExtractedLine
{
    public required string Description { get; init; }
    public required int Quantity { get; init; }
    public string? Uom { get; init; }
}

/// <summary>The Extract stage's output — structure, not resolution. No SKU, no price, no customer id
/// appears here; that is what Resolve and Price add.</summary>
public sealed record ExtractedEnquiry
{
    public required IReadOnlyList<ExtractedLine> Lines { get; init; }

    /// <summary>As signed off in the message, e.g. "Shreeji Textiles". Empty string if none written.</summary>
    public required string CompanyName { get; init; }

    public string? ShipTo { get; init; }
    public DateOnly? RequiredBy { get; init; }

    /// <summary>A pricing expectation the customer stated verbatim, e.g. "last time you gave 8% on
    /// bearings, please keep same". Null if none.</summary>
    public string? CommercialAsk { get; init; }
}

/// <summary>Extract's actual output edge type — bundles the original <see cref="EnquiryInput"/> back
/// in alongside <see cref="ExtractedEnquiry"/>, since Resolve needs the sender id and raw body (for
/// <c>resolve_customer</c> and the untrusted-content wrapper) and Extract's own structured result
/// does not carry them forward.</summary>
public sealed record ExtractionResult
{
    public required EnquiryInput Enquiry { get; init; }
    public required ExtractedEnquiry Extracted { get; init; }
}

public sealed record ResolvedLine
{
    public required string OriginalDescription { get; init; }
    public required string Sku { get; init; }
    public required int Quantity { get; init; }
    public required string Reason { get; init; }
}

public sealed record UnresolvedLine
{
    public required string OriginalDescription { get; init; }
    public required int Quantity { get; init; }
    public required string Reason { get; init; }
}

/// <summary>The Resolve stage's output. Every <see cref="ResolvedLine.Sku"/> here has already been
/// re-validated against the catalogue in code — see <c>ResolveExecutor</c> — so nothing downstream
/// needs to trust the model's claim a second time.</summary>
public sealed record ResolutionResult
{
    public required EnquiryInput Enquiry { get; init; }
    public required ExtractedEnquiry Extracted { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public required IReadOnlyList<ResolvedLine> Resolved { get; init; }
    public required IReadOnlyList<UnresolvedLine> Unresolved { get; init; }
}

/// <summary>What the human approval card shows — the Price stage's output. <see cref="PricedQuote"/>
/// is the same shape <c>price_quote</c> already returns; nothing about the money is reshaped again
/// here.</summary>
public sealed record ApprovalRequest
{
    public required int EnquiryId { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public required PricedQuote PricedQuote { get; init; }
    public required IReadOnlyList<UnresolvedLine> Unresolved { get; init; }
    public required string Narration { get; init; }
    public string? ShipTo { get; init; }
    public DateOnly? RequiredBy { get; init; }
}

/// <summary>
/// The human's decision at the Approve gate — the <c>RequestPort</c>'s response type, so it is what
/// task 07's approval endpoint constructs and sends back into the suspended workflow. Deliberately
/// self-sufficient (carries <see cref="EnquiryId"/> and the final <see cref="Quote"/> to create,
/// already resolved from "approve as priced" vs. "approve this edited version" by the caller) so
/// <c>ApproveExecutor</c> needs no further lookup to act on it.
/// </summary>
public sealed record ApprovalDecision
{
    public required int EnquiryId { get; init; }
    public required bool Approved { get; init; }
    public required int ApprovedByUserId { get; init; }

    /// <summary>The quote to create — the original <c>PricedQuote</c> from Price, or a human-edited
    /// version of it. Required when <see cref="Approved"/> is true; null when rejected.</summary>
    public PricedQuote? Quote { get; init; }

    public string? RejectionReason { get; init; }
}

/// <summary>The pipeline's final output, once a decision has been acted on.</summary>
public sealed record PipelineResult
{
    public required bool Success { get; init; }
    public int? QuoteId { get; init; }
    public string? QuoteNumber { get; init; }
    public required string Reason { get; init; }
}
