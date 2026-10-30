using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;

public sealed class OrganizationMembershipStore(IdentityDbContext db) : IOrganizationMembershipStore
{
    public async Task<OrganizationMembershipRoleState> LoadRolesAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Membership mutations require the pipeline-owned transaction.");
        }

        try
        {
            List<OrganizationRole> roles = await db.OrganizationRoles.AsNoTracking().Where(role => role.OrganizationId == organizationId).ToListAsync(cancellationToken);
            List<OrganizationRoleAssignment> assignments = await db.OrganizationRoleAssignments.AsTracking()
                .Where(assignment => assignment.OrganizationId == organizationId && assignment.MembershipId == membershipId).ToListAsync(cancellationToken);
            return new OrganizationMembershipRoleState(roles, assignments);
        }
        catch (Exception exception) when (OrganizationAuthorizationService.IsStoreUnavailable(exception, cancellationToken))
        {
            throw OrganizationAuthorizationService.CreateAuthorityUnavailable();
        }
    }

    public void AddAssignment(OrganizationRoleAssignment assignment) => db.OrganizationRoleAssignments.Add(assignment);
}
