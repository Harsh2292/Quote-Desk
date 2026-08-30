using System.Text.Json;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// Parses a JSON value out of a model's plain-text reply, tolerating a ```json fenced block or
/// leading/trailing prose around it. Used uniformly instead of <c>AIAgent.RunAsync&lt;T&gt;</c>'s
/// built-in structured-output mode: Gemini's OpenAI-compatibility endpoint's support for the
/// `json_schema` response format is unverified for this project's exact model, and that mode's own
/// deserializer is not fence-tolerant — this is the fallback docs/SPEC.md §4 already planned for,
/// applied uniformly rather than only on a caught failure.
/// </summary>
public static class ModelJson
{
    // Case-insensitive with camelCase as the default naming policy: prompts describe fields in
    // lowerCamelCase (the JSON convention a model is far more likely to actually produce), while the
    // C# contracts it deserializes into are PascalCase — this is what reconciles the two without
    // requiring every model reply to match .NET property casing exactly.
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web);

    public static T Parse<T>(string text, JsonSerializerOptions? options = null)
    {
        var json = ExtractJson(text);
        return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions)
            ?? throw new InvalidOperationException("Model response deserialized to null: " + text);
    }

    private static string ExtractJson(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var fenceStart = text.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            var contentStart = text.IndexOf('\n', fenceStart);
            if (contentStart >= 0)
            {
                var fenceEnd = text.IndexOf("```", contentStart + 1, StringComparison.Ordinal);
                if (fenceEnd >= 0)
                {
                    return text[(contentStart + 1)..fenceEnd].Trim();
                }
            }
        }

        var braceStart = text.IndexOf('{', StringComparison.Ordinal);
        var braceEnd = text.LastIndexOf('}');
        return braceStart >= 0 && braceEnd > braceStart ? text[braceStart..(braceEnd + 1)] : text.Trim();
    }
}
