namespace QuoteDesk.IntegrationTests.Agents;

/// <summary>A <see cref="TimeProvider"/> stuck at one instant, so date and expiry assertions stay
/// deterministic per CLAUDE.md's rule against a real clock in tests.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
