using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public sealed class EngagementLinkRateLimitException(int retryAfterSeconds)
    : ConflictException("EngagementLink.RateLimited", "Link code attempts are temporarily limited.")
{
    public int RetryAfterSeconds { get; } = Math.Max(1, retryAfterSeconds);
}
