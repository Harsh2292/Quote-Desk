namespace QuoteDesk.Agents.Llm;

/// <summary>
/// Bound from the "Llm" config section by QuoteDesk.Api (task 07) and passed to
/// <c>AddQuoteDeskAgents</c> — the same pattern QuoteDesk.Data's connection string uses, rather than
/// this project taking a dependency on <c>Microsoft.Extensions.Configuration</c> itself.
/// <see cref="Endpoint"/>/<see cref="ApiKey"/>/<see cref="Model"/> are empty-string defaults in
/// appsettings.json, filled locally via <c>dotnet user-secrets</c>. Task 09 added per-stage routing
/// (<see cref="ExtractModel"/>, <see cref="ResolveModel"/>, <see cref="NarrateModel"/>) once the
/// live pipeline proved that one model for every call bunches ~6 sequential requests against a
/// single free-tier requests-per-minute ceiling; routing stages onto different models also spreads
/// them across different quota buckets. <see cref="QuoteDesk.Agents.Llm.ChatClientRegistry"/> is
/// what turns these into actual <c>IChatClient</c>s, one per distinct model name.
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

    /// <summary>The default model, and the one every stage falls back to when its own
    /// <see cref="ExtractModel"/>/<see cref="ResolveModel"/>/<see cref="NarrateModel"/> is unset —
    /// what keeps the evals and any single-model config binding unchanged.</summary>
    public required string Model { get; init; }

    /// <summary>Model for the Extract stage — messy text into JSON, no tools, no judgement calls.
    /// Falls back to <see cref="Model"/>. docs/SPEC.md §4: routed to a cheap, high-quota model
    /// (<c>gemini-3.5-flash-lite</c>) since nothing here is worth the capable model's scarce quota.</summary>
    public string? ExtractModel { get; init; }

    /// <summary>Model for the Resolve stage — the one autonomous node: a tool-calling loop that has
    /// to weigh candidates and know when it genuinely cannot tell. Falls back to <see cref="Model"/>.
    /// docs/SPEC.md §4: the one call worth paying for the capable model (<c>gemini-3.6-flash</c>).</summary>
    public string? ResolveModel { get; init; }

    /// <summary>Model for the Narrate stage — one sentence built from numbers
    /// <c>QuoteDesk.Domain</c> already computed. Falls back to <see cref="Model"/>. docs/SPEC.md §4:
    /// routed to the cheap model, same reasoning as <see cref="ExtractModel"/>.</summary>
    public string? NarrateModel { get; init; }

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
