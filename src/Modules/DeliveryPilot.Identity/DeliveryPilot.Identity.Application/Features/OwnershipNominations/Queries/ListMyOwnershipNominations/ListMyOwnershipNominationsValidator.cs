using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Queries.ListMyOwnershipNominations;

public sealed class ListMyOwnershipNominationsValidator : IValidator<ListMyOwnershipNominationsQuery>
{
    public Task ValidateAsync(ListMyOwnershipNominationsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NominationValidation.Page(null, request.Status, request.Limit, request.Cursor);
        return Task.CompletedTask;
    }
}
