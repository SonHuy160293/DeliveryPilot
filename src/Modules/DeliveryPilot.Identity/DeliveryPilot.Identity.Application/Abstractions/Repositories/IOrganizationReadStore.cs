namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public interface IOrganizationReadStore
{
    Task<OrganizationReadPage<OrganizationReadWorkspace>> ListWorkspacesAsync(Guid userId, int limit, string? cursor, CancellationToken cancellationToken);
    Task<OrganizationReadMember?> GetMemberAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken);
    Task<OrganizationReadPage<OrganizationReadMember>> ListMembersAsync(Guid organizationId, string status, int limit, string? cursor, CancellationToken cancellationToken);
}
