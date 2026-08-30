using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using QuoteDesk.Data.Repositories;

namespace QuoteDesk.Agents.Checkpointing;

/// <summary>
/// Bridges the framework's <see cref="ICheckpointStore{JsonElement}"/> onto plain SQL persistence
/// (<see cref="IWorkflowCheckpointRepository"/> in QuoteDesk.Data) — the mechanism a suspended
/// approval survives an application restart through (tasks/task-06-agents-workflow.md). No
/// framework type crosses into QuoteDesk.Data; this class is the entire translation layer.
/// </summary>
/// <remarks>
/// <see cref="ICheckpointStore{TStoreObject}"/>'s three methods carry no <see cref="CancellationToken"/>
/// — that is the framework's own contract, not a deviation from CLAUDE.md's rule, which is about
/// methods this project defines. <see cref="IWorkflowCheckpointRepository"/>, the interface this class
/// actually calls into, has a <see cref="CancellationToken"/> on every method.
/// </remarks>
public sealed class SqlCheckpointStore(IWorkflowCheckpointRepository checkpoints, TimeProvider timeProvider)
    : ICheckpointStore<JsonElement>
{
    public async ValueTask<CheckpointInfo> CreateCheckpointAsync(string sessionId, JsonElement value, CheckpointInfo? parent)
    {
        var checkpointId = Guid.NewGuid().ToString("N");

        await checkpoints.CreateAsync(
            sessionId, checkpointId, parent?.CheckpointId, value.GetRawText(), timeProvider.GetUtcNow(), CancellationToken.None);

        return new CheckpointInfo(sessionId, checkpointId);
    }

    public async ValueTask<JsonElement> RetrieveCheckpointAsync(string sessionId, CheckpointInfo key)
    {
        var payload = await checkpoints.GetPayloadAsync(sessionId, key.CheckpointId, CancellationToken.None);
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }

    public async ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(string sessionId, CheckpointInfo? withParent)
    {
        var entries = await checkpoints.GetIndexAsync(sessionId, withParent?.CheckpointId, CancellationToken.None);
        return entries.Select(e => new CheckpointInfo(sessionId, e.CheckpointId));
    }
}
