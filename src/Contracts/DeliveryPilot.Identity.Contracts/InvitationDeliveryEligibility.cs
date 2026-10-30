namespace DeliveryPilot.Identity.Contracts;

public sealed record InvitationDeliveryEligibility(bool Allowed, DateTimeOffset? ExpiresAt);
