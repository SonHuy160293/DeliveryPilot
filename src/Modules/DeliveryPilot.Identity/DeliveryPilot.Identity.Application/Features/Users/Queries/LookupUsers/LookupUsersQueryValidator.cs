using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Users.Queries.LookupUsers;

public sealed class LookupUsersQueryValidator : IValidator<LookupUsersQuery>
{
    public Task ValidateAsync(LookupUsersQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.UserIds is null || request.UserIds.Count is < 1 or > 100 || request.UserIds.Contains(Guid.Empty))
        {
            throw new CqrsValidationException(["UserIds must contain between 1 and 100 nonempty user identifiers."]);
        }

        return Task.CompletedTask;
    }
}
