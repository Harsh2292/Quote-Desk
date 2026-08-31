using QuoteDesk.Domain;

namespace QuoteDesk.Data;

// Plain records only — entities never leave QuoteDesk.Data, so this is what every caller sees.

public sealed record CustomerRecord(
    int Id,
    string Name,
    string? EmailDomain,
    string? WhatsAppNumber,
    CustomerTier Tier,
    int CreditDays,
    string? GstIn,
    string? DefaultShipTo);

/// <summary>
/// The Data-layer projection of a catalogue row — includes <see cref="CostPrice"/> because
/// <c>price_quote</c> needs it to check margin server-side. The boundary that must never leak
/// <see cref="CostPrice"/> to the model sits one layer up, at the tool-result shapes in
/// QuoteDesk.Agents (docs/SPEC.md §6), not here.
/// </summary>
public sealed record CatalogItemRecord(
    int Id,
    string Sku,
    string Name,
    string Category,
    string Uom,
    decimal ListPrice,
    decimal CostPrice,
    string? Attributes);

public sealed record StockRecord(string Sku, int OnHand, int LeadTimeDays, int ReorderLevel);

public sealed record PriceRuleRecord(int Id, string Scope, string Target, int MinQty, decimal DiscountPct);

public sealed record OrderHistoryRecord(int Id, int CustomerId, string Sku, int Qty, decimal UnitPrice, DateTimeOffset OrderedAt);

/// <summary>
/// A signed-in salesperson. <see cref="GoogleSubject"/> is present because the Api layer matches on
/// it; it is deliberately not part of any API response shape.
/// </summary>
public sealed record UserRecord(
    int Id,
    string GoogleSubject,
    string Email,
    string Name,
    string? PictureUrl,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastLoginAt);

/// <summary>
/// Everything needed to create or refresh a user from one Google sign-in. <see cref="SignedInAt"/>
/// is passed in rather than read from the clock, so the write is deterministic under test.
/// </summary>
public sealed record GoogleUserUpsert(
    string GoogleSubject,
    string Email,
    string Name,
    string? PictureUrl,
    string Role,
    DateTimeOffset SignedInAt);

public sealed record EnquiryRecord(
    int Id,
    string Channel,
    string SenderId,
    string RawBody,
    DateTimeOffset ReceivedAt,
    int? CustomerId,
    string Status);

/// <summary>Everything needed to persist one freshly ingested enquiry. <c>Channel</c> and
/// <c>Status</c> are plain strings here — the enum and status constants they came from belong to
/// QuoteDesk.Intake, which converts before calling <see cref="Repositories.IEnquiryRepository.CreateAsync"/>.</summary>
public sealed record NewEnquiry(
    string Channel,
    string SenderId,
    string RawBody,
    DateTimeOffset ReceivedAt,
    int? CustomerId,
    string Status);

/// <summary>One line of a quote being created. <c>UnitPrice</c> is the already-discounted net price
/// — the same rounding rule as <see cref="QuoteDesk.Domain.PricedLine.NetUnitPrice"/> — never a list
/// price with the discount applied separately downstream.</summary>
public sealed record NewQuoteLine(
    string Sku,
    int Qty,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal LineTotal,
    bool RequiresOverride,
    DateOnly? DispatchDate,
    DateOnly? DeliveryDate,
    string? Note);

/// <summary>Everything needed to persist one freshly created quote draft. <c>Status</c> is a plain
/// string — the write tool that calls <see cref="Repositories.IQuoteRepository.CreateDraftAsync"/>
/// owns the vocabulary of valid values, the same pattern QuoteDesk.Intake uses for
/// <see cref="NewEnquiry.Status"/>.</summary>
public sealed record NewQuote(
    int EnquiryId,
    string Status,
    decimal Subtotal,
    decimal Freight,
    decimal Tax,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset ValidUntil,
    string? ShipTo,
    DateOnly? RequiredBy,
    IReadOnlyList<NewQuoteLine> Lines);

public sealed record QuoteLineRecord(
    int Id,
    string Sku,
    int Qty,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal LineTotal,
    bool RequiresOverride,
    DateOnly? DispatchDate,
    DateOnly? DeliveryDate,
    string? Note);

public sealed record QuoteRecord(
    int Id,
    int EnquiryId,
    string Number,
    string Status,
    decimal Subtotal,
    decimal Freight,
    decimal Tax,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset ValidUntil,
    string? ShipTo,
    DateOnly? RequiredBy,
    int? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? SentAt,
    IReadOnlyList<QuoteLineRecord> Lines);

/// <summary>One pipeline run of one enquiry through Extract → Resolve → Price → Approve.</summary>
public sealed record AgentRunRecord(
    int Id,
    int EnquiryId,
    string SessionId,
    string Status,
    string? ApprovalRequestJson,
    string? TraceJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Everything needed to start tracking a new pipeline run.</summary>
public sealed record NewAgentRun(int EnquiryId, string SessionId, string Status, DateTimeOffset CreatedAt);

/// <summary>The row shape <c>GET /api/quotes</c> lists — a summary, not the full line detail
/// <see cref="QuoteRecord"/> carries. <c>CustomerName</c> is joined in from the quote's enquiry
/// because <see cref="Entities.Quote"/> itself carries no customer id directly (docs/SPEC.md §6:
/// the customer lives on <c>Enquiries</c>, not <c>Quotes</c>).</summary>
public sealed record QuoteSummaryRecord(
    int Id,
    int EnquiryId,
    string Number,
    string Status,
    int? CustomerId,
    string? CustomerName,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset ValidUntil);

/// <summary>One committed workflow checkpoint's identity, without its payload — enough to let
/// QuoteDesk.Agents' bridge onto <c>ICheckpointStore&lt;JsonElement&gt;</c> pick the latest checkpoint
/// without paying to load every payload in a session's history.</summary>
public sealed record CheckpointRecord(string CheckpointId, string? ParentCheckpointId, DateTimeOffset CreatedAt);
