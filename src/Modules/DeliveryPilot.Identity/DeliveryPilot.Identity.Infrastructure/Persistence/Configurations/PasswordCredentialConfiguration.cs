using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class PasswordCredentialConfiguration : IEntityTypeConfiguration<PasswordCredential>
{
    public void Configure(EntityTypeBuilder<PasswordCredential> builder)
    {
        builder.ToTable("PasswordCredentials", "shared", table =>
            table.HasCheckConstraint("CkPasswordCredentialsPasswordHash", "char_length(btrim(\"PasswordHash\")) > 0"));

        builder.HasKey(x => x.UserId).HasName("PkPasswordCredentials");
        builder.Property(x => x.UserId).HasColumnName("UserId").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.PasswordHash).HasColumnName("PasswordHash").HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired();
        builder.HasOne<User>().WithOne()
            .HasForeignKey<PasswordCredential>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
