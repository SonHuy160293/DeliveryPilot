using DeliveryPilot.Identity.Contracts;

namespace DeliveryPilot.Identity.Application.Features.Users.Queries.LookupUsers;

public interface IUserLookupStore
{
    Task<IReadOnlyList<UserIntegrationDto>> LookupAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
