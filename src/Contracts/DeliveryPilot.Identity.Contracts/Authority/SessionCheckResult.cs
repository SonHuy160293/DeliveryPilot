namespace DeliveryPilot.Identity.Contracts.Authority;

/// <summary>
/// True only when the session exists, belongs to the user, is not revoked and the account is active.
/// </summary>
public sealed record SessionCheckResult(bool Active);
