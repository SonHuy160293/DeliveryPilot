namespace DeliveryPilot.SharedKernel.Security;

/// <summary>
/// Checks, after cryptographic token validation, whether an authenticated context is still
/// usable for a new protected request. Implementations must consult authoritative state
/// without caching and must not grant access when that state is unavailable.
/// </summary>
public interface IAuthenticatedContextValidator
{
    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}
