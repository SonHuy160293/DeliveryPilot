namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Responses;

public sealed record InvitationSummary(Guid Id, Guid OrganizationId, string RecipientEmail, string Status, IReadOnlyList<InvitationRoleSummary> OfferedRoles, DateTimeOffset ExpiresAt, long Version, InvitationDeliverySummary Delivery);
