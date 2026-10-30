namespace DeliveryPilot.Identity.Application.Features.OwnershipNominations.Responses;

public sealed record NominationSummary(Guid Id, Guid OrganizationId, Guid MembershipId, Guid NominatedByUserId,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, long Version, bool CanAccept);
