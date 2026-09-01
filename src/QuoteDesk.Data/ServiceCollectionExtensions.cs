using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuoteDeskData(this IServiceCollection services, string connectionString)
    {
        // A factory, not a plain AddDbContext: WorkflowCheckpointRepository needs short-lived
        // contexts of its own (see its doc comment for why), and AddDbContextFactory's singleton
        // IDbContextFactory<T> cannot coexist with AddDbContext's scoped DbContextOptions<T> for the
        // same TContext — registering both throws at startup. AddScoped<QuoteDeskDbContext> below
        // recreates the "one context per request" behaviour everything else in this project expects,
        // just sourced from the same factory instead of a second, conflicting registration.
        //
        // EnableRetryOnFailure (task 09): Azure SQL's free-tier auto-pause (docs/SPEC.md §3) means the
        // first connection after an idle period routinely hits a transient "database is
        // resuming/unavailable" error — without this, that error surfaces to the caller as a bare 500
        // instead of EF Core quietly retrying until the database wakes up. Safe here specifically
        // because nothing in this project opens an explicit transaction (grepped — none do); EF Core's
        // retrying execution strategy refuses to coexist with a user-managed transaction, so this
        // would need re-checking if one is ever added.
        services.AddDbContextFactory<QuoteDeskDbContext>(options => options
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<QuoteDeskDbContext>>().CreateDbContext());

        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IOrderHistoryRepository, OrderHistoryRepository>();
        services.AddScoped<IPriceRuleRepository, PriceRuleRepository>();
        services.AddScoped<IEnquiryRepository, EnquiryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IAgentRunRepository, AgentRunRepository>();
        services.AddScoped<IWorkflowCheckpointRepository, WorkflowCheckpointRepository>();

        return services;
    }
}
