using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class EnquiryConfiguration : IEntityTypeConfiguration<Enquiry>
{
    public void Configure(EntityTypeBuilder<Enquiry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SenderId).HasMaxLength(200).IsRequired();
        // No max length: task 04 tests a 50KB pasted body, so this stays nvarchar(max).
        builder.Property(e => e.RawBody).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(30).IsRequired();

        builder.HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
