using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public interface IOrganizationRoleStore
{
    void Add(OrganizationRole role, IReadOnlyCollection<OrganizationRolePermission> permissions);

    Task<OrganizationReadPage<OrganizationRoleRead>> ListAsync(Guid organizationId, int limit, string? cursor, CancellationToken cancellationToken);

    Task<OrganizationRoleRead?> FindForUpdateAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken);

    void UpdatePermissions(IReadOnlyCollection<OrganizationRolePermission> removed, IReadOnlyCollection<OrganizationRolePermission> added);

    Task<OrganizationRoleAssignment?> FindAssignmentAsync(Guid organizationId, Guid membershipId, Guid roleId, CancellationToken cancellationToken);

    void AddAssignment(OrganizationRoleAssignment assignment);
}
