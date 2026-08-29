using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class PriceRuleConfiguration : IEntityTypeConfiguration<PriceRule>
{
    public void Configure(EntityTypeBuilder<PriceRule> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Scope).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Target).HasMaxLength(100).IsRequired();
        builder.Property(p => p.DiscountPct).HasColumnType("decimal(5,4)");

        builder.HasIndex(p => new { p.Scope, p.Target });
    }
}
