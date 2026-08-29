namespace QuoteDesk.Data.Repositories;

public interface IOrderHistoryRepository
{
    /// <summary>All prior purchases for a customer, optionally narrowed to one SKU — this is what
    /// resolves "same as last time" (docs/DOMAIN.md).</summary>
    Task<IReadOnlyList<OrderHistoryRecord>> GetByCustomerAsync(int customerId, string? sku, CancellationToken cancellationToken);
}
