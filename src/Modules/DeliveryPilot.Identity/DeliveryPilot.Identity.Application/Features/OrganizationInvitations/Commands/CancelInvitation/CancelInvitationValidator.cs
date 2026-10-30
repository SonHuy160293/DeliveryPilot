using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.CancelInvitation;

public sealed class CancelInvitationValidator : IValidator<CancelInvitationCommand>
{
    public Task ValidateAsync(CancelInvitationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvitationValidation.Scope(request.OrganizationId, request.InvitationId);
        InvitationValidation.Version(request.ExpectedVersion);
        InvitationValidation.Reason(request.Reason, true);
        return Task.CompletedTask;
    }
}
