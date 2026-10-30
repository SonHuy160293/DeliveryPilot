using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.CancelNomination;

public sealed class CancelNominationValidator : IValidator<CancelNominationCommand>
{
    public Task ValidateAsync(CancelNominationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NominationValidation.Scope(request.OrganizationId, request.NominationId);
        NominationValidation.Transition(request.ExpectedVersion, request.Reason);
        return Task.CompletedTask;
    }
}
