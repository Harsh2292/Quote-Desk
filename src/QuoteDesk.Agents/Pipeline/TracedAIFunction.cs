using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// Wraps one read tool so every call is traced (CLAUDE.md rule 4) and the run's tool-call budget is
/// enforced (tasks/task-06: "max 8 tool calls per run, then a forced summary"). One instance per tool
/// per run — <see cref="budget"/> is shared across every tool wrapped for the same Resolve invocation.
/// </summary>
public sealed class TracedAIFunction(
    AIFunction inner,
    ToolCallBudget budget,
    Func<AgentEvent, CancellationToken, ValueTask> emit) : DelegatingAIFunction(inner)
{
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!budget.TryReserve())
        {
            const string Refusal = "Tool call budget exhausted for this run. Stop calling tools and summarize using only what you already know.";
            await emit(new ToolEndEvent { Name = Name, Ms = 0, Ok = false, Result = Refusal }, cancellationToken);
            return Refusal;
        }

        var args = new Dictionary<string, object?>(arguments);
        await emit(new ToolStartEvent { Name = Name, Args = args, At = DateTimeOffset.UtcNow }, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await base.InvokeCoreAsync(arguments, cancellationToken);
            stopwatch.Stop();
            await emit(new ToolEndEvent { Name = Name, Ms = stopwatch.ElapsedMilliseconds, Ok = true, Result = result }, cancellationToken);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            await emit(new ToolEndEvent { Name = Name, Ms = stopwatch.ElapsedMilliseconds, Ok = false, Result = ex.Message }, cancellationToken);
            throw;
        }
    }
}
