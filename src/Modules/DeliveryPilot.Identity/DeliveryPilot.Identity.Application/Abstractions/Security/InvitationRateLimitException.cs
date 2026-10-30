using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Abstractions.Security;

public sealed class InvitationRateLimitException : ConflictException
{
    public InvitationRateLimitException(int retryAfterSeconds)
        : base("Invitation.RateLimited", "Invitation issuance is temporarily limited.")
    {
        RetryAfterSeconds = Math.Max(1, retryAfterSeconds);
    }

    public int RetryAfterSeconds { get; }
}
