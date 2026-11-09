using System.Net;
using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.Projects.Infrastructure.Identity;
using DeliveryPilot.Projects.Tests.Support;
using DeliveryPilot.SharedKernel.Integrations;

namespace DeliveryPilot.Projects.Tests.Identity;

public sealed class IdentitySessionValidatorTests
{
    [Fact]
    public async Task ActiveSessionIsAccepted()
    {
        var validator = new IdentitySessionValidator(new FakeIdentityAuthorityClient());

        Assert.True(await validator.IsActiveAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task InactiveSessionIsRejected()
    {
        var client = new FakeIdentityAuthorityClient
        {
            Session = IntegrationResult<SessionCheckResult>.Success(new SessionCheckResult(false), HttpStatusCode.OK),
        };

        Assert.False(await new IdentitySessionValidator(client).IsActiveAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UnavailableIdentityFailsClosed()
    {
        var client = new FakeIdentityAuthorityClient { Session = FakeIdentityAuthorityClient.Unavailable<SessionCheckResult>() };

        Assert.False(await new IdentitySessionValidator(client).IsActiveAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }
}
