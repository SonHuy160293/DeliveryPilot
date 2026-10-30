namespace DeliveryPilot.Identity.Application.Features.OrganizationRoles.Queries.ListDelegablePermissions;

public sealed record DelegablePermissionsResponse(IReadOnlyList<string> Codes);
