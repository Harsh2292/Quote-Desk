using System.Reflection;
using FluentAssertions;
using QuoteDesk.Agents.Pipeline;
using QuoteDesk.Agents.Tools;

namespace QuoteDesk.UnitTests.Agents;

/// <summary>
/// "A test proves the Resolve agent cannot invoke create_quote_draft" (tasks/task-06-agents-workflow.md)
/// — checked statically here, the same reflection-over-the-actual-boundary style as
/// ToolRegistryTests/ToolResultBoundaryTests: <see cref="ResolveExecutor"/> is constructed from a bare
/// list of lookup tools, never from <see cref="WriteToolRegistry"/> or <see cref="QuoteWriteTools"/>,
/// so there is no path from Resolve to a write tool for any prompt or model behaviour to exploit.
/// The complementary runtime check — that the tools actually handed to the model exclude the write
/// tool names — lives in the integration tests against a stubbed IChatClient.
/// </summary>
public class ResolveAgentToolBoundaryTests
{
    [Fact]
    public void ResolveExecutor_HasNoConstructorOrFieldDependencyOnWriteTools()
    {
        var type = typeof(ResolveExecutor);

        var ctorParameterTypes = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType);

        var fieldTypes = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(f => f.FieldType);

        var offending = ctorParameterTypes.Concat(fieldTypes)
            .Where(t => t == typeof(WriteToolRegistry) || t == typeof(QuoteWriteTools))
            .Select(t => t.Name)
            .ToList();

        offending.Should().BeEmpty("the Resolve agent must never be constructed with a path to a write tool");
    }
}
