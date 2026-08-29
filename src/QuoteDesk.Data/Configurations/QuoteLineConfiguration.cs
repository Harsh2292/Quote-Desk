using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Sku).HasMaxLength(40).IsRequired();
        builder.Property(l => l.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(l => l.DiscountPct).HasColumnType("decimal(5,4)");
        builder.Property(l => l.LineTotal).HasColumnType("decimal(18,2)");
        builder.Property(l => l.Note).HasMaxLength(500);

        builder.HasOne(l => l.CatalogItem)
            .WithMany()
            .HasForeignKey(l => l.Sku)
            .HasPrincipalKey(c => c.Sku)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
