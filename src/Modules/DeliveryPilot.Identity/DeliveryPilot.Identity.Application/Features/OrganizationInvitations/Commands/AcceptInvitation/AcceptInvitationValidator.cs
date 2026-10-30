using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.AcceptInvitation;

public sealed class AcceptInvitationValidator : IValidator<AcceptInvitationCommand>
{
    public Task ValidateAsync(AcceptInvitationCommand request, CancellationToken cancellationToken)
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
