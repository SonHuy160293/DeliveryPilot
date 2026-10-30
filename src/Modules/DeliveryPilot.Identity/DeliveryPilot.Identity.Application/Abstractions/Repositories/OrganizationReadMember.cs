namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public sealed record OrganizationReadMember(Guid MembershipId, Guid UserId, string DisplayName, string PrimaryEmail, string Status, IReadOnlyList<OrganizationReadRole> Roles, DateTimeOffset JoinedAt, long Version);
