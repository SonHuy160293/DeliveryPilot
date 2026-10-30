using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;

public sealed record InvitationAcceptance(string Outcome, Guid MembershipId, Guid OrganizationId,
    IReadOnlyList<OrganizationRoleResponse> Roles, long MembershipVersion);
