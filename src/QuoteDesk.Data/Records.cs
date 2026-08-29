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

public sealed record EnquiryRecord(
    int Id,
    string Channel,
    string SenderId,
    string RawBody,
    DateTimeOffset ReceivedAt,
    int? CustomerId,
    string Status);
