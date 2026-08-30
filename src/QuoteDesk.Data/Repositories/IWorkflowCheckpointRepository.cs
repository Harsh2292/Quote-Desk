namespace QuoteDesk.Data.Repositories;

/// <summary>
/// Plain persistence for suspended-workflow checkpoints. Deliberately framework-agnostic — no
/// Microsoft.Agents.AI.Workflows type crosses this boundary, matching every other repository in this
/// project. QuoteDesk.Agents bridges this onto the framework's own <c>ICheckpointStore&lt;JsonElement&gt;</c>.
/// </summary>
public interface IWorkflowCheckpointRepository
{
    Task CreateAsync(
        string sessionId,
        string checkpointId,
        string? parentCheckpointId,
        string payload,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken);

    /// <exception cref="KeyNotFoundException">No checkpoint matches the given session and id.</exception>
    Task<string> GetPayloadAsync(string sessionId, string checkpointId, CancellationToken cancellationToken);

    /// <summary>Checkpoints for <paramref name="sessionId"/>, oldest first. When
    /// <paramref name="parentCheckpointId"/> is given, only checkpoints whose parent matches it are
    /// returned; when null, every checkpoint for the session is returned.</summary>
    Task<IReadOnlyList<CheckpointRecord>> GetIndexAsync(
        string sessionId,
        string? parentCheckpointId,
        CancellationToken cancellationToken);
}
