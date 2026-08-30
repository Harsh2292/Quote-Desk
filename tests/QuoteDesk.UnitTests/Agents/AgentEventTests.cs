using System.Text.Json;
using FluentAssertions;
using QuoteDesk.Agents.Pipeline;

namespace QuoteDesk.UnitTests.Agents;

/// <summary>
/// "The AgentEvent union mirrors the C# contract exactly" (CLAUDE.md, Frontend) — checked here by
/// serializing each variant and asserting the exact "type" discriminator docs/SPEC.md §8 defines, so
/// a future rename is caught at the boundary rather than silently breaking the TypeScript side.
/// </summary>
public class AgentEventTests
{
    [Theory]
    [InlineData(typeof(StageEvent), "stage")]
    [InlineData(typeof(ToolStartEvent), "tool_start")]
    [InlineData(typeof(ToolEndEvent), "tool_end")]
    [InlineData(typeof(TokenEvent), "token")]
    [InlineData(typeof(ApprovalRequiredEvent), "approval_required")]
    [InlineData(typeof(DoneEvent), "done")]
    [InlineData(typeof(ErrorEvent), "error")]
    public void Serializing_EachVariant_ProducesTheExactTypeDiscriminator(Type variantType, string expectedDiscriminator)
    {
        AgentEvent instance = variantType.Name switch
        {
            nameof(StageEvent) => new StageEvent { Stage = "extract", At = DateTimeOffset.UtcNow },
            nameof(ToolStartEvent) => new ToolStartEvent { Name = "search_catalog", Args = null, At = DateTimeOffset.UtcNow },
            nameof(ToolEndEvent) => new ToolEndEvent { Name = "search_catalog", Ms = 12, Ok = true, Result = null },
            nameof(TokenEvent) => new TokenEvent { Text = "hello" },
            nameof(ApprovalRequiredEvent) => new ApprovalRequiredEvent { ApprovalId = "1", Action = "approve_quote", Payload = new { } },
            nameof(DoneEvent) => new DoneEvent { Usage = new UsageInfo { PromptTokens = 1, CompletionTokens = 1 } },
            nameof(ErrorEvent) => new ErrorEvent { Code = "internal", Message = "boom" },
            _ => throw new ArgumentOutOfRangeException(nameof(variantType)),
        };

        var json = JsonSerializer.Serialize(instance);
        using var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("type").GetString().Should().Be(expectedDiscriminator);
    }

    [Fact]
    public void Serializing_ThroughTheBaseType_StillProducesTheDiscriminator()
    {
        // AgentEvent is what actually flows through the pipeline (AgentTraceEvent.Event), so
        // polymorphic serialization must work through the abstract base, not just the concrete type.
        AgentEvent evt = new StageEvent { Stage = "resolve", At = DateTimeOffset.UtcNow };

        var json = JsonSerializer.Serialize(evt);

        json.Should().Contain("\"type\":\"stage\"");
    }
}
