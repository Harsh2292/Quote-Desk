using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>
/// A model does not follow a formatting instruction with 100% reliability — confirmed live against
/// real <c>gemini-3.6-flash</c> during task 07's manual verification, where <c>requiredBy</c> came
/// back as the ISO date the prompt asks for on one run and the literal word <c>"5th"</c> on another,
/// for the same enquiry. The strict built-in <see cref="DateOnly"/> converter throws on the second
/// case, which would fail the entire Extract stage over one optional, informational field — nothing
/// downstream depends on <c>RequiredBy</c> being present (docs/DOMAIN.md's actual delivery dates are
/// computed by <c>QuoteDesk.Domain</c> from stock and lead time, never from what the customer asked
/// for). So this degrades a date it cannot confidently parse to null, the same as the field never
/// having been stated at all, rather than letting one messy field take down the whole run.
/// </summary>
public sealed class LenientNullableDateOnlyConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            return null;
        }

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is { } date)
        {
            writer.WriteStringValue(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
