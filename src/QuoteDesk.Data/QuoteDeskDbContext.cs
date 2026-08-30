using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data;

public class QuoteDeskDbContext(DbContextOptions<QuoteDeskDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<OrderHistory> OrderHistory => Set<OrderHistory>();
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteLine> QuoteLines => Set<QuoteLine>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<WorkflowCheckpoint> WorkflowCheckpoints => Set<WorkflowCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuoteDeskDbContext).Assembly);
    }
}
