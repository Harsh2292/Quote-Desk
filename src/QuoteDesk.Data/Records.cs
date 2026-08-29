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
