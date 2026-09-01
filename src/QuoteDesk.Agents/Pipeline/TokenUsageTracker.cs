namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// Tracks cumulative token usage across one pipeline run — Extract, Resolve, and Price's narration
/// call. Shared by reference across the executors built for one run (they are all constructed fresh
/// per run — see <see cref="EnquiryPipeline"/>).
///
/// A run suspends and resumes across two separate HTTP requests (<c>/process</c>, then
/// <c>/approvals/{id}</c>), each building a fresh instance of this class — so the totals from the
/// first leg have to be carried forward explicitly via <paramref name="initialPromptTokens"/>/
/// <paramref name="initialCompletionTokens"/> (persisted on <c>AgentRuns</c> between the two
/// requests), or the second leg's tracker silently starts from zero and the real spend is lost.
/// </summary>
public sealed class TokenUsageTracker(int budget, long initialPromptTokens = 0, long initialCompletionTokens = 0)
{
    private long _promptTokens = initialPromptTokens;
    private long _completionTokens = initialCompletionTokens;

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
