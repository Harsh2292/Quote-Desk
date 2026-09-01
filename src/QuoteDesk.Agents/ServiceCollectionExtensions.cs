using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        // One registry, one pipeline: every model call in the app — Extract, Resolve's tool loop,
        // Narrate — goes through a client this registry built, so the logging middleware wraps all of
        // them regardless of which model answered. When a run fails, the request and response are in
        // the log, not just a database row to reverse-engineer. See ChatClientRegistry's remarks for
        // why the pipeline is no longer built on a single shared IChatClient.
        services.AddSingleton(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new ChatClientRegistry(llmOptions, model => ChatClientFactory.Create(llmOptions, model), loggerFactory);
        });

        services.AddSingleton<PromptLibrary>();
        services.AddScoped<SqlCheckpointStore>();
        services.AddScoped<EnquiryPipeline>();

        return services;
    }
}
