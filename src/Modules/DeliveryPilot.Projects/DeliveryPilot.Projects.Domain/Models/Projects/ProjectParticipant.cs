namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed class ProjectParticipant
{
    public const string MemberKind = "Member";
    public const string GuestKind = "Guest";
    public const string ActiveStatus = "Active";
    public const string RemovedStatus = "Removed";
    public const string RevokedStatus = "Revoked";
    public const string LeftStatus = "Left";

    private ProjectParticipant() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public string Kind { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public DateTimeOffset FirstJoinedAt { get; private set; }
    public DateTimeOffset ActiveSince { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public Guid? EndedByUserId { get; private set; }
    public Guid? AdmittedViaInvitationId { get; private set; }
    public long Version { get; private set; }

    public static ProjectParticipant CreateMember(Guid organizationId, Guid projectId, Guid userId, DateTimeOffset joinedAt)
        => Create(organizationId, projectId, userId, joinedAt, MemberKind, null);

    public static ProjectParticipant CreateGuest(Guid organizationId, Guid projectId, Guid userId, DateTimeOffset joinedAt, Guid invitationId)
        => Create(organizationId, projectId, userId, joinedAt, GuestKind, ProjectFields.NonEmptyId(invitationId, nameof(invitationId)));

    private static ProjectParticipant Create(Guid organizationId, Guid projectId, Guid userId, DateTimeOffset joinedAt, string kind, Guid? invitationId)
        => new()
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = ProjectFields.NonEmptyId(organizationId, nameof(organizationId)),
            ProjectId = ProjectFields.NonEmptyId(projectId, nameof(projectId)),
            UserId = ProjectFields.NonEmptyId(userId, nameof(userId)),
            Kind = kind,
            Status = ActiveStatus,
            FirstJoinedAt = joinedAt.ToUniversalTime(),
            ActiveSince = joinedAt.ToUniversalTime(),
            AdmittedViaInvitationId = invitationId,
            Version = 1,
        };
}
