using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed class Project
{
    public const string ClientKind = "Client";
    public const string InternalKind = "Internal";
    public const string DraftStatus = "Draft";
    public const string ActiveStatus = "Active";

    private Project() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Purpose { get; private set; } = null!;
    public string Kind { get; private set; } = null!;
    public Guid? EngagementId { get; private set; }
    public string Status { get; private set; } = null!;
    public short SprintLengthWeeks { get; private set; }
    public Guid? ClientProductOwnerParticipantId { get; private set; }
    public DateTimeOffset? ClientPoAssignedAt { get; private set; }
    public Guid? ClientPoAssignedByUserId { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public Guid? ActivatedByUserId { get; private set; }
    public string? ClientPoWaiverReason { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public long Version { get; private set; }

    public static Project Create(Guid organizationId, Guid createdByUserId, DateTimeOffset createdAt, string name, string purpose, string kind, Guid? engagementId = null)
    {
        if (kind is not (ClientKind or InternalKind) || (kind == InternalKind && engagementId.HasValue))
        {
            throw new BadRequestException("Project.InvalidKind", "A client or internal project is required; internal projects have no engagement.");
        }

        DateTimeOffset instant = createdAt.ToUniversalTime();
        return new Project
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = ProjectFields.NonEmptyId(organizationId, nameof(organizationId)),
            CreatedByUserId = ProjectFields.NonEmptyId(createdByUserId, nameof(createdByUserId)),
            CreatedAt = instant,
            UpdatedAt = instant,
            Name = ProjectFields.RequiredText(name, 160, nameof(name)),
            Purpose = ProjectFields.RequiredText(purpose, 500, nameof(purpose)),
            Kind = kind,
            EngagementId = ProjectFields.OptionalId(engagementId, nameof(engagementId)),
            Status = DraftStatus,
            SprintLengthWeeks = 2,
            Version = 1,
        };
    }
}
