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

    /// <summary>"gemini" (default) or "github" — which branch <c>ChatClientFactory.Create</c> builds.
    /// Added once adopting <c>Google.GenAI</c> for the "gemini" profile (docs/SPEC.md §4's
    /// `thought_signature` correction) meant the two profiles could no longer share one
    /// OpenAI-compatible client differing only by <see cref="Endpoint"/> — Google's native SDK takes
    /// an API key, not an arbitrary base URL, so the code that builds the client has to know which
    /// one to build.</summary>
    public string Provider { get; init; } = "gemini";

    /// <summary>Meaningful only for <see cref="Provider"/> "github" — Google's native SDK
    /// (<c>Google.GenAI</c>) has no endpoint override, so this is not "any OpenAI-compatible endpoint"
    /// universally any more, just for that one fallback profile.</summary>
    public required string Endpoint { get; init; }
    public required string ApiKey { get; init; }
    public required string Model { get; init; }

    /// <summary>tasks/task-06: "Max 8 tool calls per run, then a forced summary".</summary>
    public int MaxToolCalls { get; init; } = 8;

    /// <summary>tasks/task-06: "Per-conversation token budget, returning a clean budget_exceeded
    /// rather than looping". Generous enough for the worked example's handful of tool calls plus
    /// narration; not tuned against a live model yet.</summary>
    public int TokenBudget { get; init; } = 20_000;

    /// <summary>
    /// Ask the provider to enforce a JSON schema on the stages that must return structured data,
    /// instead of asking politely in the prompt and parsing whatever comes back. On by default: it is
    /// the single biggest reliability lever available, and <see cref="StructuredModelCall"/> falls
    /// back to plain-text parsing if the provider rejects it.
    ///
    /// Whether <c>gemini-3.6-flash</c> honours it is unverified — if a live run logs the
    /// "provider rejected schema-enforced output" warning, set this to false so the pipeline stops
    /// paying for the rejected attempt on every run.
    /// </summary>
    public bool UseStructuredOutput { get; init; } = true;
}
