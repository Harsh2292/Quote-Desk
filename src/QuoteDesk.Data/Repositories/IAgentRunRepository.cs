namespace QuoteDesk.Data.Repositories;

public interface IAgentRunRepository
{
    Task<AgentRunRecord> CreateAsync(NewAgentRun run, CancellationToken cancellationToken);

    /// <summary>The workflow's own run id — how QuoteDesk.Agents finds which checkpoint session to
    /// resume from a bare enquiry id.</summary>
    Task<AgentRunRecord?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken);

    Task<AgentRunRecord?> GetByIdAsync(int id, CancellationToken cancellationToken);

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

    /// <summary>Overwrites <c>TraceJson</c> with <paramref name="traceJson"/> — the Api's SSE writer
    /// (task 07) calls this exactly once, in a <c>finally</c>, after a <c>/process</c> or
    /// <c>/approvals/{id}</c> stream ends (or the connection drops). A run can be streamed twice
    /// (once to suspend at Approve, once to resume), so the caller is responsible for reading the
    /// existing trace first and passing the merged whole — this method does not itself append.</summary>
    Task AppendTraceAsync(int id, string traceJson, DateTimeOffset updatedAt, CancellationToken cancellationToken);
}
