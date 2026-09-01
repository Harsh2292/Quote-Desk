using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace QuoteDesk.Agents.Llm;

/// <summary>
/// Builds the one <see cref="IChatClient"/> the whole pipeline is layered on. Originally both free
/// providers in docs/SPEC.md §4 spoke the OpenAI wire protocol, so swapping providers was only ever a
/// different <see cref="LlmOptions.Endpoint"/> — that stopped being true for the "gemini" profile once
/// docs/SPEC.md §4's `thought_signature` correction was resolved: Gemini's OpenAI-compatibility
/// endpoint silently drops the `thought_signature` a multi-turn tool call needs, so the "gemini"
/// profile now goes through Google's own native SDK (<c>Google.GenAI</c>) instead, which round-trips
/// it correctly (confirmed live during the task-07 debugging session; see
/// <c>tests/QuoteDesk.Evals/GeminiWorkedExampleEval.cs</c> for the full-pipeline proof).
/// "github" is unaffected — a real
/// OpenAI endpoint, no `thought_signature` involved — and keeps the original OpenAI-compatible path.
/// </summary>
public static class ChatClientFactory
{
    /// <summary>Builds a client for <see cref="LlmOptions.Model"/> — the single-model shape every
    /// caller used before task 09's per-stage routing (the eval harnesses, and any deployment that
    /// never sets the per-stage overrides). Equivalent to <c>Create(options, options.Model)</c>.</summary>
    public static IChatClient Create(LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Create(options, options.Model);
    }

    /// <summary>Builds a client for an explicit <paramref name="model"/>, independent of
    /// <see cref="LlmOptions.Model"/> — what <see cref="ChatClientRegistry"/> calls once per distinct
    /// model name across Extract/Resolve/Narrate.</summary>
    public static IChatClient Create(LlmOptions options, string model)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return options.Provider switch
        {
            "github" => CreateOpenAiCompatible(options, model),
            "gemini" => new Google.GenAI.Client(apiKey: options.ApiKey).AsIChatClient(model),
            _ => throw new InvalidOperationException(
                $"Unknown Llm:Provider '{options.Provider}'. Expected 'gemini' or 'github'."),
        };
    }

    private static IChatClient CreateOpenAiCompatible(LlmOptions options, string model)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) });

        return client.GetChatClient(model).AsIChatClient();
    }
}
