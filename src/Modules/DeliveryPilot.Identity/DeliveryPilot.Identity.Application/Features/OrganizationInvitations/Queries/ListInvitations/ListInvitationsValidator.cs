using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Queries.ListInvitations;

public sealed class ListInvitationsValidator : IValidator<ListInvitationsQuery>
{
    public Task ValidateAsync(ListInvitationsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvitationValidation.Scope(request.OrganizationId);
        InvitationValidation.Page(request.Status, request.Limit, request.Cursor);
        return Task.CompletedTask;
    }
}
