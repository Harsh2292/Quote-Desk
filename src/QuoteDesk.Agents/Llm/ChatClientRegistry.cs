using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace QuoteDesk.Agents.Llm;

/// <summary>
/// Builds and caches one logging-wrapped <see cref="IChatClient"/> per distinct model name across
/// the pipeline's three stages (<see cref="LlmOptions.ExtractModel"/>, <see cref="LlmOptions.ResolveModel"/>,
/// <see cref="LlmOptions.NarrateModel"/> — each falling back to <see cref="LlmOptions.Model"/>).
///
/// This is the fix for what killed the first live run of the reworked pipeline
/// (docs/SESSION-LOG.md, 2026-08-31): <c>gemini-3.6-flash</c>'s free tier allows only 5 requests per
/// minute, and one pipeline run makes ~6 sequential model calls in well under a minute. Before this,
/// every stage shared one <see cref="IChatClient"/> and therefore one quota bucket. Routing Extract
/// and Narrate onto a different, high-quota model gives them their own bucket — the bunching that
/// produced the run-ending 429 can no longer happen against the scarce Resolve model. Two stages
/// configured onto the *same* model still share one cached client, so they correctly share one
/// bucket rather than being double-counted.
///
/// <paramref name="clientFactory"/> is a seam, not hardcoded to
/// <see cref="ChatClientFactory.Create(LlmOptions, string)"/>, so tests can supply one shared stub
/// instance for every model name instead of building a real provider client — see
/// <c>tests/QuoteDesk.IntegrationTests/Api/QuoteDeskApiFactory.cs</c>, which used to swap the single
/// <c>IChatClient</c> registration and now swaps this registry's factory instead.
/// </summary>
public sealed class ChatClientRegistry
{
    private readonly LlmOptions _options;
    private readonly Func<string, IChatClient> _clientFactory;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly Dictionary<string, IChatClient> _clients = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public ChatClientRegistry(LlmOptions options, Func<string, IChatClient> clientFactory, ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientFactory);

        _options = options;
        _clientFactory = clientFactory;
        _loggerFactory = loggerFactory;
    }

    public IChatClient Extract => GetOrCreate(_options.ExtractModel ?? _options.Model);

    public IChatClient Resolve => GetOrCreate(_options.ResolveModel ?? _options.Model);

    public IChatClient Narrate => GetOrCreate(_options.NarrateModel ?? _options.Model);

    private IChatClient GetOrCreate(string model)
    {
        lock (_gate)
        {
            if (_clients.TryGetValue(model, out var existing))
            {
                return existing;
            }

            var chatClient = _clientFactory(model);
            var wrapped = _loggerFactory is null
                ? chatClient
                : new ChatClientBuilder(chatClient).UseLogging(_loggerFactory).Build();

            _clients[model] = wrapped;
            return wrapped;
        }
    }
}
