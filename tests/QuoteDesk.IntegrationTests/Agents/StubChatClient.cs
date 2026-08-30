using Microsoft.Extensions.AI;

namespace QuoteDesk.IntegrationTests.Agents;

/// <summary>
/// A scripted <see cref="IChatClient"/> — CLAUDE.md: "Integration tests use a stubbed IChatClient. CI
/// must pass with no network and no API key." <see cref="FunctionInvokingChatClient"/> (wrapped around
/// this by <c>ResolveExecutor</c>/<see cref="Llm.ChatClientFactory"/>-style construction) calls
/// <see cref="GetResponseAsync"/> exactly once per round-trip of its tool-calling loop, so a test
/// scripts the model's turns as a plain ordered list — a function-call turn, then (once
/// <c>FunctionInvokingChatClient</c> has executed the real tool and fed the result back) a final text
/// turn, exactly like a real multi-turn conversation with a real model.
/// </summary>
public sealed class StubChatClient(IReadOnlyList<ChatResponse> turns) : IChatClient
{
    private int _index;

    /// <summary>Every request this stub actually received, in order — lets a test assert on what the
    /// tool-calling loop sent back (e.g. that a tool result was fed into the next turn).</summary>
    public List<IReadOnlyList<ChatMessage>> ReceivedMessages { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add([.. messages]);

        if (_index >= turns.Count)
        {
            throw new InvalidOperationException(
                $"StubChatClient received a call beyond its {turns.Count} scripted turn(s) — the tool-calling loop made more round-trips than the test scripted.");
        }

        return Task.FromResult(turns[_index++]);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "QuoteDesk runs the tool-calling loop non-streaming everywhere (docs/SPEC.md §4) — this stub never needs to serve a streaming call.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
