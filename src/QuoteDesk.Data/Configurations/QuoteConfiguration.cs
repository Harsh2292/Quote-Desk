using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Number).HasMaxLength(30).IsRequired();
        builder.HasIndex(q => q.Number).IsUnique();

        builder.Property(q => q.Status).HasMaxLength(20).IsRequired();
        builder.Property(q => q.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(q => q.Tax).HasColumnType("decimal(18,2)");
        builder.Property(q => q.Total).HasColumnType("decimal(18,2)");
        builder.HasOne(q => q.Enquiry)
            .WithMany()
            .HasForeignKey(q => q.EnquiryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, not Cascade: deleting a user must never delete the quotes they approved.
        builder.HasOne(q => q.ApprovedByUser)
            .WithMany()
            .HasForeignKey(q => q.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Lines)
            .WithOne(l => l.Quote)
            .HasForeignKey(l => l.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
