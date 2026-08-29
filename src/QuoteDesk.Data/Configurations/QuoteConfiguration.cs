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
        builder.Property(q => q.ApprovedBy).HasMaxLength(200);

        builder.HasOne(q => q.Enquiry)
            .WithMany()
            .HasForeignKey(q => q.EnquiryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Lines)
            .WithOne(l => l.Quote)
            .HasForeignKey(l => l.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
