using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Commands.DemoteOwner;

public sealed class DemoteOwnerValidator : IValidator<DemoteOwnerCommand>
{
    public Task ValidateAsync(DemoteOwnerCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MembershipChangeValidation.Validate(request.OrganizationId, request.MembershipId, request.ExpectedMembershipVersion, request.Reason);
        return Task.CompletedTask;
    }
}
