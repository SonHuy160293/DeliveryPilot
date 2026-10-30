namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;

public sealed record OrganizationWorkspaceResponse(Guid OrganizationId, string Name, IReadOnlyList<OrganizationRoleResponse> Roles, Guid MembershipId, long Version, long MembershipVersion);
