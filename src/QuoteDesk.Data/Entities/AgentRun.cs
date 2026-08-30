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

    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }

    public Enquiry? Enquiry { get; set; }
}
