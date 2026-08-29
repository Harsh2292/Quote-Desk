using System.ComponentModel;
using Microsoft.Extensions.AI;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data.Repositories;
using QuoteDesk.Domain;

namespace QuoteDesk.Agents.Tools;

/// <summary>
/// <c>price_quote</c> — the only tool that touches money, per CLAUDE.md rule 1. Every number in the
/// result comes from <see cref="PricingEngine"/> and <see cref="QuoteTotalsCalculator"/> in
/// QuoteDesk.Domain; this class only fetches the inputs those need and shapes the output. Read-only,
/// per docs/SPEC.md §7 — it prices, it never writes.
/// </summary>
public sealed class PricingTools(
    ICustomerRepository customers,
    ICatalogRepository catalog,
    IStockRepository stock,
    IPriceRuleRepository priceRules,
    TimeProvider timeProvider)
{
    private const int ValidityDays = 15;

    [Description(
        "Prices a set of resolved SKUs and quantities for one customer — the only tool that computes a " +
        "price. Applies the quantity-slab and customer-tier discounts, checks the margin floor, and adds " +
        "GST, freight and delivery dates. Call this only after every line's SKU is resolved (search_catalog " +
        "outcome 'resolved'). Never state or adjust a price yourself — read this tool's numbers back " +
        "verbatim. A line flagged RequiresOverride still needs a human's approval before it can be quoted.")]
    public async Task<PricedQuote> PriceQuoteAsync(
        [Description("The customer's Id from resolve_customer, or null when the sender matched no customer — pricing still applies the quantity discount, per company policy on new-customer enquiries (docs/DOMAIN.md).")]
        int? customerId,
        [Description("One entry per resolved line: the exact SKU and the requested quantity.")]
        QuoteLineRequest[] lines,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var customer = customerId is int id ? await customers.GetByIdAsync(id, cancellationToken) : null;
        var zone = ShipToZoneResolver.Resolve(customer?.DefaultShipTo);

        // The pipeline reaches price_quote shortly after the enquiry is received, so "now" is the
        // right anchor for dispatch dates — the fixed tool signature in docs/SPEC.md §7 carries no
        // separate received-at parameter to pass instead.
        var now = timeProvider.GetUtcNow();

        var domainLines = new List<PricedLine>();
        var deliveryDates = new List<DeliveryDates?>();
        var warnings = new List<string>();

        foreach (var line in lines)
        {
            var item = await catalog.GetBySkuAsync(line.Sku, cancellationToken);
            if (item is null)
            {
                warnings.Add($"'{line.Sku}' is not a known SKU — line skipped.");
                continue;
            }

            var slabs = await LoadSlabsAsync(item.Category, item.Sku, cancellationToken);
            var priced = PricingEngine.PriceLine(
                new PricingLineRequest { Sku = item.Sku, Quantity = line.Quantity, ListPrice = item.ListPrice, CostPrice = item.CostPrice, Slabs = slabs },
                customer?.Tier);

            if (priced.RequiresOverride)
            {
                warnings.Add($"'{item.Sku}' needs a margin override to quote at this discount.");
            }

            var stockRecord = await stock.GetBySkuAsync(item.Sku, cancellationToken);
            var dates = stockRecord is null
                ? (DeliveryDates?)null
                : DeliveryDateCalculator.Calculate(now, stockRecord.OnHand, line.Quantity, stockRecord.LeadTimeDays, zone, QuoteDeskCalendar.Holidays);

            domainLines.Add(priced);
            deliveryDates.Add(dates);
        }

        var totals = QuoteTotalsCalculator.Calculate(domainLines, zone);

        if (customer is null)
        {
            warnings.Add("Sender did not match a known customer — list price and quantity discount only, no tier discount or credit terms. Flag for verification before sending.");
        }

        var pricedLines = domainLines.Zip(deliveryDates, (priced, dates) => new PricedQuoteLine
        {
            Sku = priced.Sku,
            Quantity = priced.Quantity,
            ListPrice = priced.ListPrice,
            DiscountPct = priced.DiscountPct,
            NetUnitPrice = priced.NetUnitPrice,
            LineTotal = priced.LineTotal,
            RequiresOverride = priced.RequiresOverride,
            DispatchDate = dates?.Dispatch,
            DeliveryDate = dates?.Delivery,
        }).ToList();

        return new PricedQuote
        {
            CustomerId = customerId,
            Lines = pricedLines,
            Subtotal = totals.Subtotal,
            Freight = totals.Freight,
            Tax = totals.Tax,
            GrandTotal = totals.GrandTotal,
            ValidUntil = now.AddDays(ValidityDays),
            Warnings = warnings,
        };
    }

    /// <summary>Per-SKU rules win over the category ladder; when neither exists, returning null lets
    /// <see cref="PricingEngine.PriceLine"/> fall back to <see cref="SlabDiscountPolicy.DefaultLadder"/>.</summary>
    private async Task<IReadOnlyList<QuantitySlab>?> LoadSlabsAsync(string category, string sku, CancellationToken cancellationToken)
    {
        var skuRules = await priceRules.GetBySkuAsync(sku, cancellationToken);
        if (skuRules.Count > 0)
        {
            return [.. skuRules.Select(r => new QuantitySlab(r.MinQty, r.DiscountPct))];
        }

        var categoryRules = await priceRules.GetByCategoryAsync(category, cancellationToken);
        return categoryRules.Count > 0 ? [.. categoryRules.Select(r => new QuantitySlab(r.MinQty, r.DiscountPct))] : null;
    }
}
