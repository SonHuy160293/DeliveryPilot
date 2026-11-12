namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed class ProjectParticipantRole
{
    private ProjectParticipantRole() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public string RoleCode { get; private set; } = null!;
    public DateTimeOffset GrantedAt { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    public static ProjectParticipantRole Grant(ProjectParticipant participant, string roleCode, Guid grantedByUserId, DateTimeOffset grantedAt)
    {
        ArgumentNullException.ThrowIfNull(participant);
        return new ProjectParticipantRole
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = participant.OrganizationId,
            ProjectId = participant.ProjectId,
            ParticipantId = participant.Id,
            RoleCode = ProjectFields.Role(roleCode, participant.Kind == ProjectParticipant.GuestKind),
            GrantedByUserId = ProjectFields.NonEmptyId(grantedByUserId, nameof(grantedByUserId)),
            GrantedAt = grantedAt.ToUniversalTime(),
        };
    }
}
