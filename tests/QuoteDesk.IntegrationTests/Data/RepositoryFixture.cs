using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data;
using QuoteDesk.Data.Repositories;
using QuoteDesk.Data.Seed;

namespace QuoteDesk.IntegrationTests.Data;

/// <summary>Migrates and seeds a dedicated test database once, shared read-only across every test
/// in <see cref="RepositoryTests"/>.</summary>
public sealed class RepositoryFixture : IAsyncLifetime
{
    private const string DatabaseName = "QuoteDeskTests_Repository";

    public ICatalogRepository Catalog { get; private set; } = null!;
    public ICustomerRepository Customers { get; private set; } = null!;
    public IStockRepository Stock { get; private set; } = null!;
    public IOrderHistoryRepository OrderHistory { get; private set; } = null!;
    public IPriceRuleRepository PriceRules { get; private set; } = null!;
    public IEnquiryRepository Enquiries { get; private set; } = null!;
    public IQuoteRepository Quotes { get; private set; } = null!;
    public IAgentRunRepository AgentRuns { get; private set; } = null!;
    public IWorkflowCheckpointRepository Checkpoints { get; private set; } = null!;
    public IUserRepository Users { get; private set; } = null!;

    private QuoteDeskDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = TestConnection.CreateContext(DatabaseName);
        await _db.Database.EnsureDeletedAsync();
        await _db.Database.MigrateAsync();
        await DeterministicSeeder.SeedAsync(_db, CancellationToken.None);

        Catalog = new CatalogRepository(_db);
        Customers = new CustomerRepository(_db);
        Stock = new StockRepository(_db);
        OrderHistory = new OrderHistoryRepository(_db);
        PriceRules = new PriceRuleRepository(_db);
        Enquiries = new EnquiryRepository(_db);
        Quotes = new QuoteRepository(_db);
        AgentRuns = new AgentRunRepository(_db);
        Checkpoints = new WorkflowCheckpointRepository(new TestDbContextFactory(DatabaseName));
        Users = new UserRepository(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    /// <summary>Mirrors production's <c>AddDbContextFactory&lt;QuoteDeskDbContext&gt;</c> registration
    /// (see QuoteDesk.Data's ServiceCollectionExtensions) — <see cref="WorkflowCheckpointRepository"/>
    /// needs short-lived contexts of its own, not the fixture's single shared one.</summary>
    private sealed class TestDbContextFactory(string databaseName) : IDbContextFactory<QuoteDeskDbContext>
    {
        public QuoteDeskDbContext CreateDbContext() => TestConnection.CreateContext(databaseName);

        public Task<QuoteDeskDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
