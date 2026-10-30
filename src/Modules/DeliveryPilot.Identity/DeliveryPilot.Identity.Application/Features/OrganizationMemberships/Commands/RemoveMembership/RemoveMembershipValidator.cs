using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationMemberships.Commands.RemoveMembership;

public sealed class RemoveMembershipValidator : IValidator<RemoveMembershipCommand>
{
    public Task ValidateAsync(RemoveMembershipCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MembershipChangeValidation.Validate(request.OrganizationId, request.MembershipId, request.ExpectedMembershipVersion, request.Reason);
        return Task.CompletedTask;
    }
}
