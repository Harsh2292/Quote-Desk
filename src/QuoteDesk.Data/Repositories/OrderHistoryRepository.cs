using Microsoft.EntityFrameworkCore;

namespace QuoteDesk.Data.Repositories;

public sealed class OrderHistoryRepository(QuoteDeskDbContext db) : IOrderHistoryRepository
{
    /// <summary>The most-recent slice only. A customer has dozens of prior orders; the Resolve agent
    /// asks this to break a tie or confirm "same as last time", and the recent history answers that.
    /// Returning every row bloated the tool result and, worse, is re-sent on every turn of the tool
    /// loop — a real driver of runaway token cost (docs/SESSION-LOG.md).</summary>
    private const int MaxRows = 20;

    public async Task<IReadOnlyList<OrderHistoryRecord>> GetByCustomerAsync(
        int customerId, string? sku, CancellationToken cancellationToken)
    {
        var query = db.OrderHistory.AsNoTracking().Where(o => o.CustomerId == customerId);

        if (sku is not null)
        {
            query = query.Where(o => o.Sku == sku);
        }

        var orders = await query
            .OrderByDescending(o => o.OrderedAt)
            .Take(MaxRows)
            .ToListAsync(cancellationToken);

        return [.. orders.Select(o => new OrderHistoryRecord(o.Id, o.CustomerId, o.Sku, o.Qty, o.UnitPrice, o.OrderedAt))];
    }
}
