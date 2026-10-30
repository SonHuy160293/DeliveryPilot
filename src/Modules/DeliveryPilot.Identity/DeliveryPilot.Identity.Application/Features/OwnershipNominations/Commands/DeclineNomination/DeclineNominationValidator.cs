using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Commands.DeclineNomination;

public sealed class DeclineNominationValidator : IValidator<DeclineNominationCommand>
{
    public Task ValidateAsync(DeclineNominationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NominationValidation.Scope(request.OrganizationId, request.NominationId);
        NominationValidation.Transition(request.ExpectedVersion);
        return Task.CompletedTask;
    }
}
