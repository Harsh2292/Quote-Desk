using Microsoft.Extensions.DependencyInjection;

namespace QuoteDesk.Intake;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddQuoteDeskIntake(this IServiceCollection services)
    {
        services.AddScoped<PasteAdapter>();

        return services;
    }
}
