using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public sealed record OrganizationMembershipRoleState(IReadOnlyList<OrganizationRole> Roles, IReadOnlyList<OrganizationRoleAssignment> Assignments);
