namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public sealed record OrganizationWorkspaceBrief(
    Guid OrganizationId,
    string Name,
    long Version,
    Guid MembershipId,
    long MembershipVersion,
    DateTimeOffset JoinedAt,
    IReadOnlyList<OrganizationRoleBrief> EffectiveRoles);
