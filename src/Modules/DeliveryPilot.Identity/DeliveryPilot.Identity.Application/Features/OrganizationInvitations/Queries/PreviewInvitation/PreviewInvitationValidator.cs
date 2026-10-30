using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.PreviewInvitation;

public sealed class PreviewInvitationValidator : IValidator<PreviewInvitationQuery>
{
    public Task ValidateAsync(PreviewInvitationQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.InvitationId == Guid.Empty)
        {
            throw new CqrsValidationException(["A valid invitation identifier is required."]);
        }

        return Task.CompletedTask;
    }
}
