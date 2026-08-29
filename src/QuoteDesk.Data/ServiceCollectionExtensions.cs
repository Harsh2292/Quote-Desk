using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuoteDeskData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<QuoteDeskDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IOrderHistoryRepository, OrderHistoryRepository>();
        services.AddScoped<IPriceRuleRepository, PriceRuleRepository>();
        services.AddScoped<IEnquiryRepository, EnquiryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();

        return services;
    }
}
