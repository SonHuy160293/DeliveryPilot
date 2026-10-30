using DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;

namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.ChangeRoleAssignment;

public sealed record ChangeRoleAssignmentResponse(Guid MembershipId, long Version, IReadOnlyList<OrganizationRoleResponse> Roles, bool Changed);
