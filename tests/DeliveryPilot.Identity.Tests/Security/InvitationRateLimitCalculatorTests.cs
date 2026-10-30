using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Infrastructure.Security;
using Xunit;

namespace DeliveryPilot.Identity.Tests.Security;

public sealed class InvitationRateLimitCalculatorTests
{
    [Fact]
    public void UsesLatestApplicableWindowAndRoundsUp()
    {
        var now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        var limits = new InvitationIssuanceLimits(TimeSpan.FromSeconds(60), 2, 2);
        DateTimeOffset[] pair = [now.AddDays(-1).AddSeconds(120.5), now.AddSeconds(-1)];
        DateTimeOffset[] workspace = [now.AddHours(-1).AddSeconds(200), now.AddSeconds(-1)];

        Assert.Equal(200, InvitationRateLimitCalculator.RetryAfterSeconds(now, pair, workspace, limits));
        Assert.Equal(121, InvitationRateLimitCalculator.RetryAfterSeconds(now, pair, [], limits));
        Assert.Equal(59, InvitationRateLimitCalculator.RetryAfterSeconds(now, [now.AddSeconds(-1)], [], limits));
        Assert.Equal(0, InvitationRateLimitCalculator.RetryAfterSeconds(now, [now.AddSeconds(-60)], [], limits));
    }
}
