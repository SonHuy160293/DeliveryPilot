namespace DeliveryPilot.Projects.Application.Abstractions.Authority;

/// <summary>
/// Current workspace-level authority from Identity (ADR-001 D-03/D-04). Workspace authority never grants
/// project access; project roles and guest grants are evaluated by Projects itself.
/// </summary>
public interface IWorkspaceAuthority
{
    /// <summary>
    /// Returns the caller's current authority in <paramref name="organizationId"/>, uncached.
    /// Throws <c>ForbiddenException</c> when Identity denies the caller or the required permission is
    /// missing, and <c>ServiceUnavailableException</c> when Identity cannot answer (fail closed).
    /// </summary>
    Task<WorkspaceAuthority> RequireAsync(
        Guid userId,
        Guid organizationId,
        Guid? sessionId,
        string? requiredPermissionCode,
        CancellationToken cancellationToken);
}
