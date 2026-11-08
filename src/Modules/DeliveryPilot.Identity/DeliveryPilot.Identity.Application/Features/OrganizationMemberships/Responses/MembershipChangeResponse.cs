using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Responses;

public sealed record MembershipChangeResponse(Guid MembershipId, string Status, IReadOnlyList<OrganizationRoleResponse> Roles, long Version, bool Changed);
