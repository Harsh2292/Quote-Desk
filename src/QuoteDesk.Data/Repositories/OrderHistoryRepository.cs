using Microsoft.EntityFrameworkCore;

namespace QuoteDesk.Data.Repositories;

public sealed class OrderHistoryRepository(QuoteDeskDbContext db) : IOrderHistoryRepository
{
    public async Task<IReadOnlyList<OrderHistoryRecord>> GetByCustomerAsync(
        int customerId, string? sku, CancellationToken cancellationToken)
    {
        var query = db.OrderHistory.AsNoTracking().Where(o => o.CustomerId == customerId);

        if (sku is not null)
        {
            query = query.Where(o => o.Sku == sku);
        }

        var orders = await query.OrderByDescending(o => o.OrderedAt).ToListAsync(cancellationToken);

        return [.. orders.Select(o => new OrderHistoryRecord(o.Id, o.CustomerId, o.Sku, o.Qty, o.UnitPrice, o.OrderedAt))];
    }
}
