using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Repositories;

public sealed class AgentRunRepository(QuoteDeskDbContext db) : IAgentRunRepository
{
    public async Task<AgentRunRecord> CreateAsync(NewAgentRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        var entity = new AgentRun
        {
            EnquiryId = run.EnquiryId,
            SessionId = run.SessionId,
            Status = run.Status,
            CreatedAt = run.CreatedAt,
            UpdatedAt = run.CreatedAt,
        };

        db.AgentRuns.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return ToRecord(entity);
    }

    public async Task<AgentRunRecord?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        var entity = await db.AgentRuns.AsNoTracking()
            .SingleOrDefaultAsync(r => r.SessionId == sessionId, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<AgentRunRecord?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await db.AgentRuns.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<AgentRunRecord?> GetLatestByEnquiryIdAsync(int enquiryId, CancellationToken cancellationToken)
    {
        // Ordered by Id, not CreatedAt: two runs for the same enquiry can share an identical
        // timestamp under a fixed TimeProvider (every integration test in this project uses one),
        // and Id — auto-increment — is the only tiebreaker guaranteed to reflect insertion order.
        var entity = await db.AgentRuns.AsNoTracking()
            .Where(r => r.EnquiryId == enquiryId)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<AgentRunRecord>> GetPendingApprovalsAsync(CancellationToken cancellationToken)
    {
        var entities = await db.AgentRuns.AsNoTracking()
            .Where(r => r.Status == AgentRunStatuses.PendingApproval)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToRecord)];
    }

    public async Task<AgentRunRecord> UpdateStatusAsync(
        int id,
        string status,
        string? approvalRequestJson,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var entity = await db.AgentRuns.SingleAsync(r => r.Id == id, cancellationToken);

        entity.Status = status;
        entity.ApprovalRequestJson = approvalRequestJson;
        entity.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(cancellationToken);

        return ToRecord(entity);
    }

    public async Task AppendTraceAsync(int id, string traceJson, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(traceJson);

        var entity = await db.AgentRuns.SingleAsync(r => r.Id == id, cancellationToken);

        entity.TraceJson = traceJson;
        entity.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AgentRunRecord ToRecord(AgentRun r) => new(
        r.Id, r.EnquiryId, r.SessionId, r.Status, r.ApprovalRequestJson, r.TraceJson, r.CreatedAt, r.UpdatedAt);
}

/// <summary>The status vocabulary <see cref="AgentRun.Status"/> is written from. Lives here, not in
/// QuoteDesk.Agents, only because <see cref="AgentRunRepository.GetPendingApprovalsAsync"/> needs to
/// filter on it directly in a LINQ query — the same reasoning as QuoteDesk.Agents.Tools.QuoteStatus,
/// just on the other side of the boundary since this table's own repository is the one filtering.</summary>
public static class AgentRunStatuses
{
    public const string Running = "running";
    public const string PendingApproval = "pending_approval";
    public const string Completed = "completed";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
}
