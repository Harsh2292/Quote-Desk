using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.SessionId).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(30).IsRequired();
        // No max length: the ApprovalRequest payload has no fixed size bound.
        builder.Property(r => r.ApprovalRequestJson);
        // No max length: a full run's trace has no fixed size bound either.
        builder.Property(r => r.TraceJson);

        builder.HasIndex(r => r.SessionId).IsUnique();

        builder.HasOne(r => r.Enquiry)
            .WithMany()
            .HasForeignKey(r => r.EnquiryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
