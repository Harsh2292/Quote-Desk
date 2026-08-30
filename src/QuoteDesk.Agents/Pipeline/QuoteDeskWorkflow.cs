using Microsoft.Agents.AI.Workflows;

namespace QuoteDesk.Agents.Pipeline;

/// <summary>The four freshly constructed nodes for one pipeline run — see <see cref="EnquiryPipeline"/>
/// for why a fresh set is built per run rather than shared.</summary>
public sealed record WorkflowNodes(ExtractExecutor Extract, ResolveExecutor Resolve, PriceExecutor Price, ApproveExecutor Approve);

/// <summary>
/// Assembles the fixed pipeline graph: <c>Extract → Resolve → Price → [approval RequestPort] →
/// Approve</c>. The sequence is wired once, here, and never reordered or skipped — the state-machine
/// half of "a fixed pipeline with one autonomous stage" (docs/SPEC.md §2). A plain <c>Executor</c> or
/// <c>RequestPort</c> converts to <c>ExecutorBinding</c> implicitly; no extra ceremony is needed to
/// wire them into <see cref="WorkflowBuilder"/>.
/// </summary>
public static class QuoteDeskWorkflow
{
    /// <summary>The <see cref="RequestPort"/>'s id — the one place a suspended run's
    /// <c>RequestInfoEvent.Request.PortInfo.PortId</c> is compared against, should more than one port
    /// ever exist.</summary>
    public const string ApprovalPortId = "approval";

    public static Workflow Build(WorkflowNodes nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var approvalPort = RequestPort.Create<ApprovalRequest, ApprovalDecision>(ApprovalPortId);

        return new WorkflowBuilder(nodes.Extract)
            .AddEdge(nodes.Extract, nodes.Resolve)
            .AddEdge(nodes.Resolve, nodes.Price)
            .AddEdge(nodes.Price, approvalPort)
            .AddEdge(approvalPort, nodes.Approve)
            .WithOutputFrom(nodes.Approve)
            .Build();
    }
}
