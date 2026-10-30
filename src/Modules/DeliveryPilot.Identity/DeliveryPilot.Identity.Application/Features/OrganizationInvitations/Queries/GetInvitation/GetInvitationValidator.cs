using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.GetInvitation;

public sealed class GetInvitationValidator : IValidator<GetInvitationQuery>
{
    public Task ValidateAsync(GetInvitationQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvitationValidation.Scope(request.OrganizationId, request.InvitationId);
        return Task.CompletedTask;
    }
}
