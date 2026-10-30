using DeliveryPilot.Identity.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("AuthSessions", "shared", table =>
        {
            table.HasCheckConstraint("CkAuthSessionsRefreshDeadline", "\"RefreshExpiresAt\" > \"CreatedAt\"");
            table.HasCheckConstraint("CkAuthSessionsRefreshTokenVerifier", "octet_length(\"RefreshTokenVerifier\") = 32");
        });

        builder.HasKey(x => x.Id).HasName("PkAuthSessions");
        builder.Ignore(x => x.IsRevoked);
        builder.Property(x => x.Id).HasColumnName("Id").HasColumnType("uuid").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("UserId").HasColumnType("uuid").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("CreatedAt").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RefreshExpiresAt).HasColumnName("RefreshExpiresAt").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.RefreshTokenVerifier).HasColumnName("RefreshTokenVerifier").HasColumnType("bytea").IsRequired();
        builder.Property(x => x.RefreshRotatedAt).HasColumnName("RefreshRotatedAt").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.RevokedAt).HasColumnName("RevokedAt").HasColumnType("timestamptz").IsRequired(false);

        builder.HasIndex(x => x.UserId).HasDatabaseName("IxAuthSessionsUserId");
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("FkAuthSessionsUsersUserId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
