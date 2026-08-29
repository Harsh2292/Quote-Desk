using Microsoft.EntityFrameworkCore;

namespace QuoteDesk.Data.Repositories;

public sealed class PriceRuleRepository(QuoteDeskDbContext db) : IPriceRuleRepository
{
    public async Task<IReadOnlyList<PriceRuleRecord>> GetByCategoryAsync(string category, CancellationToken cancellationToken)
    {
        var rules = await db.PriceRules.AsNoTracking()
            .Where(p => p.Scope == "Category" && p.Target == category)
            .OrderBy(p => p.MinQty)
            .ToListAsync(cancellationToken);

        return [.. rules.Select(ToRecord)];
    }

    public async Task<IReadOnlyList<PriceRuleRecord>> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        var rules = await db.PriceRules.AsNoTracking()
            .Where(p => p.Scope == "Sku" && p.Target == sku)
            .OrderBy(p => p.MinQty)
            .ToListAsync(cancellationToken);

        return [.. rules.Select(ToRecord)];
    }

    private static PriceRuleRecord ToRecord(Entities.PriceRule p) => new(p.Id, p.Scope, p.Target, p.MinQty, p.DiscountPct);
}
