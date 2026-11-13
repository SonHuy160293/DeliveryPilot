using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed class ProjectStakeholder
{
    public const string VendorSide = "Vendor";
    public const string ClientSide = "Client";

    private ProjectStakeholder() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Side { get; private set; } = null!;
    public string? OrganizationName { get; private set; }
    public string? Responsibilities { get; private set; }
    public string? DecisionAreas { get; private set; }
    public string? Email { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public Guid? RetiredByUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public ProjectStakeholderValues Values() => new(Name, Side, OrganizationName, Responsibilities, DecisionAreas, Email, ParticipantId);

    public bool Update(ProjectStakeholderValues values, long expectedVersion, DateTimeOffset occurredAt)
    {
        CheckVersion(expectedVersion);
        if (RetiredAt.HasValue)
        {
            throw new ConflictException("ProjectStakeholder.Retired", "A retired stakeholder cannot be edited.");
        }

        ProjectStakeholderValues normalized = values.Normalize();
        if (normalized == Values())
        {
            return false;
        }

        Name = normalized.Name!;
        Side = normalized.Side!;
        OrganizationName = normalized.OrganizationName;
        Responsibilities = normalized.Responsibilities;
        DecisionAreas = normalized.DecisionAreas;
        Email = normalized.Email;
        ParticipantId = normalized.ParticipantId;
        UpdatedAt = occurredAt.ToUniversalTime();
        Version = checked(Version + 1);
        return true;
    }

    public bool Retire(long expectedVersion, Guid actor, DateTimeOffset occurredAt)
    {
        CheckVersion(expectedVersion);
        if (RetiredAt.HasValue)
        {
            return false;
        }

        RetiredAt = occurredAt.ToUniversalTime();
        RetiredByUserId = ProjectFields.NonEmptyId(actor, nameof(actor));
        UpdatedAt = RetiredAt.Value;
        Version = checked(Version + 1);
        return true;
    }

    private void CheckVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new ConflictException("ProjectStakeholder.VersionConflict", "The stakeholder has changed.");
        }
    }

    public static ProjectStakeholder Create(Guid organizationId, Guid projectId, Guid createdByUserId, DateTimeOffset createdAt, string name, string side, string? organizationName = null, string? responsibilities = null, string? decisionAreas = null, string? email = null, Guid? participantId = null)
    {
        if (side is not (VendorSide or ClientSide))
        {
            throw new BadRequestException("Project.InvalidStakeholderSide", "A vendor or client side is required.");
        }

        DateTimeOffset instant = createdAt.ToUniversalTime();
        return new ProjectStakeholder
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = ProjectFields.NonEmptyId(organizationId, nameof(organizationId)),
            ProjectId = ProjectFields.NonEmptyId(projectId, nameof(projectId)),
            CreatedByUserId = ProjectFields.NonEmptyId(createdByUserId, nameof(createdByUserId)),
            CreatedAt = instant,
            UpdatedAt = instant,
            Name = ProjectFields.RequiredText(name, 200, nameof(name)),
            Side = side,
            OrganizationName = ProjectFields.OptionalText(organizationName, 200, nameof(organizationName)),
            Responsibilities = ProjectFields.OptionalText(responsibilities, 2000, nameof(responsibilities)),
            DecisionAreas = ProjectFields.OptionalText(decisionAreas, 2000, nameof(decisionAreas)),
            Email = string.IsNullOrWhiteSpace(email) ? null : ProjectFields.Email(email),
            ParticipantId = ProjectFields.OptionalId(participantId, nameof(participantId)),
            Version = 1,
        };
    }
}
