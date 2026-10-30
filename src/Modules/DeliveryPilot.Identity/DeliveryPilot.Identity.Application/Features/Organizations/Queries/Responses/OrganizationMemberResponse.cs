namespace DeliveryPilot.Identity.Application.Features.Organizations.Queries.Responses;

public sealed record OrganizationMemberResponse(Guid MembershipId, Guid UserId, string DisplayName, string PrimaryEmail, string Status, IReadOnlyList<OrganizationRoleResponse> Roles, DateTimeOffset JoinedAt, long Version);
