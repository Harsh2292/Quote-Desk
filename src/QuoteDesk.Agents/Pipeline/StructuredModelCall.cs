using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// One model call that has to come back as <typeparamref name="T"/>, with three layers of defence so
/// a single malformed reply cannot kill a run:
///
/// <list type="number">
/// <item>Ask the provider to <b>enforce the shape</b> — a JSON schema generated from
/// <typeparamref name="T"/> itself, so there is no hand-written schema to drift out of sync.</item>
/// <item>If the provider rejects schema mode, <b>fall back to plain text</b> for that call and log
/// loudly — Gemini's support for it is unverified for this project's model, so this path is real.</item>
/// <item>If the reply still cannot be parsed, <b>retry once with the parse error fed back</b>. A blind
/// retry just fails the same way; handing the model the actual error is what makes it useful.</item>
/// </list>
///
/// Token usage for both attempts is counted by <see cref="BudgetedChatClient"/>, which sits under
/// every agent — not here.
/// </summary>
public static partial class StructuredModelCall
{
    private static readonly JsonSerializerOptions SchemaOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T> RunAsync<T>(
        AIAgent agent,
        string prompt,
        bool useSchema,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(logger);

        var schemaAccepted = useSchema;
        AgentResponse response;
        try
        {
            response = await agent.RunAsync(prompt, session: null, options: SchemaOptionsFor<T>(useSchema), cancellationToken);
        }
        catch (Exception ex) when (useSchema && IsSchemaUnsupported(ex))
        {
            LogSchemaRejected(logger, ex, typeof(T).Name);
            schemaAccepted = false;
            response = await agent.RunAsync(prompt, session: null, options: null, cancellationToken);
        }

        try
        {
            return ModelJson.Parse<T>(response.Text);
        }
        catch (Exception parseFailure) when (IsParseFailure(parseFailure))
        {
            LogParseFailureRetrying(logger, parseFailure, typeof(T).Name);

            var corrective = $"""
                {prompt}

                ---
                Your previous reply could not be used. It failed with:

                {parseFailure.Message}

                Reply again with only the JSON object this task requires — no prose before or after it,
                no code fence, and every required field present.
                """;

            var retry = await agent.RunAsync(
                corrective, session: null, options: SchemaOptionsFor<T>(schemaAccepted), cancellationToken);
            return ModelJson.Parse<T>(retry.Text);
        }
    }

    /// <summary>Run options carrying a schema generated from <typeparamref name="T"/>, or null when
    /// schema mode is off — in which case the model is only asked for JSON by the prompt.</summary>
    private static ChatClientAgentRunOptions? SchemaOptionsFor<T>(bool useSchema) =>
        useSchema
            ? new ChatClientAgentRunOptions(new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<T>(SchemaOptions, typeof(T).Name),
            })
            : null;

    private static bool IsParseFailure(Exception ex) =>
        ex is JsonException or InvalidOperationException or NotSupportedException or ArgumentException;

    /// <summary>Best-effort detection of "this provider/model does not do schema-enforced output".
    /// There is no status code for it — providers answer with a 400 naming the offending field.</summary>
    private static bool IsSchemaUnsupported(Exception ex)
    {
        if (ex is NotSupportedException)
        {
            return true;
        }

        var message = ex.Message;
        return message.Contains("response_format", StringComparison.OrdinalIgnoreCase)
            || message.Contains("responseSchema", StringComparison.OrdinalIgnoreCase)
            || message.Contains("json_schema", StringComparison.OrdinalIgnoreCase)
            || message.Contains("responseMimeType", StringComparison.OrdinalIgnoreCase);
    }

    // Source-generated (CA1848: LoggerMessage delegates instead of the LoggerExtensions convenience
    // methods) — a real diagnostic `dotnet build` never surfaced because CA1848 only fires under
    // Release configuration, and nothing in this repo published Release before task 09's Dockerfile.
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The provider rejected schema-enforced output for {Type}. Falling back to plain-text parsing for this call — "
            + "set Llm:UseStructuredOutput to false to stop paying for the rejected attempt on every run.")]
    private static partial void LogSchemaRejected(ILogger logger, Exception exception, string type);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Model reply for {Type} could not be parsed. Retrying once with the error fed back.")]
    private static partial void LogParseFailureRetrying(ILogger logger, Exception exception, string type);
}
