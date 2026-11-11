using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Users.Queries.LookupUsers;

public sealed record LookupUsersQuery(IReadOnlyList<Guid>? UserIds) : IQuery<UserLookupResult>
{
    public override string ToString() => nameof(LookupUsersQuery);
}
