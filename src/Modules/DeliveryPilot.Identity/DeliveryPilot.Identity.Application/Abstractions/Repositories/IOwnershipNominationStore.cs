using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.Identity.Domain.Models;

namespace DeliveryPilot.Identity.Application.Abstractions.Repositories;

public interface IOwnershipNominationStore
{
    Task<OrganizationOwnershipNomination?> FindAsync(Guid organizationId, Guid nominationId, CancellationToken cancellationToken);
    Task<OrganizationOwnershipNomination?> FindPendingAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken);
    Task CloseExpiredAsync(OrganizationOwnershipNomination nomination, DateTimeOffset now, CancellationToken cancellationToken);
    void Add(OrganizationOwnershipNomination nomination);
    Task<OrganizationReadPage<NominationSummary>> ReadPageAsync(Guid? organizationId, string status, int limit, string? cursor, CancellationToken cancellationToken);
}
