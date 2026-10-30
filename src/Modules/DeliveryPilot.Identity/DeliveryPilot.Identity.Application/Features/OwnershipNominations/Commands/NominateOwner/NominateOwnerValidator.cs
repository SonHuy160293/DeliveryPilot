using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.NominateOwner;

public sealed class NominateOwnerValidator : IValidator<NominateOwnerCommand>
{
    public Task ValidateAsync(NominateOwnerCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NominationValidation.Scope(request.OrganizationId, request.MembershipId);
        return Task.CompletedTask;
    }
}
