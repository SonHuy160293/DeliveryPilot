using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.IssueInvitation;

public sealed class IssueInvitationValidator : IValidator<IssueInvitationCommand>
{
    public Task ValidateAsync(IssueInvitationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvitationValidation.Scope(request.OrganizationId);
        InvitationValidation.Email(request.RecipientEmail);
        InvitationValidation.Roles(request.RoleIds);
        return Task.CompletedTask;
    }
}
