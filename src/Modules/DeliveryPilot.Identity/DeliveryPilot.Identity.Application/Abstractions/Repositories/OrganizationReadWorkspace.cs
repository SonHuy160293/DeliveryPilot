namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public sealed record OrganizationReadWorkspace(Guid OrganizationId, string Name, IReadOnlyList<OrganizationReadRole> Roles, Guid MembershipId, long Version, long MembershipVersion);
