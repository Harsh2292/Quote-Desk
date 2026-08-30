namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// Tracks cumulative token usage across one pipeline run — Extract, Resolve, and Price's narration
/// call. Shared by reference across the executors built for one run (they are all constructed fresh
/// per run — see <see cref="EnquiryPipeline"/>), never persisted: only Approve suspends, and it makes
/// no model calls, so nothing here needs to survive a checkpoint.
/// </summary>
public sealed class TokenUsageTracker(int budget)
{
    private long _promptTokens;
    private long _completionTokens;

    public int Budget { get; } = budget;
    public long PromptTokens => _promptTokens;
    public long CompletionTokens => _completionTokens;
    public long Total => _promptTokens + _completionTokens;

    /// <exception cref="BudgetExceededException">Recording this usage would exceed <see cref="Budget"/>.</exception>
    public void Add(long? promptTokens, long? completionTokens)
    {
        Interlocked.Add(ref _promptTokens, promptTokens ?? 0);
        Interlocked.Add(ref _completionTokens, completionTokens ?? 0);

        if (Total > Budget)
        {
            throw new BudgetExceededException((int)Math.Min(Total, int.MaxValue), Budget);
        }
    }
}
