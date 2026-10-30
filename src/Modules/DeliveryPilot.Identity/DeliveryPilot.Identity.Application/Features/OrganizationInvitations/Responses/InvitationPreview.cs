namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;

public sealed record InvitationPreview(Guid InvitationId, Guid OrganizationId, string WorkspaceName,
    IReadOnlyList<InvitationRoleSummary> OfferedRoles, DateTimeOffset ExpiresAt, long Version, string Status,
    bool CanAccept, bool RequiresReissue);
