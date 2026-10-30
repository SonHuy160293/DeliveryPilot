using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.ReissueInvitation;

public sealed class ReissueInvitationValidator : IValidator<ReissueInvitationCommand>
{
    public Task ValidateAsync(ReissueInvitationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvitationValidation.Scope(request.OrganizationId, request.InvitationId);
        InvitationValidation.Version(request.ExpectedVersion);
        InvitationValidation.Roles(request.RoleIds);
        InvitationValidation.Reason(request.Reason, true);
        return Task.CompletedTask;
    }
}
