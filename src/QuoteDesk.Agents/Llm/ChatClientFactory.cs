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
    public static IChatClient Create(LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Provider switch
        {
            "github" => CreateOpenAiCompatible(options),
            "gemini" => new Google.GenAI.Client(apiKey: options.ApiKey).AsIChatClient(options.Model),
            _ => throw new InvalidOperationException(
                $"Unknown Llm:Provider '{options.Provider}'. Expected 'gemini' or 'github'."),
        };
    }

    private static IChatClient CreateOpenAiCompatible(LlmOptions options)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) });

        return client.GetChatClient(options.Model).AsIChatClient();
    }
}
