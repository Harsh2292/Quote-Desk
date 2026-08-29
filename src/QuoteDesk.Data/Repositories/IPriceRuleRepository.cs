namespace QuoteDesk.Data.Repositories;

public interface IPriceRuleRepository
{
    /// <summary>Category-scoped rules first, falling back to none — <c>price_quote</c> uses this to
    /// build the slab ladder it hands to <see cref="QuoteDesk.Domain.PricingEngine"/>.</summary>
    Task<IReadOnlyList<PriceRuleRecord>> GetByCategoryAsync(string category, CancellationToken cancellationToken);

    Task<IReadOnlyList<PriceRuleRecord>> GetBySkuAsync(string sku, CancellationToken cancellationToken);
}
