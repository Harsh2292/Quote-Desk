using Microsoft.EntityFrameworkCore;

namespace QuoteDesk.Data.Repositories;

public sealed class CatalogRepository(QuoteDeskDbContext db) : ICatalogRepository
{
    public async Task<IReadOnlyList<CatalogItemRecord>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var items = await db.CatalogItems.AsNoTracking()
            .Where(c => EF.Functions.Like(c.Sku, $"%{query}%") || EF.Functions.Like(c.Name, $"%{query}%"))
            .OrderBy(c => c.Sku)
            .ToListAsync(cancellationToken);

        return [.. items.Select(ToRecord)];
    }

    public async Task<CatalogItemRecord?> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        var item = await db.CatalogItems.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Sku == sku, cancellationToken);

        return item is null ? null : ToRecord(item);
    }

    public async Task<IReadOnlyList<CatalogItemRecord>> GetByCategoryAsync(string category, CancellationToken cancellationToken)
    {
        var items = await db.CatalogItems.AsNoTracking()
            .Where(c => c.Category == category)
            .OrderBy(c => c.Sku)
            .ToListAsync(cancellationToken);

        return [.. items.Select(ToRecord)];
    }

    private static CatalogItemRecord ToRecord(Entities.CatalogItem c) =>
        new(c.Id, c.Sku, c.Name, c.Category, c.Uom, c.ListPrice, c.CostPrice, c.Attributes);
}
