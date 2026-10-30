using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Queries.ListOwnershipNominations;

public sealed class ListOwnershipNominationsHandler(IOwnershipNominationStore store) : IQueryHandler<ListOwnershipNominationsQuery, OrganizationReadPage<NominationSummary>>
{
    public async Task<Result<OrganizationReadPage<NominationSummary>>> HandleAsync(ListOwnershipNominationsQuery request, CancellationToken cancellationToken)
        => Result.Success(await store.ReadPageAsync(request.OrganizationId, request.Status, request.Limit, request.Cursor, cancellationToken));
}
