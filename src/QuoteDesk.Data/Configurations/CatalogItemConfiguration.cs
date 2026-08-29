using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Sku).HasMaxLength(40).IsRequired();
        builder.HasIndex(c => c.Sku).IsUnique();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Category).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Uom).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Attributes).HasMaxLength(100);

        builder.Property(c => c.ListPrice).HasColumnType("decimal(18,2)");
        builder.Property(c => c.CostPrice).HasColumnType("decimal(18,2)");

        builder.HasOne(c => c.StockLevel)
            .WithOne(s => s.CatalogItem)
            .HasForeignKey<StockLevel>(s => s.Sku)
            .HasPrincipalKey<CatalogItem>(c => c.Sku);
    }
}
