namespace QuoteDesk.Data.Entities;

/// <summary>
/// One committed checkpoint of a suspended Microsoft.Agents.AI.Workflows run — the backing store
/// behind <c>ICheckpointStore&lt;JsonElement&gt;</c> in QuoteDesk.Agents. <see cref="Payload"/> is the
/// framework's own serialized workflow state; this table's only job is to survive a process restart,
/// never to interpret what is inside it.
/// </summary>
public class WorkflowCheckpoint
{
    public int Id { get; set; }

    /// <summary>The workflow run id — one per enquiry pipeline run.</summary>
    public required string SessionId { get; set; }

    public required string CheckpointId { get; set; }

    /// <summary>Null for the first checkpoint of a run.</summary>
    public string? ParentCheckpointId { get; set; }

    /// <summary>The framework's serialized <c>JsonElement</c> checkpoint state, stored verbatim.</summary>
    public required string Payload { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
