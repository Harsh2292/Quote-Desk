namespace QuoteDesk.Data.Repositories;

public interface IStockRepository
{
    Task<StockRecord?> GetBySkuAsync(string sku, CancellationToken cancellationToken);
}
