using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuoteDesk.Data.Entities;

namespace QuoteDesk.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        // The only explicit ToTable in the model: the entity is AppUser but the table is Users.
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.GoogleSubject).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Name).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PictureUrl).HasMaxLength(500);
        builder.Property(u => u.Role).HasMaxLength(20).IsRequired();

        // Both unique: the subject is how a returning user is matched, the email is how an admin is
        // recognised from configuration. Two people can never share either.
        builder.HasIndex(u => u.GoogleSubject).IsUnique();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
