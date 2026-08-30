using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class WorkflowCheckpointConfiguration : IEntityTypeConfiguration<WorkflowCheckpoint>
{
    public void Configure(EntityTypeBuilder<WorkflowCheckpoint> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.SessionId).HasMaxLength(100).IsRequired();
        builder.Property(c => c.CheckpointId).HasMaxLength(100).IsRequired();
        builder.Property(c => c.ParentCheckpointId).HasMaxLength(100);
        // No max length: a workflow checkpoint payload has no fixed size bound.
        builder.Property(c => c.Payload).IsRequired();

        builder.HasIndex(c => new { c.SessionId, c.CheckpointId }).IsUnique();
    }
}
