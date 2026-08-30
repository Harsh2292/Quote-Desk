namespace QuoteDesk.Agents.Pipeline;

/// <summary>Raised when a run's cumulative token usage crosses <see cref="Llm.LlmOptions.TokenBudget"/>.
/// <see cref="EnquiryPipeline"/> catches this and emits a clean <c>ErrorEvent</c> with code
/// "budget_exceeded" rather than letting the run fail with a raw exception.</summary>
public sealed class BudgetExceededException(int used, int budget)
    : Exception($"Token budget exceeded: used {used} of a {budget}-token budget for this run.")
{
    public int Used { get; } = used;
    public int Budget { get; } = budget;
}
