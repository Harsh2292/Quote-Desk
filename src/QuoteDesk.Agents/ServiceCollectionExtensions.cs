using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using QuoteDesk.Agents.Checkpointing;
using QuoteDesk.Agents.Llm;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Agents.Prompts;
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

    /// <summary>Adds the pipeline itself — Extract/Resolve/Price/Approve, and everything they need.
    /// Takes a bound <see cref="LlmOptions"/> the same way <c>AddQuoteDeskData</c> takes a connection
    /// string, rather than this project depending on <c>Microsoft.Extensions.Configuration</c> itself.</summary>
    public static IServiceCollection AddQuoteDeskAgentPipeline(this IServiceCollection services, LlmOptions llmOptions)
    {
        ArgumentNullException.ThrowIfNull(llmOptions);

        services.AddQuoteDeskAgents();
        services.AddSingleton(llmOptions);
        services.AddSingleton(_ => ChatClientFactory.Create(llmOptions));
        services.AddSingleton<PromptLibrary>();
        services.AddScoped<SqlCheckpointStore>();
        services.AddScoped<EnquiryPipeline>();

        return services;
    }
}
