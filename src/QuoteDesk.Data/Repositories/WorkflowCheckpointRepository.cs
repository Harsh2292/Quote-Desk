using Microsoft.EntityFrameworkCore;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Repositories;

/// <summary>
/// Uses <see cref="IDbContextFactory{QuoteDeskDbContext}"/> rather than a shared, injected
/// <see cref="QuoteDeskDbContext"/> — deliberately, not by convention. The workflow engine writes a
/// checkpoint from its own background execution task, concurrently with whatever the caller driving
/// the run's event stream does with the same request's scoped <c>DbContext</c> (e.g. <c>AgentRuns</c>
/// updates in <c>EnquiryPipeline</c>); sharing one <c>DbContext</c> instance between those two
/// concurrent paths throws EF Core's "a second operation was started on this context" exception,
/// found while running task 06's own integration tests. A short-lived context per call sidesteps it
/// without needing to reason about the framework's exact internal scheduling.
/// </summary>
public sealed class WorkflowCheckpointRepository(IDbContextFactory<QuoteDeskDbContext> contextFactory) : IWorkflowCheckpointRepository
{
    public async Task CreateAsync(
        string sessionId,
        string checkpointId,
        string? parentCheckpointId,
        string payload,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        db.WorkflowCheckpoints.Add(new WorkflowCheckpoint
        {
            SessionId = sessionId,
            CheckpointId = checkpointId,
            ParentCheckpointId = parentCheckpointId,
            Payload = payload,
            CreatedAt = createdAt,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetPayloadAsync(string sessionId, string checkpointId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var payload = await db.WorkflowCheckpoints.AsNoTracking()
            .Where(c => c.SessionId == sessionId && c.CheckpointId == checkpointId)
            .Select(c => c.Payload)
            .SingleOrDefaultAsync(cancellationToken);

        return payload ?? throw new KeyNotFoundException(
            $"No checkpoint '{checkpointId}' for session '{sessionId}'.");
    }

    public async Task<IReadOnlyList<CheckpointRecord>> GetIndexAsync(
        string sessionId,
        string? parentCheckpointId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = db.WorkflowCheckpoints.AsNoTracking().Where(c => c.SessionId == sessionId);

        query = parentCheckpointId is null
            ? query
            : query.Where(c => c.ParentCheckpointId == parentCheckpointId);

        var rows = await query
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.CheckpointId, c.ParentCheckpointId, c.CreatedAt })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new CheckpointRecord(r.CheckpointId, r.ParentCheckpointId, r.CreatedAt))];
    }
}
