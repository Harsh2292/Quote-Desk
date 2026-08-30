namespace QuoteDesk.Agents.Pipeline;

/// <summary>Enforces "max N tool calls per run, then a forced summary" (tasks/task-06). One instance
/// per Resolve invocation, shared across every tool <see cref="TracedAIFunction"/> wraps for that run.</summary>
public sealed class ToolCallBudget(int max)
{
    private int _count;

    public int Max { get; } = max;

    public int Count => _count;

    /// <summary>True if this call is within budget and may proceed.</summary>
    public bool TryReserve() => Interlocked.Increment(ref _count) <= Max;
}
