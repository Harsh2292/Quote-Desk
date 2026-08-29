using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class OrderHistoryConfiguration : IEntityTypeConfiguration<OrderHistory>
{
    public void Configure(EntityTypeBuilder<OrderHistory> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Sku).HasMaxLength(40).IsRequired();
        builder.Property(o => o.UnitPrice).HasColumnType("decimal(18,2)");

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.CatalogItem)
            .WithMany()
            .HasForeignKey(o => o.Sku)
            .HasPrincipalKey(c => c.Sku)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => new { o.CustomerId, o.Sku });
    }
}
