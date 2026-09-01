using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.UnitTests.Agents.Fakes;

/// <summary>A <see cref="TimeProvider"/> stuck at one instant, so tests reading dates or computing
/// expiries stay deterministic per CLAUDE.md's rule against a real clock in tests.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>In-memory repository fakes for the tool tests — no database, per CLAUDE.md's rule that
/// unit tests cover every tool's validation and miss paths without external dependencies.</summary>
internal sealed class FakeCustomerRepository : ICustomerRepository
{
    public List<CustomerRecord> Customers { get; } = [];

    public Task<CustomerRecord?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.SingleOrDefault(c => c.Id == id));

    public Task<CustomerRecord?> FindByEmailDomainAsync(string domain, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.SingleOrDefault(c => c.EmailDomain == domain));

    public Task<CustomerRecord?> FindByWhatsAppNumberAsync(string number, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.SingleOrDefault(c => c.WhatsAppNumber == number));

    public Task<CustomerRecord?> FindByNameAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.SingleOrDefault(c => c.Name == name));
}

internal sealed class FakeCatalogRepository : ICatalogRepository
{
    public List<CatalogItemRecord> Items { get; } = [];

    public Task<IReadOnlyList<CatalogItemRecord>> SearchAsync(string query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CatalogItemRecord>>(
            [.. Items.Where(i => i.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) || i.Name.Contains(query, StringComparison.OrdinalIgnoreCase))]);

    public Task<CatalogItemRecord?> GetBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(i => i.Sku == sku));

    public Task<IReadOnlyList<CatalogItemRecord>> GetByCategoryAsync(string category, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CatalogItemRecord>>([.. Items.Where(i => i.Category == category)]);
}

internal sealed class FakeStockRepository : IStockRepository
{
    public List<StockRecord> Stock { get; } = [];

    public Task<StockRecord?> GetBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(Stock.SingleOrDefault(s => s.Sku == sku));
}

internal sealed class FakeOrderHistoryRepository : IOrderHistoryRepository
{
    public List<OrderHistoryRecord> Orders { get; } = [];

    public Task<IReadOnlyList<OrderHistoryRecord>> GetByCustomerAsync(int customerId, string? sku, CancellationToken cancellationToken)
    {
        var query = Orders.Where(o => o.CustomerId == customerId);
        if (sku is not null)
        {
            query = query.Where(o => o.Sku == sku);
        }

        return Task.FromResult<IReadOnlyList<OrderHistoryRecord>>([.. query.OrderByDescending(o => o.OrderedAt)]);
    }
}

internal sealed class FakePriceRuleRepository : IPriceRuleRepository
{
    public List<PriceRuleRecord> Rules { get; } = [];

    public Task<IReadOnlyList<PriceRuleRecord>> GetByCategoryAsync(string category, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PriceRuleRecord>>([.. Rules.Where(r => r.Scope == "Category" && r.Target == category)]);

    public Task<IReadOnlyList<PriceRuleRecord>> GetBySkuAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PriceRuleRecord>>([.. Rules.Where(r => r.Scope == "Sku" && r.Target == sku)]);
}

internal sealed class FakeQuoteRepository : IQuoteRepository
{
    private readonly List<QuoteRecord> _quotes = [];

    public Task<QuoteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_quotes.SingleOrDefault(q => q.Id == id));

    public Task<IReadOnlyList<QuoteSummaryRecord>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuoteSummaryRecord>>(
            [.. _quotes.OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuoteSummaryRecord(q.Id, q.EnquiryId, q.Number, q.Status, null, null, q.Total, q.CreatedAt, q.ValidUntil,
                    [.. q.Lines.Select(l => l.Sku)]))]);

    public Task<QuoteRecord> CreateDraftAsync(NewQuote quote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);

        var id = _quotes.Count + 1;
        var lines = quote.Lines
            .Select((l, i) => new QuoteLineRecord(i + 1, l.Sku, l.Sku, l.Qty, l.UnitPrice, l.DiscountPct, l.LineTotal, l.RequiresOverride, l.DispatchDate, l.DeliveryDate, l.Note))
            .ToList();

        var record = new QuoteRecord(
            id, quote.EnquiryId, $"QTN-{quote.CreatedAt.Year}-{id:D4}", quote.Status, quote.Subtotal, quote.Freight,
            quote.Tax, quote.Total, quote.CreatedAt, quote.ValidUntil, quote.ShipTo, quote.RequiredBy, null, null, null, lines);

        _quotes.Add(record);
        return Task.FromResult(record);
    }

    public Task<QuoteRecord> MarkSentAsync(int quoteId, string status, DateTimeOffset sentAt, CancellationToken cancellationToken)
    {
        var index = _quotes.FindIndex(q => q.Id == quoteId);
        var updated = _quotes[index] with { Status = status, SentAt = sentAt };
        _quotes[index] = updated;
        return Task.FromResult(updated);
    }

    public Task<QuoteRecord> MarkApprovedAsync(int quoteId, int approvedByUserId, string status, DateTimeOffset approvedAt, CancellationToken cancellationToken)
    {
        var index = _quotes.FindIndex(q => q.Id == quoteId);
        var updated = _quotes[index] with { Status = status, ApprovedByUserId = approvedByUserId, ApprovedAt = approvedAt };
        _quotes[index] = updated;
        return Task.FromResult(updated);
    }
}
