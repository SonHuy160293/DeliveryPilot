using System.Net;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Tests.Support;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Integrations;

namespace DeliveryPilot.Projects.Tests.Identity;

public sealed class IdentityUserDirectoryTests
{
    [Fact]
    public async Task BatchesDeduplicatedIdsAndLeavesUnknownUsersOutWithoutCaching()
    {
        Guid[] ids = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();
        string name = "Before";
        var client = new FakeIdentityAuthorityClient
        {
            Lookup = request =>
            {
                UserIntegrationDto[] users = request.UserIds!.Where(id => id != ids[0]).Select(id => new UserIntegrationDto(id, name, null, "active")).ToArray();
                return IntegrationResult<UserLookupResult>.Success(new UserLookupResult(users), HttpStatusCode.OK);
            },
        };
        var directory = new IdentityUserDirectory(client);
        IReadOnlyDictionary<Guid, string> first = await directory.LookupDisplayNamesAsync([.. ids, ids[0]], CancellationToken.None);
        Assert.Equal([100, 1], client.Lookups.Select(request => request.UserIds!.Count));
        Assert.Equal(100, first.Count);
        Assert.False(first.ContainsKey(ids[0]));
        Assert.Equal("Before", first[ids[1]]);
        name = "After";
        IReadOnlyDictionary<Guid, string> second = await directory.LookupDisplayNamesAsync([ids[1]], CancellationToken.None);
        Assert.Equal("After", second[ids[1]]);
        Assert.Equal(3, client.Lookups.Count);
    }

    [Fact]
    public async Task EmptyLookupsMakeNoNetworkCallAndCancellationPropagates()
    {
        var client = new FakeIdentityAuthorityClient();
        var directory = new IdentityUserDirectory(client);
        Assert.Empty(await directory.LookupDisplayNamesAsync([], CancellationToken.None));
        Assert.Empty(client.Lookups);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => directory.LookupDisplayNamesAsync([Guid.NewGuid()], cancellation.Token));
        Assert.Empty(client.Lookups);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("null-data")]
    [InlineData("null-users")]
    [InlineData("null-name")]
    [InlineData("null-user")]
    [InlineData("foreign-id")]
    [InlineData("duplicate-id")]
    public async Task FailuresAndMalformedAnswersFailClosedWithSafe503(string mode)
    {
        Guid id = Guid.NewGuid();
        var user = new UserIntegrationDto(id, "Name", null, "active");
        var client = new FakeIdentityAuthorityClient
        {
            Lookup = _ => mode == "unavailable" ? FakeIdentityAuthorityClient.Unavailable<UserLookupResult>()
                : IntegrationResult<UserLookupResult>.Success(
                mode switch
                {
                    "null-data" => null!,
                    "null-users" => new UserLookupResult(null!),
                    "null-name" => new UserLookupResult([user with { DisplayName = null! }]),
                    "null-user" => new UserLookupResult([null!]),
                    "foreign-id" => new UserLookupResult([user with { Id = Guid.NewGuid() }]),
                    "duplicate-id" => new UserLookupResult([user, user]),
                    _ => throw new ArgumentException("Unknown failure mode.", nameof(mode)),
                }, HttpStatusCode.OK),
        };
        var directory = new IdentityUserDirectory(client);
        ServiceUnavailableException failure = await Assert.ThrowsAsync<ServiceUnavailableException>(() => directory.LookupDisplayNamesAsync([id], CancellationToken.None));
        Assert.Equal("Identity.Unavailable", failure.Code);
        Assert.DoesNotContain(id.ToString(), failure.Message, StringComparison.Ordinal);
    }
}
