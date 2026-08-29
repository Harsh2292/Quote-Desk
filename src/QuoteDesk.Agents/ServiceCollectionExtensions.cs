using Microsoft.Extensions.DependencyInjection;
using QuoteDesk.Agents.Tools;

namespace QuoteDesk.Agents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuoteDeskAgents(this IServiceCollection services)
    {
        services.AddScoped<CustomerTools>();
        services.AddScoped<CatalogTools>();
        services.AddScoped<StockTools>();
        services.AddScoped<PricingTools>();
        services.AddScoped<QuoteWriteTools>();
        services.AddScoped<ReadToolRegistry>();
        services.AddScoped<WriteToolRegistry>();

        return services;
    }
}
