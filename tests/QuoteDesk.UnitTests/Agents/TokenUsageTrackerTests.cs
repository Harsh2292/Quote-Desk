using FluentAssertions;
using QuoteDesk.Agents.Pipeline;

namespace QuoteDesk.UnitTests.Agents;

/// <summary>
/// "Per-conversation token budget, returning a clean budget_exceeded rather than looping"
/// (tasks/task-06-agents-workflow.md).
/// </summary>
public class TokenUsageTrackerTests
{
    [Fact]
    public void Add_WithinBudget_AccumulatesAndDoesNotThrow()
    {
        var tracker = new TokenUsageTracker(budget: 100);

        tracker.Add(40, 10);
        tracker.Add(20, 5);

        tracker.Total.Should().Be(75);
        tracker.PromptTokens.Should().Be(60);
        tracker.CompletionTokens.Should().Be(15);
    }

    [Fact]
    public void Add_ExactlyAtBudget_DoesNotThrow()
    {
        var tracker = new TokenUsageTracker(budget: 100);

        var act = () => tracker.Add(60, 40);

        act.Should().NotThrow();
        tracker.Total.Should().Be(100);
    }

    [Fact]
    public void Add_OverBudget_Throws()
    {
        var tracker = new TokenUsageTracker(budget: 100);
        tracker.Add(60, 30);

        var act = () => tracker.Add(5, 6);

        act.Should().Throw<BudgetExceededException>()
            .Which.Budget.Should().Be(100);
    }

    [Fact]
    public void Add_WithNullUsageValues_TreatsAsZero()
    {
        var tracker = new TokenUsageTracker(budget: 100);

        var act = () => tracker.Add(null, null);

        act.Should().NotThrow();
        tracker.Total.Should().Be(0);
    }
}
