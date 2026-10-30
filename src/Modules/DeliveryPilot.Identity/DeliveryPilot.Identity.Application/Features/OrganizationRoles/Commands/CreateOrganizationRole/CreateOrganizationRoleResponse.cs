namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Commands.CreateOrganizationRole;

public sealed record CreateOrganizationRoleResponse(Guid Id, string Code, string Name, string? Description, string Origin, IReadOnlyList<string> PermissionCodes, long Version, long PermissionSetVersion, DateTimeOffset? RetiredAt);
