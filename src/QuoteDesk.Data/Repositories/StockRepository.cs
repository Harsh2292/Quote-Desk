using Microsoft.EntityFrameworkCore;

namespace QuoteDesk.Data.Repositories;

public sealed class StockRepository(QuoteDeskDbContext db) : IStockRepository
{
    public async Task<StockRecord?> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        var stock = await db.StockLevels.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Sku == sku, cancellationToken);

        return stock is null ? null : new StockRecord(stock.Sku, stock.OnHand, stock.LeadTimeDays, stock.ReorderLevel);
    }
}
