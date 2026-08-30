namespace QuoteDesk.Data.Repositories;

public interface IAgentRunRepository
{
    Task<AgentRunRecord> CreateAsync(NewAgentRun run, CancellationToken cancellationToken);

    /// <summary>The workflow's own run id — how QuoteDesk.Agents finds which checkpoint session to
    /// resume from a bare enquiry id.</summary>
    Task<AgentRunRecord?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken);

    Task<AgentRunRecord?> GetLatestByEnquiryIdAsync(int enquiryId, CancellationToken cancellationToken);

    /// <summary>Runs suspended awaiting a human decision — what <c>GET /api/approvals</c> (task 07)
    /// lists.</summary>
    Task<IReadOnlyList<AgentRunRecord>> GetPendingApprovalsAsync(CancellationToken cancellationToken);

    Task<AgentRunRecord> UpdateStatusAsync(
        int id,
        string status,
        string? approvalRequestJson,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken);
}
