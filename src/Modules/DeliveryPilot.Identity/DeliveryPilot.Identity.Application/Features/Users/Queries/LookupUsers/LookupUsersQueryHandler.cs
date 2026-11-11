using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Identity.Application.Features.Users.Queries.LookupUsers;

public sealed class LookupUsersQueryHandler(IUserLookupStore users) : IQueryHandler<LookupUsersQuery, UserLookupResult>
{
    public async Task<Result<UserLookupResult>> HandleAsync(LookupUsersQuery request, CancellationToken cancellationToken)
    {
        Guid[] userIds = request.UserIds!.Distinct().ToArray();
        IReadOnlyList<UserIntegrationDto> found = await users.LookupAsync(userIds, cancellationToken);
        return Result.Success(new UserLookupResult(found));
    }
}
