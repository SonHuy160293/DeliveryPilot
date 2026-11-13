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

    public bool ChangeSprintLength(short weeks, long expectedVersion, DateTimeOffset now)
    {
        CheckVersion(expectedVersion);
        if (weeks is < 1 or > 4)
        {
            throw new BadRequestException("Project.InvalidSprintLength", "Sprint length must be one to four weeks.");
        }

        if (SprintLengthWeeks == weeks)
        {
            return false;
        }

        SprintLengthWeeks = weeks;
        Touch(now);
        return true;
    }

    public bool ChangeEngagement(string kind, Guid? engagementId, long expectedVersion, DateTimeOffset now)
    {
        CheckVersion(expectedVersion);
        if (Status != DraftStatus)
        {
            throw new ConflictException("Project.NotDraft", "The project is not Draft.");
        }

        if (kind is not (ClientKind or InternalKind) || (kind == InternalKind && engagementId.HasValue))
        {
            throw new BadRequestException("Project.InvalidKind", "Internal projects cannot have an engagement.");
        }

        if (Kind == kind && EngagementId == engagementId)
        {
            return false;
        }

        Kind = kind;
        EngagementId = ProjectFields.OptionalId(engagementId, nameof(engagementId));
        Touch(now);
        return true;
    }

    private void CheckVersion(long expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new ConflictException("Project.VersionConflict", "The project has changed.");
        }
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now.ToUniversalTime();
        Version = checked(Version + 1);
    }

    public static ProjectCreation CreateWithAdministrator(Guid organizationId, Guid createdByUserId, DateTimeOffset createdAt, string name, string purpose, string kind, Guid? engagementId)
    {
        Project project = Create(organizationId, createdByUserId, createdAt, name, purpose, kind, engagementId);
        ProjectBrief brief = ProjectBrief.CreateFor(project);
        ProjectParticipant participant = ProjectParticipant.CreateMember(organizationId, project.Id, createdByUserId, createdAt);
        ProjectParticipantRole role = ProjectParticipantRole.Grant(participant, ProjectRoles.ProjectAdministrator, createdByUserId, createdAt);
        return new ProjectCreation(project, brief, participant, role);
    }

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
