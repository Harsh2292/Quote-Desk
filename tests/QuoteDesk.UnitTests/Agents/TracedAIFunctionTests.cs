using FluentAssertions;
using Microsoft.Extensions.AI;
using QuoteDesk.Agents.Pipeline;

namespace QuoteDesk.UnitTests.Agents;

/// <summary>
/// "Max 8 tool calls per run, then a forced summary" and "every stage and tool call is traced"
/// (tasks/task-06-agents-workflow.md, CLAUDE.md rule 4) — both guardrails live in one wrapper, tested
/// here against a trivial inner <see cref="AIFunction"/> rather than a real tool.
/// </summary>
public class TracedAIFunctionTests
{
    [Fact]
    public async Task InvokeAsync_WithinBudget_InvokesInnerFunctionAndEmitsStartAndEnd()
    {
        var events = new List<AgentEvent>();
        var invocations = 0;
        var inner = AIFunctionFactory.Create(
            () => { invocations++; return "ok"; },
            new AIFunctionFactoryOptions { Name = "echo" });
        var traced = new TracedAIFunction(inner, new ToolCallBudget(max: 8), (evt, ct) => { events.Add(evt); return ValueTask.CompletedTask; });

        var result = await traced.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ToString().Should().Be("ok", "actual runtime type was {0}", result.GetType());
        invocations.Should().Be(1);
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<ToolStartEvent>();
        var end = events[1].Should().BeOfType<ToolEndEvent>().Subject;
        end.Ok.Should().BeTrue();
        end.Name.Should().Be("echo");
    }

    [Fact]
    public async Task InvokeAsync_CallNumberNinePastAnEightCap_RefusesWithoutInvokingInner()
    {
        var events = new List<AgentEvent>();
        var budget = new ToolCallBudget(max: 8);
        var invocations = 0;
        var counting = AIFunctionFactory.Create(
            (string x) => { invocations++; return x; },
            new AIFunctionFactoryOptions { Name = "echo" });
        var traced = new TracedAIFunction(counting, budget, (evt, ct) => { events.Add(evt); return ValueTask.CompletedTask; });

        for (var i = 0; i < 8; i++)
        {
            await traced.InvokeAsync(new AIFunctionArguments { ["x"] = "value" }, CancellationToken.None);
        }

        invocations.Should().Be(8);
        events.Clear();

        var ninthResult = await traced.InvokeAsync(new AIFunctionArguments { ["x"] = "value" }, CancellationToken.None);

        invocations.Should().Be(8, "the ninth call must not reach the inner function");
        ninthResult.Should().BeOfType<string>().Which.Should().Contain("budget exhausted");
        events.Should().ContainSingle().Which.Should().BeOfType<ToolEndEvent>()
            .Which.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenInnerThrows_EmitsFailedToolEndAndRethrows()
    {
        var events = new List<AgentEvent>();
        var budget = new ToolCallBudget(max: 8);
        var throwing = AIFunctionFactory.Create(
            new Func<object?>(() => throw new InvalidOperationException("boom")),
            new AIFunctionFactoryOptions { Name = "explode" });
        var traced = new TracedAIFunction(throwing, budget, (evt, ct) => { events.Add(evt); return ValueTask.CompletedTask; });

        Func<Task> act = () => traced.InvokeAsync(new AIFunctionArguments(), CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
        events.Should().HaveCount(2);
        events[1].Should().BeOfType<ToolEndEvent>().Which.Ok.Should().BeFalse();
    }
}
