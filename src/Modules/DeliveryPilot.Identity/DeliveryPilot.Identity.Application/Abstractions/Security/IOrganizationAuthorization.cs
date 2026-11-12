namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public interface IOrganizationAuthorization
{
    /// <summary>Uncached membership read; store failures throw <c>ServiceUnavailableException</c>.</summary>
    Task<IReadOnlySet<Guid>> GetActiveMemberUserIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    Task<Guid> GetAuthenticatedUserIdAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationWorkspaceBrief>> ListEligibleWorkspacesAsync(CancellationToken cancellationToken = default);

    Task<OrganizationMemberSecurityContext> AuthorizeMemberScopeAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMemberSecurityContext> AuthorizeCapabilityAsync(
        Guid organizationId,
        string requiredPermissionCode,
        CancellationToken cancellationToken = default);

    Task<OrganizationMemberSecurityContext> AuthorizeRoleCatalogReadAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    Task<OrganizationMemberSecurityContext> ReauthorizeJobActorAsync(
        Guid organizationId,
        Guid actorUserId,
        string? requiredPermissionCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uncached session eligibility for an explicit subject (internal integration checks). Store failures
    /// throw <c>ServiceUnavailableException</c>; they never report an active session.
    /// </summary>
    Task<bool> IsSessionActiveAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Non-throwing workspace authority evaluation for an explicit subject (internal integration checks).
    /// Returns <c>null</c> for every ineligible case without disclosing which check failed; store failures
    /// throw <c>ServiceUnavailableException</c>. A null <paramref name="sessionId"/> skips the session check
    /// for queued-work reauthorization.
    /// </summary>
    Task<OrganizationMemberSecurityContext?> EvaluateActorAsync(
        Guid organizationId,
        Guid userId,
        Guid? sessionId,
        string? requiredPermissionCode,
        CancellationToken cancellationToken = default);

    Task<OrganizationMemberSecurityContext?> LoadMemberSecurityContextAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken = default);

    bool CanAccessProjectResource(OrganizationMemberSecurityContext memberContext, Guid projectId);
}
