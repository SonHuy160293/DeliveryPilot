using DeliveryPilot.Identity.Contracts.Authority;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Infrastructure.Identity;

/// <summary>
/// Confirms with Identity, uncached, that a validly signed token's session is still active (ADR-001 D-02).
/// Any failure to obtain a positive answer rejects the request.
/// </summary>
public sealed class IdentitySessionValidator(IIdentityAuthorityClient client) : IAuthenticatedContextValidator
{
    public async Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await client.CheckSessionAsync(new SessionCheckRequest(userId, sessionId), cancellationToken);
        return result.IsSuccess && result.Data is { Active: true };
    }
}
