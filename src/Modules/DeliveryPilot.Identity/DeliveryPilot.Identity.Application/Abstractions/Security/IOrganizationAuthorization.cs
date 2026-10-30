namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public interface IOrganizationAuthorization
{
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

    Task<OrganizationMemberSecurityContext?> LoadMemberSecurityContextAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken = default);

    bool CanAccessProjectResource(OrganizationMemberSecurityContext memberContext, Guid projectId);
}
