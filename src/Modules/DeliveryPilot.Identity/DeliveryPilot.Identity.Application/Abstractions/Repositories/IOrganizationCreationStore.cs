using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public interface IOrganizationCreationStore
{
    Task LockAttemptAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken);

    Task<OrganizationCreationAttempt?> FindAttemptAsync(Guid userId, Guid attemptId, CancellationToken cancellationToken);

    Task LockUserAsync(Guid userId, CancellationToken cancellationToken);

    Task LockSessionAndCheckEligibilityAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    void Stage(
        Organization organization,
        IReadOnlyCollection<OrganizationRole> roles,
        IReadOnlyCollection<OrganizationRolePermission> permissions,
        OrganizationMembership membership,
        OrganizationRoleAssignment assignment,
        OrganizationCreationAttempt attempt);
}
