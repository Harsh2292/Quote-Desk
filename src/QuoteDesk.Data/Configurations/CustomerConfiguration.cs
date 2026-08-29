using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.EmailDomain).HasMaxLength(200);
        builder.Property(c => c.WhatsAppNumber).HasMaxLength(20);
        builder.Property(c => c.Tier).HasConversion<string>().HasMaxLength(1);
        builder.Property(c => c.GstIn).HasMaxLength(20);
        builder.Property(c => c.DefaultShipTo).HasMaxLength(200);

        builder.HasIndex(c => c.EmailDomain);
        builder.HasIndex(c => c.WhatsAppNumber);
    }
}
