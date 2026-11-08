using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Queries.ListMyOwnershipNominations;

public sealed class ListMyOwnershipNominationsHandler(IOwnershipNominationStore store) : IQueryHandler<ListMyOwnershipNominationsQuery, OrganizationReadPage<NominationSummary>>
{
    public async Task<Result<OrganizationReadPage<NominationSummary>>> HandleAsync(ListMyOwnershipNominationsQuery request, CancellationToken cancellationToken)
        => Result.Success(await store.ReadPageAsync(null, request.Status, request.Limit, request.Cursor, cancellationToken));
}
