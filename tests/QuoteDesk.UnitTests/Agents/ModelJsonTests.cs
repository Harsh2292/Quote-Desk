using FluentAssertions;
using QuoteDesk.Agents.Pipeline;

namespace QuoteDesk.UnitTests.Agents;

/// <summary>
/// The tolerant JSON extraction every model reply is parsed through, in place of
/// <c>AIAgent.RunAsync&lt;T&gt;</c>'s built-in structured output — Gemini's OpenAI-compatibility
/// support for the `json_schema` response format is unverified for this project's model, and that
/// mode's own deserializer is not fence-tolerant, so this is used uniformly rather than as a
/// caught-failure fallback.
/// </summary>
public class ModelJsonTests
{
    private sealed record Sample(string Name, int Count);

    [Fact]
    public void Parse_PlainJson_Deserializes()
    {
        var result = ModelJson.Parse<Sample>("""{"Name":"bearing","Count":3}""");

        result.Should().Be(new Sample("bearing", 3));
    }

    [Fact]
    public void Parse_FencedJsonBlock_StripsTheFence()
    {
        var text = """
            Here is the result:
            ```json
            {"Name":"bearing","Count":3}
            ```
            """;

        var result = ModelJson.Parse<Sample>(text);

        result.Should().Be(new Sample("bearing", 3));
    }

    [Fact]
    public void Parse_JsonWithSurroundingProse_ExtractsTheObject()
    {
        var text = "Sure, here you go: {\"Name\":\"bearing\",\"Count\":3} — let me know if that helps.";

        var result = ModelJson.Parse<Sample>(text);

        result.Should().Be(new Sample("bearing", 3));
    }
}
