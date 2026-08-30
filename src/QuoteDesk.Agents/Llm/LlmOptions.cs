namespace QuoteDesk.Agents.Llm;

/// <summary>
/// Bound from the "Llm" config section by QuoteDesk.Api (task 07) and passed to
/// <c>AddQuoteDeskAgents</c> — the same pattern QuoteDesk.Data's connection string uses, rather than
/// this project taking a dependency on <c>Microsoft.Extensions.Configuration</c> itself.
/// <see cref="Endpoint"/>/<see cref="ApiKey"/>/<see cref="Model"/> are empty-string defaults in
/// appsettings.json, filled locally via <c>dotnet user-secrets</c> (docs/SPEC.md §4: pinned model
/// <c>gemini-3.6-flash</c> against the Gemini OpenAI-compatibility endpoint).
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string Model { get; init; }

    /// <summary>tasks/task-06: "Max 8 tool calls per run, then a forced summary".</summary>
    public int MaxToolCalls { get; init; } = 8;

    /// <summary>tasks/task-06: "Per-conversation token budget, returning a clean budget_exceeded
    /// rather than looping". Generous enough for the worked example's handful of tool calls plus
    /// narration; not tuned against a live model yet.</summary>
    public int TokenBudget { get; init; } = 20_000;
}
