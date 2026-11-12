using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectStakeholderConfiguration : IEntityTypeConfiguration<ProjectStakeholder>
{
    public void Configure(EntityTypeBuilder<ProjectStakeholder> builder)
    {
        ProjectConfigurationRules.Key(builder, "ProjectStakeholders");
        ProjectConfigurationRules.Child(builder, "ProjectStakeholders");
        ProjectConfigurationRules.Version(builder, "ProjectStakeholders");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Side).HasMaxLength(16).IsRequired();
        builder.Property(x => x.OrganizationName).HasMaxLength(200);
        builder.Property(x => x.Responsibilities).HasMaxLength(2000);
        builder.Property(x => x.DecisionAreas).HasMaxLength(2000);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.HasOne<ProjectParticipant>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ProjectId, x.ParticipantId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ProjectId, x.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FkProjectStakeholdersParticipants");
        builder.ToTable("ProjectStakeholders", "project", table =>
        {
            table.HasCheckConstraint("CkProjectStakeholdersName", "char_length(btrim(\"Name\")) BETWEEN 1 AND 200 AND \"Name\" = btrim(\"Name\")");
            table.HasCheckConstraint("CkProjectStakeholdersSide", "\"Side\" IN ('Vendor', 'Client')");
            table.HasCheckConstraint("CkProjectStakeholdersEmail", "\"Email\" IS NULL OR (char_length(\"Email\") BETWEEN 3 AND 320 AND \"Email\" = lower(btrim(\"Email\")))");
            table.HasCheckConstraint("CkProjectStakeholdersRetirement", "(\"RetiredAt\" IS NULL AND \"RetiredByUserId\" IS NULL) OR (\"RetiredAt\" IS NOT NULL AND \"RetiredByUserId\" IS NOT NULL)");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.ParticipantId }).HasDatabaseName("IxProjectStakeholdersParticipant");
    }
}
