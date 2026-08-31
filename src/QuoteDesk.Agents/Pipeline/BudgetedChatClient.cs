using Microsoft.Extensions.AI;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// Counts every underlying model call against the run's token budget, and throws the moment the
/// budget is breached.
///
/// This exists because counting used to happen only <i>after</i> a whole agent run finished. Resolve's
/// tool loop can make up to <c>MaxToolCalls</c> round-trips, each re-sending the accumulated
/// conversation, so a run could spend several times its budget before anybody looked — one recorded
/// run reached 56,463 tokens against a 20,000 budget and reported it only once it was over.
/// Wrapping the client instead makes the budget a governor rather than a post-mortem.
///
/// Wrapped <b>inside</b> the function-invocation middleware so it sees each raw round-trip, not one
/// aggregate per agent run. One instance per pipeline run, sharing that run's tracker.
/// </summary>
public sealed class BudgetedChatClient(IChatClient inner, TokenUsageTracker tokens) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        tokens.Add(response.Usage?.InputTokenCount, response.Usage?.OutputTokenCount);
        return response;
    }
}
