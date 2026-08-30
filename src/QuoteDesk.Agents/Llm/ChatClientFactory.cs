using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace QuoteDesk.Agents.Llm;

/// <summary>
/// Builds the one <see cref="IChatClient"/> the whole pipeline is layered on. Both free providers in
/// docs/SPEC.md §4 (Gemini's OpenAI-compatibility endpoint, GitHub Models) speak the OpenAI wire
/// protocol, so swapping providers is only ever a different <see cref="LlmOptions.Endpoint"/> — no
/// code path here is Gemini-specific.
/// </summary>
public static class ChatClientFactory
{
    public static IChatClient Create(LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var client = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) });

        return client.GetChatClient(options.Model).AsIChatClient();
    }
}
