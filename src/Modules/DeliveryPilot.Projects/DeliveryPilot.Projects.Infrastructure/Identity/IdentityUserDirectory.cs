using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Projects.Application.Abstractions.Identity;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Integrations;

namespace DeliveryPilot.Projects.Infrastructure.Identity;

public sealed class IdentityUserDirectory(IIdentityAuthorityClient client) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> LookupDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();
        foreach (Guid[] batch in userIds.Distinct().Chunk(100))
        {
            IntegrationResult<UserLookupResult> result = await client.LookupUsersAsync(new UserLookupRequest(batch), cancellationToken);
            if (!result.IsSuccess || result.Data?.Users is null)
            {
                throw Unavailable();
            }

            foreach (UserIntegrationDto user in result.Data.Users)
            {
                if (user is null || !batch.Contains(user.Id) || user.DisplayName is null || !names.TryAdd(user.Id, user.DisplayName))
                {
                    throw Unavailable();
                }
            }
        }

        return names;
    }

    private static ServiceUnavailableException Unavailable()
        => new("Identity.Unavailable", "User display names could not be confirmed.");
}
