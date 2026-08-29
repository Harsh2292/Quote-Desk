using System.ComponentModel;
using Microsoft.Extensions.AI;
using QuoteDesk.Agents.Tools.Results;
using QuoteDesk.Data.Repositories;
using QuoteDesk.Domain;

namespace QuoteDesk.Agents.Tools;

/// <summary><c>check_stock</c> — read-only, per docs/SPEC.md §7.</summary>
public sealed class StockTools(IStockRepository stock, TimeProvider timeProvider)
{
    [Description(
        "Checks on-hand quantity and supplier lead time for one SKU, and computes the earliest dispatch " +
        "date for the requested quantity — the next working day if there is enough on hand, otherwise " +
        "today plus the lead time. Call this once per resolved SKU so a shortage can be flagged before " +
        "pricing. This does not compute delivery to the customer's door — price_quote adds that once the " +
        "customer's freight zone is known.")]
    public async Task<StockResult> CheckStockAsync(
        [Description("The exact SKU, as returned by search_catalog.")]
        string sku,
        [Description("The quantity the customer is asking for. Must be greater than zero.")]
        int qty,
        CancellationToken cancellationToken)
    {
        if (qty <= 0)
        {
            return new StockResult { Found = false, Reason = "Quantity must be greater than zero." };
        }

        var record = await stock.GetBySkuAsync(sku, cancellationToken);
        if (record is null)
        {
            return new StockResult { Found = false, Reason = $"No stock record for SKU '{sku}'." };
        }

        // FreightZone.Local is a placeholder here — this tool only needs the Dispatch half of the
        // result, which does not depend on the zone; only Delivery would, and that is discarded.
        var dispatch = DeliveryDateCalculator.Calculate(
            timeProvider.GetUtcNow(), record.OnHand, qty, record.LeadTimeDays, FreightZone.Local, QuoteDeskCalendar.Holidays).Dispatch;

        return new StockResult
        {
            Found = true,
            OnHand = record.OnHand,
            LeadTimeDays = record.LeadTimeDays,
            DispatchDate = dispatch,
            Reason = record.OnHand >= qty
                ? $"{record.OnHand} on hand covers the requested {qty} — dispatches the next working day."
                : $"Only {record.OnHand} on hand against {qty} requested — dispatch waits for the {record.LeadTimeDays}-day supplier lead time.",
        };
    }
}
