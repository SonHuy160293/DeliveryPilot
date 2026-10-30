using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.ResendInvitation;

public sealed class ResendInvitationValidator : IValidator<ResendInvitationCommand>
{
    public Task ValidateAsync(ResendInvitationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvitationValidation.Scope(request.OrganizationId, request.InvitationId);
        InvitationValidation.Version(request.ExpectedVersion);
        InvitationValidation.Reason(request.Reason, false);
        return Task.CompletedTask;
    }
}
