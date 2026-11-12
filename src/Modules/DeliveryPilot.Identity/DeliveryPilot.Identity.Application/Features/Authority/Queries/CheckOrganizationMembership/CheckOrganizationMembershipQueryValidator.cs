using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckOrganizationMembership;

public sealed class CheckOrganizationMembershipQueryValidator : IValidator<CheckOrganizationMembershipQuery>
{
    public Task ValidateAsync(CheckOrganizationMembershipQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.OrganizationId is null || request.OrganizationId == Guid.Empty || request.UserIds is null || request.UserIds.Count is < 1 or > 100 || request.UserIds.Contains(Guid.Empty))
        {
            throw new CqrsValidationException(["OrganizationId and between 1 and 100 nonempty user identifiers are required."]);
        }

        return Task.CompletedTask;
    }
}
