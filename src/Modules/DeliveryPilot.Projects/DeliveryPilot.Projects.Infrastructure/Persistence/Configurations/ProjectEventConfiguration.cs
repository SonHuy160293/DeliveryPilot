using DeliveryPilot.Projects.Domain.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryPilot.Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectEventConfiguration : IEntityTypeConfiguration<ProjectEvent>
{
    public void Configure(EntityTypeBuilder<ProjectEvent> builder)
    {
        ProjectConfigurationRules.Key(builder, "ProjectEvents");
        ProjectConfigurationRules.Child(builder, "ProjectEvents");
        builder.Property(x => x.EventType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.SubjectType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.Details).HasColumnType("jsonb").IsRequired();
        builder.ToTable("ProjectEvents", "project", table =>
        {
            table.HasCheckConstraint("CkProjectEventsType", "char_length(btrim(\"EventType\")) BETWEEN 1 AND 160 AND \"EventType\" = btrim(\"EventType\")");
            table.HasCheckConstraint("CkProjectEventsSubject", "\"SubjectType\" IN ('Project', 'Brief', 'Participant', 'RoleGrant', 'Invitation', 'Stakeholder', 'ClientPo', 'GuestShare')");
            table.HasCheckConstraint("CkProjectEventsVersion", "\"ProjectVersion\" > 0");
            table.HasCheckConstraint("CkProjectEventsDetails", "jsonb_typeof(\"Details\") = 'object'");
        });
        builder.HasIndex(x => new { x.OrganizationId, x.ProjectId, x.OccurredAt, x.Id }).HasDatabaseName("IxProjectEventsOrganizationProjectOccurredId");
    }
}
