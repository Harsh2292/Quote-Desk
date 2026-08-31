namespace QuoteDesk.Data.Entities;

/// <summary>
/// One pipeline run of one enquiry through Extract → Resolve → Price → Approve. This is what
/// <c>GET /api/approvals</c> (task 07) reads to list pending approvals, and what
/// <see cref="Repositories.IAgentRunRepository"/> uses to find the checkpoint session to resume.
/// </summary>
public class AgentRun
{
    public int Id { get; set; }

    public required int EnquiryId { get; set; }

    /// <summary>The Microsoft.Agents.AI.Workflows run id — the key <see cref="WorkflowCheckpoint"/>
    /// rows are stored under.</summary>
    public required string SessionId { get; set; }

    /// <summary>"running" / "pending_approval" / "completed" / "rejected" / "failed".</summary>
    public required string Status { get; set; }

    /// <summary>The <c>ApprovalRequest</c> the Price stage produced, serialized, once the run is
    /// suspended awaiting a human decision. Null until then.</summary>
    public string? ApprovalRequestJson { get; set; }

    /// <summary>Every <c>AgentEvent</c> this run has emitted, serialized as a JSON array, appended to
    /// (not replaced) as the run progresses — the Api's SSE writer (task 07) is the only thing that
    /// writes this column, once per stream, in a <c>finally</c> so a dropped connection still leaves
    /// whatever ran on the record. Null until the first event is persisted. This is what
    /// <c>GET /api/enquiries/{id}</c> and <c>GET /api/quotes/{id}</c> replay once the live SSE stream
    /// has closed — CLAUDE.md calls the Agent Trace panel "the product", so it must survive a page
    /// refresh, not only exist while a browser tab is watching.</summary>
    public string? TraceJson { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }

    public Enquiry? Enquiry { get; set; }
}
