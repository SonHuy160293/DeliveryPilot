using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Infrastructure.Identity;

/// <summary>
/// Confirms with Identity, uncached, that a validly signed token's session is still active (ADR-001 D-02).
/// Any failure to obtain a positive answer rejects the request.
/// </summary>
public sealed class IdentitySessionValidator(IIdentityAuthorityClient client) : IAuthenticatedContextValidator
{
    public bool IsUnavailable { get; private set; }

    public async Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await client.CheckSessionAsync(new SessionCheckRequest(userId, sessionId), cancellationToken);
        IsUnavailable = !result.IsSuccess || result.Data is null;
        return result.IsSuccess && result.Data is { Active: true };
    }
}
