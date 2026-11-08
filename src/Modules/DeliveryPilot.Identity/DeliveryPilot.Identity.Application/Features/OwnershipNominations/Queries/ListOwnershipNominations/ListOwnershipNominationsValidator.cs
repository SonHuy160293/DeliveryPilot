using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Queries.ListOwnershipNominations;

public sealed class ListOwnershipNominationsValidator : IValidator<ListOwnershipNominationsQuery>
{
    public Task ValidateAsync(ListOwnershipNominationsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NominationValidation.Page(request.OrganizationId, request.Status, request.Limit, request.Cursor);
        return Task.CompletedTask;
    }
}
