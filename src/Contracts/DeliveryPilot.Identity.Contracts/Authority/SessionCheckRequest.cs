namespace DeliveryPilot.Identity.Contracts.Authority;

/// <summary>
/// Internal request asking Identity whether an authenticated session is still usable.
/// </summary>
public sealed record SessionCheckRequest(Guid UserId, Guid SessionId);
