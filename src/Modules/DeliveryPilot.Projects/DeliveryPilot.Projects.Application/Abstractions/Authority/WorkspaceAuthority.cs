namespace DeliveryPilot.Projects.Application.Abstractions.Authority;

/// <summary>
/// Workspace authority facts for an allowed caller, as reported by Identity at <see cref="EvaluatedAt"/>.
/// </summary>
public sealed record WorkspaceAuthority(
    Guid OrganizationId,
    Guid MembershipId,
    bool IsOwner,
    bool IsAdmin,
    IReadOnlySet<string> EffectivePermissions,
    DateTimeOffset EvaluatedAt)
{
    public bool HasPermission(string permissionCode)
        => EffectivePermissions.Contains(permissionCode);
}
