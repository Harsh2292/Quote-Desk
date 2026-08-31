using Microsoft.Extensions.AI;
using QuoteDesk.IntegrationTests.Agents;

namespace QuoteDesk.IntegrationTests.Api;

/// <summary>
/// The <see cref="IChatClient"/> <see cref="QuoteDeskApiFactory"/> registers in place of a real
/// provider — CLAUDE.md: "Integration tests use a stubbed IChatClient." <see cref="StubChatClient"/>
/// takes its scripted turns at construction, which does not fit one instance shared for the whole
/// test host's lifetime across many tests; this wraps a replaceable <see cref="StubChatClient"/>
/// instead, so a test calls <see cref="Script"/> immediately before issuing its request — resetting
/// the turn cursor and received-message log the same way constructing a fresh
/// <see cref="StubChatClient"/> would. Safe only because every test hitting
/// <see cref="QuoteDeskApiFactory"/> shares the "QuoteDeskApi" collection (see
/// <see cref="QuoteDeskApiCollection"/>), which already runs them sequentially for the database's
/// sake — this relies on that same guarantee.
/// </summary>
public sealed class ScriptableChatClient : IChatClient
{
    private StubChatClient _current = new([]);
    private Exception? _nextThrow;

    public void Script(IReadOnlyList<ChatResponse> turns)
    {
        _current = new StubChatClient(turns);
        _nextThrow = null;
    }

    /// <summary>Makes the very next <see cref="GetResponseAsync"/> call throw <paramref name="exception"/>
    /// instead of returning a scripted turn — how <c>AgentStreamEndpointTests</c> proves a provider
    /// 429 becomes a clean <c>provider_rate_limited</c> event, since <see cref="StubChatClient"/> only
    /// ever returns responses, never throws.</summary>
    public void ScriptThrow(Exception exception) => _nextThrow = exception;

    public IReadOnlyList<IReadOnlyList<ChatMessage>> ReceivedMessages => _current.ReceivedMessages;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_nextThrow is { } exception)
        {
            _nextThrow = null;
            throw exception;
        }

        return _current.GetResponseAsync(messages, options, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        _current.GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => _current.GetService(serviceType, serviceKey);

    public void Dispose()
    {
    }
}
