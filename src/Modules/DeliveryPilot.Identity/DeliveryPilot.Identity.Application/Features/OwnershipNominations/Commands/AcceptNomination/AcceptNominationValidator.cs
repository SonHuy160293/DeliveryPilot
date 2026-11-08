using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.AcceptNomination;

public sealed class AcceptNominationValidator : IValidator<AcceptNominationCommand>
{
    public Task ValidateAsync(AcceptNominationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NominationValidation.Scope(request.OrganizationId, request.NominationId);
        NominationValidation.Transition(request.ExpectedVersion);
        return Task.CompletedTask;
    }
}
