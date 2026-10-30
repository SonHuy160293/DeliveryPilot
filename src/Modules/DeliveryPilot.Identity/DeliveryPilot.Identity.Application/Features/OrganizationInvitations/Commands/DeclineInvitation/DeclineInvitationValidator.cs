using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.DeclineInvitation;

public sealed class DeclineInvitationValidator : IValidator<DeclineInvitationCommand>
{
    public Task ValidateAsync(DeclineInvitationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.InvitationId == Guid.Empty)
        {
            throw new CqrsValidationException(["A valid invitation identifier is required."]);
        }

        InvitationValidation.Version(request.ExpectedVersion);

        return Task.CompletedTask;
    }
}
