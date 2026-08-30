using System.Text.Json.Serialization;
using Microsoft.Agents.AI.Workflows;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>
/// Mirrors docs/SPEC.md §8's TypeScript <c>AgentEvent</c> union exactly — the two must change in the
/// same commit (CLAUDE.md, Frontend section). This is the only place either side is defined.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StageEvent), "stage")]
[JsonDerivedType(typeof(ToolStartEvent), "tool_start")]
[JsonDerivedType(typeof(ToolEndEvent), "tool_end")]
[JsonDerivedType(typeof(TokenEvent), "token")]
[JsonDerivedType(typeof(ApprovalRequiredEvent), "approval_required")]
[JsonDerivedType(typeof(DoneEvent), "done")]
[JsonDerivedType(typeof(ErrorEvent), "error")]
public abstract record AgentEvent;

public sealed record StageEvent : AgentEvent
{
    /// <summary>"extract" | "resolve" | "price".</summary>
    public required string Stage { get; init; }
    public required DateTimeOffset At { get; init; }
}

public sealed record ToolStartEvent : AgentEvent
{
    public required string Name { get; init; }
    public required object? Args { get; init; }
    public required DateTimeOffset At { get; init; }
}

public sealed record ToolEndEvent : AgentEvent
{
    public required string Name { get; init; }
    public required long Ms { get; init; }
    public required bool Ok { get; init; }
    public required object? Result { get; init; }
}

public sealed record TokenEvent : AgentEvent
{
    public required string Text { get; init; }
}

public sealed record ApprovalRequiredEvent : AgentEvent
{
    public required string ApprovalId { get; init; }
    public required string Action { get; init; }
    public required object Payload { get; init; }
}

public sealed record UsageInfo
{
    public required int PromptTokens { get; init; }
    public required int CompletionTokens { get; init; }
}

public sealed record DoneEvent : AgentEvent
{
    public required UsageInfo Usage { get; init; }
}

public sealed record ErrorEvent : AgentEvent
{
    /// <summary>"provider_rate_limited" | "budget_exceeded" | "internal".</summary>
    public required string Code { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Carries one <see cref="AgentEvent"/> over the workflow's own event stream — <c>IWorkflowContext
/// .AddEventAsync</c> is how every executor raises trace events (CLAUDE.md rule 4: "every stage and
/// tool call is traced"), and <c>StreamingRun.WatchStreamAsync</c> (task 07) is the transport back out
/// to the SSE writer, which unwraps <see cref="Event"/> for the browser. No separate channel or queue
/// is needed — the framework's own event plumbing already does this job.
/// </summary>
public sealed class AgentTraceEvent(AgentEvent @event) : WorkflowEvent(@event)
{
    public AgentEvent Event => (AgentEvent)Data!;
}
