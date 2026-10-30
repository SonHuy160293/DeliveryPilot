using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public interface IOrganizationMembershipStore
{
    Task<OrganizationMembershipRoleState> LoadRolesAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken);
    void AddAssignment(OrganizationRoleAssignment assignment);
}
