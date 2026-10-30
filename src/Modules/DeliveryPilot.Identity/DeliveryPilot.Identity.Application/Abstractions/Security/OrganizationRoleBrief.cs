namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public sealed record OrganizationRoleBrief(
    Guid Id,
    string Code,
    string Name,
    string Origin,
    long Version,
    long PermissionSetVersion);
